// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Test;

using System.Text.Json;
using ktsu.UndoRedo.Models;
using ktsu.UndoRedo.Core.Services;

[TestClass]
public class SerializationTests
{
	private static UndoRedoService CreateService() => new(new StackManager(), new SaveBoundaryManager(), new CommandMerger());

	[TestMethod]
	public async Task JsonSerializer_SerializeEmpty_ReturnsValidData()
	{
		// Arrange
		JsonUndoRedoSerializer serializer = new();

		// Act
		byte[] data = await serializer.SerializeAsync([], 0, [], true).ConfigureAwait(false);

		// Assert
		Assert.IsNotNull(data);
		Assert.IsNotEmpty(data);

		// Verify it can be deserialized
		UndoRedoStackState state = await serializer.DeserializeAsync(data).ConfigureAwait(false);
		Assert.IsNotNull(state);
		Assert.IsEmpty(state.Commands);
		Assert.AreEqual(0, state.CurrentPosition);
		Assert.IsEmpty(state.SaveBoundaries);
	}

	[TestMethod]
	public async Task JsonSerializer_SerializeDeserialize_PreservesBasicData()
	{
		// Arrange
		JsonUndoRedoSerializer serializer = new();

		List<ICommand> commands =
		[
			new DelegateCommand("Command 1", () => { }, () => { }, ChangeType.Insert, ["item1"]),
			new DelegateCommand("Command 2", () => { }, () => { }, ChangeType.Modify, ["item2"])
		];

		List<SaveBoundary> boundaries =
		[
			new SaveBoundary(1, "Save 1")
		];

		// Act
		byte[] data = await serializer.SerializeAsync(commands, 1, boundaries, false).ConfigureAwait(false);
		UndoRedoStackState state = await serializer.DeserializeAsync(data).ConfigureAwait(false);

		// Assert
		Assert.HasCount(2, state.Commands);
		Assert.AreEqual(1, state.CurrentPosition);
		Assert.HasCount(1, state.SaveBoundaries);
		Assert.AreEqual("json-v1.0", state.FormatVersion);
	}

	[TestMethod]
	public async Task JsonSerializer_DeserializeInvalidData_ThrowsException()
	{
		// Arrange
		JsonUndoRedoSerializer serializer = new();
		byte[] invalidData = "invalid json data"u8.ToArray();

		// Act & Assert
		await Assert.ThrowsExactlyAsync<JsonException>(async () =>
			await serializer.DeserializeAsync(invalidData).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task JsonSerializer_UnsupportedVersion_ThrowsNotSupportedException()
	{
		// Arrange
		JsonUndoRedoSerializer serializer = new();

		// Create data with unsupported version
		string json = """
			{
				"commands": [],
				"currentPosition": 0,
				"saveBoundaries": [],
				"formatVersion": "unsupported-v2.0",
				"timestamp": "2023-01-01T00:00:00Z"
			}
			""";
		byte[] data = JsonSerializer.SerializeToUtf8Bytes(JsonSerializer.Deserialize<object>(json));

		// Act & Assert
		await Assert.ThrowsExactlyAsync<NotSupportedException>(async () =>
			await serializer.DeserializeAsync(data).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[TestMethod]
	public void JsonSerializer_SupportsVersion_ChecksCorrectly()
	{
		// Arrange
		JsonUndoRedoSerializer serializer = new();

		// Act & Assert
		Assert.IsTrue(serializer.SupportsVersion("json-v1.0"), "Serializer should support json-v1.0 format");
		Assert.IsTrue(serializer.SupportsVersion("json-v1.1"), "Serializer should support json-v1.1 format");
		Assert.IsFalse(serializer.SupportsVersion("xml-v1.0"), "Serializer should not support xml-v1.0 format");
		Assert.IsFalse(serializer.SupportsVersion("json-v2.0"), "Serializer should not support json-v2.0 format");
	}

	[TestMethod]
	public async Task UndoRedoService_SaveLoadState_PreservesStackState()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		stack.SetSerializer(new JsonUndoRedoSerializer());

		int value = 0;
		stack.Execute(new DelegateCommand("Set 1", () => value = 1, () => value = 0));
		stack.Execute(new DelegateCommand("Set 2", () => value = 2, () => value = 1));
		stack.MarkAsSaved("Test save");
		stack.Execute(new DelegateCommand("Set 3", () => value = 3, () => value = 2));
		await stack.UndoAsync().ConfigureAwait(false); // Back to value = 2

		// Verify the value for testing purposes
		Assert.AreEqual(2, value);

		// Act
		byte[] data = await stack.SaveStateAsync().ConfigureAwait(false);

		// Create new stack and load state
		UndoRedoService newStack = CreateService();
		newStack.SetSerializer(new JsonUndoRedoSerializer());
		bool success = await newStack.LoadStateAsync(data).ConfigureAwait(false);

		// Assert
		Assert.IsTrue(success, "LoadStateAsync should return true on successful load");
		Assert.AreEqual(stack.CommandCount, newStack.CommandCount);
		Assert.AreEqual(stack.CurrentPosition, newStack.CurrentPosition);
		Assert.HasCount(stack.SaveBoundaries.Count, newStack.SaveBoundaries);
		Assert.AreEqual(stack.HasUnsavedChanges, newStack.HasUnsavedChanges);
	}

	[TestMethod]
	public async Task UndoRedoService_SaveLoadState_PreservesCommandMetadata()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		stack.SetSerializer(new JsonUndoRedoSerializer());

		Dictionary<string, object> customData = new() { ["author"] = "alice" };
		stack.Execute(new DelegateCommand("Big edit", () => { }, () => { }, ChangeType.Insert, ["doc"], size: 42, customData: customData));
		ChangeMetadata original = stack.Commands[0].Metadata;

		// Act
		byte[] data = await stack.SaveStateAsync().ConfigureAwait(false);

		UndoRedoService newStack = CreateService();
		newStack.SetSerializer(new JsonUndoRedoSerializer());
		bool success = await newStack.LoadStateAsync(data).ConfigureAwait(false);

		// Assert
		Assert.IsTrue(success);
		ChangeMetadata loaded = newStack.Commands[0].Metadata;
		Assert.AreEqual(original.Timestamp, loaded.Timestamp, "The timestamp must be when the change was made, not when it was loaded");
		Assert.AreEqual(42, loaded.Size);
		Assert.AreEqual(ChangeType.Insert, loaded.ChangeType);
		Assert.HasCount(1, loaded.AffectedItems);
		Assert.AreEqual("doc", loaded.AffectedItems[0]);
		Assert.IsNotNull(loaded.CustomData, "CustomData must survive a save and load");
		Assert.AreEqual("alice", loaded.CustomData["author"].ToString());
	}

	[TestMethod]
	public async Task UndoRedoService_NoSerializer_ThrowsInvalidOperationException()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		stack.Execute(new DelegateCommand("Test", () => { }, () => { }));

		// Act & Assert
		await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
			await stack.SaveStateAsync().ConfigureAwait(false)).ConfigureAwait(false);
	}

	[TestMethod]
	public void UndoRedoService_GetCurrentState_ReturnsCorrectState()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		stack.Execute(new DelegateCommand("Command 1", () => { }, () => { }));
		stack.MarkAsSaved("Save point");
		stack.Execute(new DelegateCommand("Command 2", () => { }, () => { }));

		// Act
		UndoRedoStackState state = stack.GetCurrentState();

		// Assert
		Assert.AreEqual(2, state.CommandCount);
		Assert.AreEqual(1, state.CurrentPosition); // Fixed: position is 0-based, after 2 commands it should be 1
		Assert.HasCount(1, state.SaveBoundaries);
		Assert.IsFalse(state.IsEmpty, "State should not be empty when commands have been executed");
		Assert.IsTrue(state.CanUndo, "State should indicate CanUndo when commands have been executed");
		Assert.IsFalse(state.CanRedo, "State should indicate CanRedo is false when at end of command stack");
	}

	[TestMethod]
	public void UndoRedoService_GetCurrentState_CanUndoMatchesService_AfterFirstCommand()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		stack.Execute(new DelegateCommand("Command 1", () => { }, () => { }));

		// Act
		UndoRedoStackState state = stack.GetCurrentState();

		// Assert
		Assert.AreEqual(0, state.CurrentPosition);
		Assert.IsTrue(stack.CanUndo, "The service can undo the command it just executed");
		Assert.AreEqual(stack.CanUndo, state.CanUndo, "State.CanUndo must agree with the service after the first command");
	}

	[TestMethod]
	public void UndoRedoService_GetCurrentState_CanUndoMatchesService_AtEveryReachablePosition()
	{
		// Arrange
		UndoRedoService stack = CreateService();

		// Assert: nothing executed yet
		Assert.AreEqual(stack.CanUndo, stack.GetCurrentState().CanUndo, "State.CanUndo must agree with the service on an empty stack");

		for (int i = 1; i <= 3; i++)
		{
			stack.Execute(new DelegateCommand($"Command {i}", () => { }, () => { }));
			Assert.AreEqual(stack.CanUndo, stack.GetCurrentState().CanUndo, $"State.CanUndo must agree with the service after executing command {i}");
			Assert.AreEqual(stack.CanRedo, stack.GetCurrentState().CanRedo, $"State.CanRedo must agree with the service after executing command {i}");
		}

		// Act & Assert: walk all the way back down, then back up
		while (stack.CanUndo)
		{
			stack.Undo();
			Assert.AreEqual(stack.CanUndo, stack.GetCurrentState().CanUndo, $"State.CanUndo must agree with the service at position {stack.CurrentPosition}");
			Assert.AreEqual(stack.CanRedo, stack.GetCurrentState().CanRedo, $"State.CanRedo must agree with the service at position {stack.CurrentPosition}");
		}

		while (stack.CanRedo)
		{
			stack.Redo();
			Assert.AreEqual(stack.CanUndo, stack.GetCurrentState().CanUndo, $"State.CanUndo must agree with the service at position {stack.CurrentPosition}");
			Assert.AreEqual(stack.CanRedo, stack.GetCurrentState().CanRedo, $"State.CanRedo must agree with the service at position {stack.CurrentPosition}");
		}
	}

	[TestMethod]
	public void UndoRedoStackState_CreateEmpty_CanUndoMatchesAFreshService()
	{
		// Arrange
		UndoRedoService stack = CreateService();

		// Act
		UndoRedoStackState state = UndoRedoStackState.CreateEmpty("test-v1.0");

		// Assert
		Assert.AreEqual(stack.CanUndo, state.CanUndo, "An empty state must report the same CanUndo as a fresh service");
		Assert.AreEqual(stack.CanRedo, state.CanRedo, "An empty state must report the same CanRedo as a fresh service");
	}

	[TestMethod]
	public void UndoRedoService_RestoreFromState_RestoresCorrectly()
	{
		// Arrange
		UndoRedoService originalStack = CreateService();
		originalStack.Execute(new DelegateCommand("Command 1", () => { }, () => { }));
		originalStack.MarkAsSaved("Save point");
		originalStack.Execute(new DelegateCommand("Command 2", () => { }, () => { }));
		originalStack.Undo();

		UndoRedoStackState state = originalStack.GetCurrentState();

		// Act
		UndoRedoService newStack = CreateService();
		bool success = newStack.RestoreFromState(state);

		// Assert
		Assert.IsTrue(success, "RestoreFromState should return true on successful restore");
		Assert.AreEqual(originalStack.CommandCount, newStack.CommandCount);
		Assert.AreEqual(originalStack.CurrentPosition, newStack.CurrentPosition);
		Assert.HasCount(originalStack.SaveBoundaries.Count, newStack.SaveBoundaries);
		Assert.AreEqual(originalStack.HasUnsavedChanges, newStack.HasUnsavedChanges);
	}

	[TestMethod]
	public async Task SerializableCommand_SerializesCorrectly()
	{
		// Arrange
		JsonUndoRedoSerializer serializer = new();
		List<ICommand> commands =
		[
			new TestSerializableCommand("Test Value")
		];

		// Act
		byte[] data = await serializer.SerializeAsync(commands, 1, [], true).ConfigureAwait(false);
		UndoRedoStackState state = await serializer.DeserializeAsync(data).ConfigureAwait(false);

		// Assert
		Assert.HasCount(1, state.Commands);
		TestSerializableCommand deserializedCommand = (TestSerializableCommand)state.Commands[0];
		Assert.AreEqual("Test Value", deserializedCommand.Value);
	}

	[TestMethod]
	public void UndoRedoStackState_CreateEmpty_CreatesCorrectState()
	{
		// Act
		UndoRedoStackState state = UndoRedoStackState.CreateEmpty("test-v1.0");

		// Assert
		Assert.IsTrue(state.IsEmpty, "Empty state should report IsEmpty as true");
		Assert.AreEqual(0, state.CommandCount);
		Assert.AreEqual(-1, state.CurrentPosition, "Empty state should use the same -1 'nothing applied' position as the stack manager");
		Assert.IsEmpty(state.SaveBoundaries);
		Assert.IsFalse(state.CanUndo, "Empty state should not allow undo");
		Assert.IsFalse(state.CanRedo, "Empty state should not allow redo");
		Assert.AreEqual("test-v1.0", state.FormatVersion);
	}

	[TestMethod]
	public void UndoRedoStackState_Properties_CalculateCorrectly()
	{
		// Arrange
		List<ICommand> commands =
		[
			new DelegateCommand("Command 1", () => { }, () => { }),
			new DelegateCommand("Command 2", () => { }, () => { }),
			new DelegateCommand("Command 3", () => { }, () => { })
		];

		List<SaveBoundary> boundaries =
		[
			new SaveBoundary(1, "Save 1")
		];

		// Act
		UndoRedoStackState state = new(commands, 2, boundaries, "test-v1.0", DateTime.UtcNow);

		// Assert
		Assert.IsFalse(state.IsEmpty, "State with commands should not be empty");
		Assert.AreEqual(3, state.CommandCount);
		Assert.AreEqual(2, state.CurrentPosition);
		Assert.IsTrue(state.CanUndo, "State should allow undo when there are commands");
		Assert.IsFalse(state.CanRedo, "State should not allow redo when at end of command stack");
	}

	[TestMethod]
	public async Task UndoRedoService_LoadStateCommandHasNoParameterlessConstructor_ReturnsFalse()
	{
		// Arrange: a command type whose only constructor takes the value it changes, which is what a
		// real ISerializableCommand implementation looks like
		UndoRedoService stack = CreateService();
		stack.SetSerializer(new JsonUndoRedoSerializer());
		stack.Execute(new ConstructorOnlySerializableCommand("saved"));

		byte[] data = await stack.SaveStateAsync().ConfigureAwait(false);

		UndoRedoService newStack = CreateService();
		newStack.SetSerializer(new JsonUndoRedoSerializer());

		// Act: LoadStateAsync reports failure rather than letting MissingMethodException escape
		bool success = await newStack.LoadStateAsync(data).ConfigureAwait(false);

		// Assert
		Assert.IsFalse(success, "LoadStateAsync should return false when a command cannot be reconstructed");
		Assert.AreEqual(0, newStack.CommandCount, "A failed load should not leave partial state on the stack");
	}

	[TestMethod]
	public async Task JsonSerializer_DeserializeCommandHasNoParameterlessConstructor_ThrowsInvalidOperationException()
	{
		// Arrange
		JsonUndoRedoSerializer serializer = new();
		ConstructorOnlySerializableCommand command = new("saved");
		byte[] data = await serializer.SerializeAsync([command], 0, [], true).ConfigureAwait(false);

		// Act & Assert: the failure is reported as part of the deserialization contract, not as the
		// raw reflection error
		InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => serializer.DeserializeAsync(data)).ConfigureAwait(false);

		Assert.Contains(nameof(ConstructorOnlySerializableCommand), ex.Message, "The message should name the type that could not be reconstructed");
		Assert.IsInstanceOfType<MissingMethodException>(ex.InnerException, "The underlying reflection failure should be preserved");
	}

	[TestMethod]
	[DataRow("""{"commands":[{"type":"x","description":"d"}],"currentPosition":0,"saveBoundaries":[],"formatVersion":"json-v1.0"}""", DisplayName = "command without metadata")]
	[DataRow("""{"commands":null,"currentPosition":0,"saveBoundaries":[],"formatVersion":"json-v1.0"}""", DisplayName = "null commands")]
	[DataRow("""{"commands":[null],"currentPosition":0,"saveBoundaries":[],"formatVersion":"json-v1.0"}""", DisplayName = "null command entry")]
	[DataRow("""{"commands":[],"currentPosition":-1,"saveBoundaries":null,"formatVersion":"json-v1.0"}""", DisplayName = "null save boundaries")]
	[DataRow("""{"commands":[],"currentPosition":-1,"saveBoundaries":[null],"formatVersion":"json-v1.0"}""", DisplayName = "null save boundary entry")]
	[DataRow("""{"commands":[],"currentPosition":-1,"saveBoundaries":[],"formatVersion":null}""", DisplayName = "null format version")]
	[DataRow("""{"commands":[{"type":"x","description":"d","metadata":{"changeType":"Modify","timestamp":"2026-01-01T00:00:00+00:00","size":1}}],"currentPosition":0,"saveBoundaries":[],"formatVersion":"json-v1.0"}""", DisplayName = "metadata without affected items")]
	public async Task UndoRedoService_LoadStateMalformed_ReturnsFalseAndKeepsHistory(string json)
	{
		// Arrange: a stack with history the user would lose if a bad load cleared it
		UndoRedoService stack = CreateService();
		stack.SetSerializer(new JsonUndoRedoSerializer());
		int value = 0;
		stack.Execute(new DelegateCommand("A", () => value = 1, () => value = 0));
		stack.Execute(new DelegateCommand("B", () => value = 2, () => value = 1));
		stack.MarkAsSaved("after B");
		await stack.UndoAsync().ConfigureAwait(false);

		// Act
		bool success = await stack.LoadStateAsync(System.Text.Encoding.UTF8.GetBytes(json)).ConfigureAwait(false);

		// Assert
		Assert.IsFalse(success, "LoadStateAsync should return false for data it cannot load");
		Assert.AreEqual(2, stack.CommandCount, "A failed load should keep the existing commands");
		Assert.AreEqual(0, stack.CurrentPosition, "A failed load should keep the existing position");
		Assert.HasCount(1, stack.SaveBoundaries, "A failed load should keep the existing save boundaries");
		Assert.AreEqual(1, value);
	}

	[TestMethod]
	public async Task UndoRedoService_SaveLoadCyclesOfPlaceholder_KeepDescriptionStable()
	{
		// Arrange: a DelegateCommand cannot be rebuilt, so it loads as a placeholder
		UndoRedoService stack = CreateService();
		stack.SetSerializer(new JsonUndoRedoSerializer());
		stack.Execute(new DelegateCommand("Type hello", () => { }, () => { }));

		// Act & Assert: every cycle shows one prefix, rather than adding another
		for (int cycle = 0; cycle < 3; cycle++)
		{
			bool success = await stack.LoadStateAsync(await stack.SaveStateAsync().ConfigureAwait(false)).ConfigureAwait(false);
			Assert.IsTrue(success);
			Assert.AreEqual("[Placeholder] Type hello", stack.Commands[0].Description, $"Cycle {cycle + 1}");
		}
	}

	[TestMethod]
	public async Task UndoRedoService_SaveAfterLoadingUnresolvableType_WritesOriginalTypeDescriptionAndData()
	{
		// Arrange: a command whose type is not loaded, for example because its plugin is missing
		const string json = """{"commands":[{"type":"My.Plugin.SetTextCommand","description":"Set text","navigationContext":"line:3","data":"hello","metadata":{"changeType":"Modify","affectedItems":["doc"],"timestamp":"2026-01-01T00:00:00+00:00","size":4}}],"currentPosition":0,"saveBoundaries":[],"formatVersion":"json-v1.0","timestamp":"2026-01-01T00:00:00Z"}""";
		UndoRedoService stack = CreateService();
		stack.SetSerializer(new JsonUndoRedoSerializer());
		Assert.IsTrue(await stack.LoadStateAsync(System.Text.Encoding.UTF8.GetBytes(json)).ConfigureAwait(false));

		// Act
		byte[] saved = await stack.SaveStateAsync().ConfigureAwait(false);

		// Assert: the save writes back what was loaded, so the command can be rebuilt once the plugin is back
		using JsonDocument document = JsonDocument.Parse(saved);
		JsonElement command = document.RootElement.GetProperty("commands")[0];
		Assert.AreEqual("My.Plugin.SetTextCommand", command.GetProperty("type").GetString());
		Assert.AreEqual("Set text", command.GetProperty("description").GetString());
		Assert.AreEqual("hello", command.GetProperty("data").GetString());
		Assert.AreEqual("line:3", command.GetProperty("navigationContext").GetString());
		Assert.AreEqual(4, command.GetProperty("metadata").GetProperty("size").GetInt32());
	}

	[TestMethod]
	[DataRow(2, DisplayName = "position past the last command")]
	[DataRow(-2, DisplayName = "position before the start")]
	public void UndoRedoService_RestoreFromStateInvalidPosition_ReturnsFalseAndKeepsHistory(int position)
	{
		// Arrange
		UndoRedoService stack = CreateService();
		stack.Execute(new DelegateCommand("A", () => { }, () => { }));
		stack.MarkAsSaved();

		UndoRedoStackState state = new(
			[new DelegateCommand("X", () => { }, () => { }), new DelegateCommand("Y", () => { }, () => { })],
			position,
			[],
			"1.0",
			DateTime.UtcNow);

		// Act
		bool success = stack.RestoreFromState(state);

		// Assert
		Assert.IsFalse(success, "RestoreFromState should reject a position outside the commands");
		Assert.AreEqual(1, stack.CommandCount, "A failed restore should keep the existing commands");
		Assert.AreEqual(0, stack.CurrentPosition, "A failed restore should keep the existing position");
		Assert.HasCount(1, stack.SaveBoundaries, "A failed restore should keep the existing save boundaries");
	}

	[TestMethod]
	public void UndoRedoService_RestoreFromStateNullSaveBoundaries_ReturnsFalseAndKeepsHistory()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		stack.Execute(new DelegateCommand("A", () => { }, () => { }));
		stack.MarkAsSaved();

		UndoRedoStackState state = new([], -1, null!, "1.0", DateTime.UtcNow);

		// Act
		bool success = stack.RestoreFromState(state);

		// Assert
		Assert.IsFalse(success, "RestoreFromState should reject a state with no save boundaries list");
		Assert.AreEqual(1, stack.CommandCount, "A failed restore should keep the existing commands");
		Assert.HasCount(1, stack.SaveBoundaries, "A failed restore should keep the existing save boundaries");
	}

	[TestMethod]
	[DataRow(-7, DisplayName = "boundary before the start")]
	[DataRow(-2, DisplayName = "boundary one before the start")]
	[DataRow(2, DisplayName = "boundary at the command count")]
	[DataRow(42, DisplayName = "boundary far past the last command")]
	public void UndoRedoService_RestoreFromStateInvalidBoundaryPosition_ReturnsFalseAndKeepsHistory(int boundaryPosition)
	{
		// Arrange
		UndoRedoService stack = CreateService();
		stack.Execute(new DelegateCommand("A", () => { }, () => { }));
		stack.MarkAsSaved();

		UndoRedoStackState state = new(
			[new DelegateCommand("X", () => { }, () => { }), new DelegateCommand("Y", () => { }, () => { })],
			1,
			[new SaveBoundary(boundaryPosition)],
			"1.0",
			DateTime.UtcNow);

		// Act
		bool success = stack.RestoreFromState(state);

		// Assert
		Assert.IsFalse(success, "RestoreFromState should reject a save boundary outside the commands");
		Assert.AreEqual(1, stack.CommandCount, "A failed restore should keep the existing commands");
		Assert.AreEqual(0, stack.CurrentPosition, "A failed restore should keep the existing position");
		Assert.HasCount(1, stack.SaveBoundaries, "A failed restore should keep the existing save boundaries");
		Assert.AreEqual(0, stack.SaveBoundaries[0].Position, "A failed restore should keep the existing save boundary position");
	}

	[TestMethod]
	[DataRow(-1, DisplayName = "boundary at the initial position")]
	[DataRow(1, DisplayName = "boundary at the last command")]
	public void UndoRedoService_RestoreFromStateBoundaryAtEdge_Restores(int boundaryPosition)
	{
		// Arrange
		UndoRedoService stack = CreateService();
		UndoRedoStackState state = new(
			[new DelegateCommand("X", () => { }, () => { }), new DelegateCommand("Y", () => { }, () => { })],
			1,
			[new SaveBoundary(boundaryPosition)],
			"1.0",
			DateTime.UtcNow);

		// Act
		bool success = stack.RestoreFromState(state);

		// Assert
		Assert.IsTrue(success, "RestoreFromState should accept a save boundary at -1 or at the last command");
		Assert.HasCount(1, stack.SaveBoundaries);
		Assert.AreEqual(boundaryPosition, stack.SaveBoundaries[0].Position);
	}

	[TestMethod]
	public async Task UndoRedoService_LoadStateAsyncInvalidBoundaryPosition_ReturnsFalseAndKeepsHistory()
	{
		// Arrange
		JsonUndoRedoSerializer serializer = new();
		byte[] data = await serializer.SerializeAsync(
			[new TestSerializableCommand("X")],
			0,
			[new SaveBoundary(-7), new SaveBoundary(42)],
			false).ConfigureAwait(false);

		UndoRedoService stack = CreateService();
		stack.SetSerializer(new JsonUndoRedoSerializer());
		stack.Execute(new DelegateCommand("A", () => { }, () => { }));
		stack.MarkAsSaved();

		// Act
		bool success = await stack.LoadStateAsync(data).ConfigureAwait(false);

		// Assert
		Assert.IsFalse(success, "LoadStateAsync should reject save boundaries outside the commands");
		Assert.AreEqual(1, stack.CommandCount, "A failed load should keep the existing commands");
		Assert.HasCount(1, stack.SaveBoundaries, "A failed load should keep the existing save boundaries");
		Assert.AreEqual(0, stack.SaveBoundaries[0].Position, "A failed load should keep the existing save boundary position");
	}

	[TestMethod]
	public async Task UndoRedoService_SaveLoadState_ReconstructsCommandWithEmptyData()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		stack.SetSerializer(new JsonUndoRedoSerializer());
		stack.Execute(new EmptyDataSerializableCommand());

		// Act
		byte[] data = await stack.SaveStateAsync().ConfigureAwait(false);

		UndoRedoService newStack = CreateService();
		newStack.SetSerializer(new JsonUndoRedoSerializer());
		bool success = await newStack.LoadStateAsync(data).ConfigureAwait(false);

		// Assert: SerializeData() returning "" is a normal result for a command with no parameters,
		// so the command is rebuilt rather than replaced with an un-undoable placeholder
		Assert.IsTrue(success);
		Assert.IsInstanceOfType<EmptyDataSerializableCommand>(newStack.Commands[0]);
		Assert.IsTrue(await newStack.UndoAsync().ConfigureAwait(false));
		Assert.AreEqual(-1, newStack.CurrentPosition);
	}

	/// <summary>
	/// Builds the #83 repro: with room for two commands, three are executed, so the first is trimmed,
	/// then both remaining are undone. Position -1 now holds the first command's never-saved result.
	/// </summary>
	private static UndoRedoService CreateDirtyAtStartAfterTrimming()
	{
		UndoRedoService stack = new(new StackManager(), new SaveBoundaryManager(), new CommandMerger(), UndoRedoOptions.Create(maxStackSize: 2));
		stack.SetSerializer(new JsonUndoRedoSerializer());
		stack.Execute(new TestSerializableCommand("1"));
		stack.Execute(new TestSerializableCommand("2"));
		stack.Execute(new TestSerializableCommand("3"));
		stack.Undo();
		stack.Undo();

		Assert.AreEqual(-1, stack.CurrentPosition);
		Assert.IsEmpty(stack.SaveBoundaries);
		Assert.IsTrue(stack.HasUnsavedChanges, "The trimmed command's result at -1 was never saved");
		return stack;
	}

	[TestMethod]
	public void UndoRedoService_RestoreFromState_KeepsUnsavedChangesAtStartAfterTrimming()
	{
		// Arrange
		UndoRedoService stack = CreateDirtyAtStartAfterTrimming();
		UndoRedoStackState state = stack.GetCurrentState();

		// Act
		UndoRedoService restored = CreateService();
		bool success = restored.RestoreFromState(state);

		// Assert
		Assert.IsTrue(success);
		Assert.IsFalse(state.InitialStateIsClean);
		Assert.AreEqual(-1, restored.CurrentPosition);
		Assert.IsTrue(restored.HasUnsavedChanges, "Restoring must not make the dirty state at -1 look saved");
	}

	[TestMethod]
	public void UndoRedoService_RestoreFromOwnState_KeepsUnsavedChangesAtStartAfterTrimming()
	{
		// Arrange
		UndoRedoService stack = CreateDirtyAtStartAfterTrimming();

		// Act
		bool success = stack.RestoreFromState(stack.GetCurrentState());

		// Assert
		Assert.IsTrue(success);
		Assert.IsTrue(stack.HasUnsavedChanges, "Restoring its own state must not make the dirty state at -1 look saved");
	}

	[TestMethod]
	public async Task UndoRedoService_SaveLoadState_KeepsUnsavedChangesAtStartAfterTrimming()
	{
		// Arrange
		UndoRedoService stack = CreateDirtyAtStartAfterTrimming();
		byte[] data = await stack.SaveStateAsync().ConfigureAwait(false);

		UndoRedoService reloaded = CreateService();
		reloaded.SetSerializer(new JsonUndoRedoSerializer());

		// Act
		bool success = await reloaded.LoadStateAsync(data).ConfigureAwait(false);

		// Assert
		Assert.IsTrue(success);
		Assert.AreEqual(-1, reloaded.CurrentPosition);
		Assert.IsTrue(reloaded.HasUnsavedChanges, "A JSON round trip must not make the dirty state at -1 look saved");
	}

	[TestMethod]
	public async Task UndoRedoService_SaveLoadState_KeepsCleanInitialState()
	{
		// Arrange: nothing trimmed or saved, so -1 is still the clean initial state
		UndoRedoService stack = CreateService();
		stack.SetSerializer(new JsonUndoRedoSerializer());
		stack.Execute(new TestSerializableCommand("1"));
		await stack.UndoAsync().ConfigureAwait(false);
		Assert.IsFalse(stack.HasUnsavedChanges);
		byte[] data = await stack.SaveStateAsync().ConfigureAwait(false);

		UndoRedoService reloaded = CreateService();
		reloaded.SetSerializer(new JsonUndoRedoSerializer());

		// Act
		bool success = await reloaded.LoadStateAsync(data).ConfigureAwait(false);

		// Assert
		Assert.IsTrue(success);
		Assert.IsFalse(reloaded.HasUnsavedChanges);
	}

	[TestMethod]
	public async Task UndoRedoService_LoadStateSavedBeforeInitialStateFlag_TreatsInitialStateAsClean()
	{
		// Arrange: data written before the flag existed has no initialStateIsClean field
		UndoRedoService stack = CreateDirtyAtStartAfterTrimming();
		byte[] data = await stack.SaveStateAsync().ConfigureAwait(false);
		System.Text.Json.Nodes.JsonObject root = System.Text.Json.Nodes.JsonNode.Parse(data)!.AsObject();
		Assert.IsTrue(root.Remove("initialStateIsClean"), "The flag should be written as initialStateIsClean");
		data = System.Text.Encoding.UTF8.GetBytes(root.ToJsonString());

		UndoRedoService reloaded = CreateService();
		reloaded.SetSerializer(new JsonUndoRedoSerializer());

		// Act
		bool success = await reloaded.LoadStateAsync(data).ConfigureAwait(false);

		// Assert: old data still loads, with the meaning it had when it was written
		Assert.IsTrue(success);
		Assert.AreEqual(-1, reloaded.CurrentPosition);
		Assert.IsFalse(reloaded.HasUnsavedChanges);
	}

	[TestMethod]
	public async Task JsonSerializer_SerializeDeserialize_PreservesInitialStateIsClean()
	{
		// Arrange
		JsonUndoRedoSerializer serializer = new();

		// Act
		UndoRedoStackState dirty = await serializer.DeserializeAsync(
			await serializer.SerializeAsync([new TestSerializableCommand("X")], -1, [], false).ConfigureAwait(false)).ConfigureAwait(false);
		UndoRedoStackState clean = await serializer.DeserializeAsync(
			await serializer.SerializeAsync([new TestSerializableCommand("X")], -1, [], true).ConfigureAwait(false)).ConfigureAwait(false);

		// Assert
		Assert.IsFalse(dirty.InitialStateIsClean);
		Assert.IsTrue(clean.InitialStateIsClean);
	}

	private static readonly DateTimeOffset SavedAt = new(2020, 1, 2, 3, 4, 5, TimeSpan.FromHours(10));

	[TestMethod]
	public async Task JsonSerializer_SerializeDeserialize_PreservesSaveBoundaryTimestamp()
	{
		// Arrange
		JsonUndoRedoSerializer serializer = new();
		byte[] data = await serializer.SerializeAsync(
			[new TestSerializableCommand("X")],
			0,
			[new SaveBoundary(0, "Saved", SavedAt)],
			false).ConfigureAwait(false);

		// Act
		UndoRedoStackState state = await serializer.DeserializeAsync(data).ConfigureAwait(false);

		// Assert
		Assert.AreEqual(SavedAt, state.SaveBoundaries[0].Timestamp, "The timestamp must be when the save was made, not when it was loaded");
	}

	[TestMethod]
	public async Task UndoRedoService_SaveLoadState_PreservesSaveBoundaryTimestamp()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		stack.SetSerializer(new JsonUndoRedoSerializer());
		stack.Execute(new TestSerializableCommand("X"));
		stack.MarkAsSaved("Saved");
		DateTimeOffset savedAt = stack.SaveBoundaries[0].Timestamp;
		byte[] data = await stack.SaveStateAsync().ConfigureAwait(false);

		// Rewrite the saved timestamp to a fixed past time, so a load that restamps it cannot match by chance
		System.Text.Json.Nodes.JsonNode root = System.Text.Json.Nodes.JsonNode.Parse(data)!;
		System.Text.Json.Nodes.JsonObject boundary = root["saveBoundaries"]![0]!.AsObject();
		Assert.AreEqual(savedAt, boundary["timestamp"]!.GetValue<DateTimeOffset>(), "The save boundary timestamp should be written");
		boundary["timestamp"] = SavedAt;
		data = System.Text.Encoding.UTF8.GetBytes(root.ToJsonString());

		UndoRedoService reloaded = CreateService();
		reloaded.SetSerializer(new JsonUndoRedoSerializer());

		// Act
		bool success = await reloaded.LoadStateAsync(data).ConfigureAwait(false);

		// Assert
		Assert.IsTrue(success);
		Assert.AreEqual(SavedAt, reloaded.SaveBoundaries[0].Timestamp);
		Assert.IsFalse(reloaded.HasUnsavedChanges, "The restored boundary should still mark the saved position");
	}

	[TestMethod]
	public async Task JsonSerializer_DeserializeSaveBoundaryWithoutTimestamp_UsesLoadTime()
	{
		// Arrange: data written before save boundary timestamps were read back may not carry one
		JsonUndoRedoSerializer serializer = new();
		byte[] data = System.Text.Encoding.UTF8.GetBytes(
			"""{"commands":[],"currentPosition":-1,"saveBoundaries":[{"position":-1,"description":"d"}],"formatVersion":"json-v1.0"}""");
		DateTimeOffset before = DateTimeOffset.Now;

		// Act
		UndoRedoStackState state = await serializer.DeserializeAsync(data).ConfigureAwait(false);

		// Assert
		Assert.AreEqual("d", state.SaveBoundaries[0].Description);
		Assert.IsGreaterThanOrEqualTo(before, state.SaveBoundaries[0].Timestamp);
	}

	[TestMethod]
	public void UndoRedoService_RestoreFromState_PreservesSaveBoundaryTimestamp()
	{
		// Arrange
		UndoRedoStackState state = new(
			[new TestSerializableCommand("X")],
			0,
			[new SaveBoundary(0, "Saved", SavedAt)],
			"1.0",
			DateTime.UtcNow);
		UndoRedoService stack = CreateService();

		// Act
		bool success = stack.RestoreFromState(state);

		// Assert
		Assert.IsTrue(success);
		Assert.AreEqual(SavedAt, stack.SaveBoundaries[0].Timestamp);
		Assert.AreEqual("Saved", stack.SaveBoundaries[0].Description);
		Assert.IsFalse(stack.HasUnsavedChanges);
	}

	private const string MalformedAssemblyName = "malformed assembly name";
	private const string InvalidVersion = "invalid assembly version";
	private const string NotACommand = "serializable type that is not a command";
	private const string DataParseFailure = "command data its parser rejects";
	private const string ThrowingConstructor = "command whose constructor throws";
	private const string OpenGeneric = "open generic command type";

	private static async Task<byte[]> SerializeWithCommandTypeAsync(string caseName)
	{
		string type = caseName switch
		{
			MalformedAssemblyName => "Foo, =bad",
			InvalidVersion => "Foo, Bar, Version=abc",
			NotACommand => typeof(SerializableNonCommand).AssemblyQualifiedName!,
			DataParseFailure => typeof(IntParsingSerializableCommand).AssemblyQualifiedName!,
			ThrowingConstructor => typeof(ThrowingConstructorSerializableCommand).AssemblyQualifiedName!,
			OpenGeneric => typeof(GenericSerializableCommand<>).AssemblyQualifiedName!,
			_ => throw new ArgumentOutOfRangeException(nameof(caseName)),
		};

		JsonUndoRedoSerializer serializer = new();
		byte[] data = await serializer.SerializeAsync([new TestSerializableCommand("saved")], 0, [], true).ConfigureAwait(false);
		System.Text.Json.Nodes.JsonNode root = System.Text.Json.Nodes.JsonNode.Parse(data)!;
		System.Text.Json.Nodes.JsonObject command = root["commands"]![0]!.AsObject();
		string typeKey = command.Single(p => p.Key.Equals("type", StringComparison.OrdinalIgnoreCase)).Key;
		string dataKey = command.Single(p => p.Key.Equals("data", StringComparison.OrdinalIgnoreCase)).Key;
		command[typeKey] = type;
		command[dataKey] = "abc";
		return System.Text.Encoding.UTF8.GetBytes(root.ToJsonString());
	}

	[TestMethod]
	[DataRow(MalformedAssemblyName)]
	[DataRow(InvalidVersion)]
	[DataRow(NotACommand)]
	[DataRow(DataParseFailure)]
	[DataRow(ThrowingConstructor)]
	[DataRow(OpenGeneric)]
	public async Task JsonSerializer_DeserializeUnloadableCommand_ThrowsInvalidOperationException(string caseName)
	{
		// Arrange
		byte[] data = await SerializeWithCommandTypeAsync(caseName).ConfigureAwait(false);
		JsonUndoRedoSerializer serializer = new();

		// Act & Assert: every way a command can fail to load is reported through the deserialization
		// contract rather than as the raw reflection, cast or parse error
		await Assert.ThrowsExactlyAsync<InvalidOperationException>(
			() => serializer.DeserializeAsync(data)).ConfigureAwait(false);
	}

	[TestMethod]
	[DataRow(MalformedAssemblyName)]
	[DataRow(InvalidVersion)]
	[DataRow(NotACommand)]
	[DataRow(DataParseFailure)]
	[DataRow(ThrowingConstructor)]
	[DataRow(OpenGeneric)]
	public async Task UndoRedoService_LoadStateUnloadableCommand_ReturnsFalseAndKeepsHistory(string caseName)
	{
		// Arrange
		byte[] data = await SerializeWithCommandTypeAsync(caseName).ConfigureAwait(false);
		UndoRedoService stack = CreateService();
		stack.SetSerializer(new JsonUndoRedoSerializer());
		stack.Execute(new DelegateCommand("A", () => { }, () => { }));

		// Act
		bool success = await stack.LoadStateAsync(data).ConfigureAwait(false);

		// Assert
		Assert.IsFalse(success, "LoadStateAsync should return false when a command cannot be loaded");
		Assert.AreEqual(1, stack.CommandCount, "A failed load should keep the existing commands");
		Assert.AreEqual("A", stack.Commands[0].Description);
	}

#pragma warning disable CA1812 // Instantiated by reflection during deserialization
	[TestMethod]
	public async Task UndoRedoService_LoadStateSerializableCommand_RestoresNavigationContextAndMetadata()
	{
		// Arrange
		Dictionary<string, object> customData = new() { ["origin"] = "keyboard" };
		NavigatingSerializableCommand original = new("hello", "line:42", 7, customData);

		UndoRedoService stack = CreateService();
		stack.SetSerializer(new JsonUndoRedoSerializer());
		stack.Execute(original);
		byte[] data = await stack.SaveStateAsync().ConfigureAwait(false);

		RecordingNavigationProvider navigation = new();
		UndoRedoService newStack = new(new StackManager(), new SaveBoundaryManager(), new CommandMerger(), navigationProvider: navigation);
		newStack.SetSerializer(new JsonUndoRedoSerializer());

		// Act
		bool success = await newStack.LoadStateAsync(data).ConfigureAwait(false);

		// Assert: the reloaded command is the real type, with the state its parameterless constructor cannot know
		Assert.IsTrue(success);
		NavigatingSerializableCommand reloaded = (NavigatingSerializableCommand)newStack.Commands.Single();
		Assert.AreEqual("hello", reloaded.Text);
		Assert.AreEqual("line:42", reloaded.NavigationContext);
		Assert.AreEqual(original.Metadata.ChangeType, reloaded.Metadata.ChangeType);
		CollectionAssert.AreEqual(original.Metadata.AffectedItems.ToList(), reloaded.Metadata.AffectedItems.ToList());
		Assert.AreEqual(7, reloaded.Metadata.Size);
		Assert.AreEqual(original.Metadata.Timestamp, reloaded.Metadata.Timestamp);
		Assert.IsNotNull(reloaded.Metadata.CustomData);
		Assert.AreEqual("keyboard", reloaded.Metadata.CustomData["origin"].ToString());

		// Undo still navigates to where the change was made
		bool undone = await newStack.UndoAsync().ConfigureAwait(false);
		Assert.IsTrue(undone);
		Assert.AreEqual("line:42", navigation.LastNavigatedContext);
	}

	private sealed class SerializableNonCommand : ISerializableCommand
	{
		public string SerializeData() => string.Empty;

		public void DeserializeData(string data)
		{
			// Nothing to restore
		}
	}
#pragma warning restore CA1812

#pragma warning disable CA1812 // Instantiated by reflection during deserialization
	private sealed class IntParsingSerializableCommand : BaseCommand, ISerializableCommand
	{
		public IntParsingSerializableCommand() : base(ChangeType.Modify, ["test"])
		{
		}

		public int Value { get; private set; }

		public override string Description => $"Int command with value: {Value}";

		public override void Execute()
		{
			// Test implementation
		}

		public override void Undo()
		{
			// Test implementation
		}

		public string SerializeData() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

		public void DeserializeData(string data) => Value = int.Parse(data, System.Globalization.CultureInfo.InvariantCulture);
	}
#pragma warning restore CA1812

#pragma warning disable CA1812 // Instantiated by reflection during deserialization
	private sealed class ThrowingConstructorSerializableCommand : BaseCommand, ISerializableCommand
	{
		public ThrowingConstructorSerializableCommand() : base(ChangeType.Modify, ["test"]) =>
			throw new InvalidDataException("Needs context the loader cannot supply");

		public override string Description => "Throwing constructor";

		public override void Execute()
		{
			// Test implementation
		}

		public override void Undo()
		{
			// Test implementation
		}

		public string SerializeData() => string.Empty;

		public void DeserializeData(string data)
		{
			// Nothing to restore
		}
	}

	private sealed class GenericSerializableCommand<T> : BaseCommand, ISerializableCommand
	{
		public GenericSerializableCommand() : base(ChangeType.Modify, ["test"])
		{
		}

		public override string Description => $"Generic command of {typeof(T).Name}";

		public override void Execute()
		{
			// Test implementation
		}

		public override void Undo()
		{
			// Test implementation
		}

		public string SerializeData() => string.Empty;

		public void DeserializeData(string data)
		{
			// Nothing to restore
		}
	}
#pragma warning restore CA1812

	private sealed class EmptyDataSerializableCommand : BaseCommand, ISerializableCommand
	{
		public EmptyDataSerializableCommand() : base(ChangeType.Modify, ["test"])
		{
		}

		public override string Description => "Clear all";

		public override void Execute()
		{
			// Test implementation
		}

		public override void Undo()
		{
			// Test implementation
		}

		public string SerializeData() => string.Empty;

		public void DeserializeData(string data)
		{
			// Nothing to restore
		}
	}

	private sealed class ConstructorOnlySerializableCommand(string value)
		: BaseCommand(ChangeType.Modify, ["test"]), ISerializableCommand
	{
		public string Value { get; private set; } = value;

		public override string Description => $"Constructor-only command with value: {Value}";

		public override void Execute()
		{
			// Test implementation
		}

		public override void Undo()
		{
			// Test implementation
		}

		public string SerializeData() => JsonSerializer.Serialize(new { Value });

		public void DeserializeData(string data)
		{
			JsonElement element = JsonSerializer.Deserialize<JsonElement>(data);
			Value = element.GetProperty(nameof(Value)).GetString() ?? string.Empty;
		}
	}

	private sealed class TestSerializableCommand : BaseCommand, ISerializableCommand
	{
		public string Value { get; private set; } = string.Empty;

		public TestSerializableCommand() : base(ChangeType.Modify, ["test"])
		{
		}

		public TestSerializableCommand(string value) : base(ChangeType.Modify, ["test"]) => Value = value;

		public override string Description => $"Test command with value: {Value}";

		public override void Execute()
		{
			// Test implementation
		}

		public override void Undo()
		{
			// Test implementation
		}

		public string SerializeData()
		{
			return JsonSerializer.Serialize(new { Value });
		}

		public void DeserializeData(string data)
		{
			dynamic? obj = JsonSerializer.Deserialize<dynamic>(data);
			JsonElement element = JsonSerializer.Deserialize<JsonElement>(data);
			Value = element.GetProperty(nameof(Value)).GetString() ?? string.Empty;
		}
	}

	private sealed class RecordingNavigationProvider : INavigationProvider
	{
		public string? LastNavigatedContext { get; private set; }

		public Task<bool> NavigateToAsync(string context, CancellationToken cancellationToken = default)
		{
			LastNavigatedContext = context;
			return Task.FromResult(true);
		}

		public bool IsValidContext(string context) => true;
	}

	private sealed class NavigatingSerializableCommand : BaseCommand, ISerializableCommand
	{
		public string Text { get; private set; } = string.Empty;

		public NavigatingSerializableCommand() : base(ChangeType.Insert, ["doc"])
		{
		}

		public NavigatingSerializableCommand(string text, string navigationContext, int size, IReadOnlyDictionary<string, object> customData)
			: base(ChangeType.Insert, ["doc"], navigationContext, size, customData) => Text = text;

		public override string Description => $"Set {Text}";

		public override void Execute()
		{
			// Test implementation
		}

		public override void Undo()
		{
			// Test implementation
		}

		public string SerializeData() => Text;

		public void DeserializeData(string data) => Text = data;
	}
}
