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
		byte[] data = await serializer.SerializeAsync([], 0, []).ConfigureAwait(false);

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
		byte[] data = await serializer.SerializeAsync(commands, 1, boundaries).ConfigureAwait(false);
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
		byte[] data = await serializer.SerializeAsync(commands, 1, []).ConfigureAwait(false);
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
		byte[] data = await serializer.SerializeAsync([command], 0, []).ConfigureAwait(false);

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
}
