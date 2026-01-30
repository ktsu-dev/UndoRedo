// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.UndoRedo.Test;

using System.Text.Json;
using ktsu.UndoRedo.Core;
using ktsu.UndoRedo.Core.Models;
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
		Assert.AreEqual(0, state.CurrentPosition);
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
