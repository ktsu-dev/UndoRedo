// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.UndoRedo.Test
{
	using ktsu.UndoRedo.Core;
	using ktsu.UndoRedo.Core.Contracts;
	using ktsu.UndoRedo.Core.Models;
	using ktsu.UndoRedo.Core.Services;
	using Microsoft.VisualStudio.TestTools.UnitTesting;
	using System;
	using System.Collections.Generic;
	using System.Text.Json;
	using System.Threading.Tasks;

	[TestClass]
	public class SerializationTests
	{
		[TestMethod]
		public async Task JsonSerializer_SerializeEmpty_ReturnsValidData()
		{
			// Arrange
			JsonUndoRedoSerializer serializer = new();
			List<ICommand> commands = new();
			List<SaveBoundary> boundaries = new();

			// Act
			byte[] data = await serializer.SerializeAsync(commands, 0, boundaries);

			// Assert
			Assert.IsNotNull(data);
			Assert.IsTrue(data.Length > 0);
		}

		[TestMethod]
		public async Task JsonSerializer_SerializeDeserialize_PreservesBasicData()
		{
			// Arrange
			JsonUndoRedoSerializer serializer = new();
			List<ICommand> commands = new()
			{
				new DelegateCommand("Command 1", () => { }, () => { }, ChangeType.Insert, new[] { "item1" }),
				new DelegateCommand("Command 2", () => { }, () => { }, ChangeType.Modify, new[] { "item2" })
			};

			List<SaveBoundary> boundaries = new()
			{
				new SaveBoundary(1, DateTime.UtcNow, "Save 1")
			};

			// Act
			byte[] data = await serializer.SerializeAsync(commands, 1, boundaries);
			UndoRedoStackState state = await serializer.DeserializeAsync(data);

			// Assert
			Assert.AreEqual(2, state.Commands.Count);
			Assert.AreEqual(1, state.CurrentPosition);
			Assert.AreEqual(1, state.SaveBoundaries.Count);
			Assert.AreEqual("json-v1.0", state.FormatVersion);
		}

		[TestMethod]
		public async Task JsonSerializer_DeserializeInvalidData_ThrowsException()
		{
			// Arrange
			JsonUndoRedoSerializer serializer = new();
			byte[] invalidData = "invalid json data"u8.ToArray();

			// Act & Assert
			await Assert.ThrowsExceptionAsync<JsonException>(async () =>
				await serializer.DeserializeAsync(invalidData));
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
			await Assert.ThrowsExceptionAsync<NotSupportedException>(async () =>
				await serializer.DeserializeAsync(data));
		}

		[TestMethod]
		public void JsonSerializer_SupportsVersion_ChecksCorrectly()
		{
			// Arrange
			JsonUndoRedoSerializer serializer = new();

			// Act & Assert
			Assert.IsTrue(serializer.SupportsVersion("json-v1.0"));
			Assert.IsTrue(serializer.SupportsVersion("json-v1.1"));
			Assert.IsFalse(serializer.SupportsVersion("xml-v1.0"));
			Assert.IsFalse(serializer.SupportsVersion("json-v2.0"));
		}

		[TestMethod]
		public async Task UndoRedoStack_SaveLoadState_PreservesStackState()
		{
			// Arrange
			UndoRedoStack stack = new();
			stack.SetSerializer(new JsonUndoRedoSerializer());

			int value = 0;
			stack.Execute(new DelegateCommand("Set 1", () => value = 1, () => value = 0));
			stack.Execute(new DelegateCommand("Set 2", () => value = 2, () => value = 1));
			stack.MarkAsSaved("Test save");
			stack.Execute(new DelegateCommand("Set 3", () => value = 3, () => value = 2));
			stack.Undo(); // Back to value = 2

			// Act
			byte[] data = await stack.SaveStateAsync();

			// Create new stack and load state
			UndoRedoStack newStack = new();
			newStack.SetSerializer(new JsonUndoRedoSerializer());
			bool success = await newStack.LoadStateAsync(data);

			// Assert
			Assert.IsTrue(success);
			Assert.AreEqual(stack.CommandCount, newStack.CommandCount);
			Assert.AreEqual(stack.CurrentPosition, newStack.CurrentPosition);
			Assert.AreEqual(stack.SaveBoundaries.Count, newStack.SaveBoundaries.Count);
			Assert.AreEqual(stack.HasUnsavedChanges, newStack.HasUnsavedChanges);
		}

		[TestMethod]
		public async Task UndoRedoStack_NoSerializer_ThrowsInvalidOperationException()
		{
			// Arrange
			UndoRedoStack stack = new();
			stack.Execute(new DelegateCommand("Test", () => { }, () => { }));

			// Act & Assert
			await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
				await stack.SaveStateAsync());
		}

		[TestMethod]
		public void UndoRedoStack_GetCurrentState_ReturnsCorrectState()
		{
			// Arrange
			UndoRedoStack stack = new();
			stack.Execute(new DelegateCommand("Command 1", () => { }, () => { }));
			stack.MarkAsSaved("Save point");
			stack.Execute(new DelegateCommand("Command 2", () => { }, () => { }));

			// Act
			UndoRedoStackState state = stack.GetCurrentState();

			// Assert
			Assert.AreEqual(2, state.CommandCount);
			Assert.AreEqual(2, state.CurrentPosition);
			Assert.AreEqual(1, state.SaveBoundaries.Count);
			Assert.IsFalse(state.IsEmpty);
			Assert.IsTrue(state.CanUndo);
			Assert.IsFalse(state.CanRedo);
		}

		[TestMethod]
		public void UndoRedoStack_RestoreFromState_RestoresCorrectly()
		{
			// Arrange
			UndoRedoStack originalStack = new();
			originalStack.Execute(new DelegateCommand("Command 1", () => { }, () => { }));
			originalStack.MarkAsSaved("Save point");
			originalStack.Execute(new DelegateCommand("Command 2", () => { }, () => { }));
			originalStack.Undo();

			UndoRedoStackState state = originalStack.GetCurrentState();

			// Act
			UndoRedoStack newStack = new();
			bool success = newStack.RestoreFromState(state);

			// Assert
			Assert.IsTrue(success);
			Assert.AreEqual(originalStack.CommandCount, newStack.CommandCount);
			Assert.AreEqual(originalStack.CurrentPosition, newStack.CurrentPosition);
			Assert.AreEqual(originalStack.SaveBoundaries.Count, newStack.SaveBoundaries.Count);
			Assert.AreEqual(originalStack.HasUnsavedChanges, newStack.HasUnsavedChanges);
		}

		[TestMethod]
		public async Task SerializableCommand_SerializesCorrectly()
		{
			// Arrange
			JsonUndoRedoSerializer serializer = new();
			List<ICommand> commands = new()
			{
				new TestSerializableCommand("Test Value")
			};

			// Act
			byte[] data = await serializer.SerializeAsync(commands, 1, new List<SaveBoundary>());
			UndoRedoStackState state = await serializer.DeserializeAsync(data);

			// Assert
			Assert.AreEqual(1, state.Commands.Count);
			TestSerializableCommand deserializedCommand = (TestSerializableCommand)state.Commands[0];
			Assert.AreEqual("Test Value", deserializedCommand.Value);
		}

		[TestMethod]
		public void UndoRedoStackState_CreateEmpty_CreatesCorrectState()
		{
			// Act
			UndoRedoStackState state = UndoRedoStackState.CreateEmpty("test-v1.0");

			// Assert
			Assert.IsTrue(state.IsEmpty);
			Assert.AreEqual(0, state.CommandCount);
			Assert.AreEqual(0, state.CurrentPosition);
			Assert.AreEqual(0, state.SaveBoundaries.Count);
			Assert.IsFalse(state.CanUndo);
			Assert.IsFalse(state.CanRedo);
			Assert.AreEqual("test-v1.0", state.FormatVersion);
		}

		[TestMethod]
		public void UndoRedoStackState_Properties_CalculateCorrectly()
		{
			// Arrange
			List<ICommand> commands = new()
			{
				new DelegateCommand("Command 1", () => { }, () => { }),
				new DelegateCommand("Command 2", () => { }, () => { }),
				new DelegateCommand("Command 3", () => { }, () => { })
			};

			// Act
			UndoRedoStackState state = new(commands, 2, new List<SaveBoundary>(), "v1.0", DateTime.UtcNow);

			// Assert
			Assert.IsFalse(state.IsEmpty);
			Assert.AreEqual(3, state.CommandCount);
			Assert.IsTrue(state.CanUndo); // Position 2 > 0
			Assert.IsTrue(state.CanRedo); // Position 2 < 3
		}

		private class TestSerializableCommand : BaseCommand, ISerializableCommand
		{
			public string Value { get; private set; } = string.Empty;

			public TestSerializableCommand() : base(ChangeType.Modify, new[] { "test" })
			{
			}

			public TestSerializableCommand(string value) : base(ChangeType.Modify, new[] { "test" })
			{
				Value = value;
			}

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
				JsonElement element = JsonSerializer.Deserialize<JsonElement>(data);
				Value = element.GetProperty("Value").GetString() ?? string.Empty;
			}
		}
	}
}
