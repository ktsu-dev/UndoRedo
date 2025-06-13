// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

using ktsu.UndoRedo.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ktsu.UndoRedo.Test;

[TestClass]
public class UndoRedoStackTests
{
	[TestMethod]
	public void Execute_SingleCommand_CanUndoAndRedo()
	{
		// Arrange
		var stack = new UndoRedoStack();
		var value = 0;
		var command = new DelegateCommand(
			"Increment",
			() => value++,
			() => value--,
			ChangeType.Modify,
			["value"]);

		// Act & Assert
		Assert.IsFalse(stack.CanUndo);
		Assert.IsFalse(stack.CanRedo);

		stack.Execute(command);
		Assert.AreEqual(1, value);
		Assert.IsTrue(stack.CanUndo);
		Assert.IsFalse(stack.CanRedo);

		stack.Undo();
		Assert.AreEqual(0, value);
		Assert.IsFalse(stack.CanUndo);
		Assert.IsTrue(stack.CanRedo);

		stack.Redo();
		Assert.AreEqual(1, value);
		Assert.IsTrue(stack.CanUndo);
		Assert.IsFalse(stack.CanRedo);
	}

	[TestMethod]
	public void Execute_MultipleCommands_MaintainsCorrectOrder()
	{
		// Arrange
		var stack = new UndoRedoStack();
		var values = new List<int>();

		var command1 = new DelegateCommand("Add 1", () => values.Add(1), () => values.RemoveAt(values.Count - 1));
		var command2 = new DelegateCommand("Add 2", () => values.Add(2), () => values.RemoveAt(values.Count - 1));
		var command3 = new DelegateCommand("Add 3", () => values.Add(3), () => values.RemoveAt(values.Count - 1));

		// Act
		stack.Execute(command1);
		stack.Execute(command2);
		stack.Execute(command3);

		// Assert
		CollectionAssert.AreEqual(new[] { 1, 2, 3 }, values);

		stack.Undo();
		CollectionAssert.AreEqual(new[] { 1, 2 }, values);

		stack.Undo();
		CollectionAssert.AreEqual(new[] { 1 }, values);

		stack.Redo();
		CollectionAssert.AreEqual(new[] { 1, 2 }, values);
	}

	[TestMethod]
	public void Execute_AfterUndo_ClearsFutureCommands()
	{
		// Arrange
		var stack = new UndoRedoStack();
		var value = 0;

		var command1 = new DelegateCommand("Set to 1", () => value = 1, () => value = 0);
		var command2 = new DelegateCommand("Set to 2", () => value = 2, () => value = 1);
		var command3 = new DelegateCommand("Set to 3", () => value = 3, () => value = 2);

		// Act
		stack.Execute(command1);
		stack.Execute(command2);
		Assert.AreEqual(2, stack.CommandCount);

		stack.Undo(); // Back to value = 1
		stack.Execute(command3); // Should clear command2 and add command3

		// Assert
		Assert.AreEqual(2, stack.CommandCount);
		Assert.AreEqual(3, value);
		Assert.IsTrue(stack.CanUndo);
		Assert.IsFalse(stack.CanRedo);
	}

	[TestMethod]
	public void MarkAsSaved_TracksUnsavedChanges()
	{
		// Arrange
		var stack = new UndoRedoStack();
		var value = 0;
		var command = new DelegateCommand("Increment", () => value++, () => value--);

		// Act & Assert
		Assert.IsFalse(stack.HasUnsavedChanges);

		stack.Execute(command);
		Assert.IsTrue(stack.HasUnsavedChanges);

		stack.MarkAsSaved("Saved at 1");
		Assert.IsFalse(stack.HasUnsavedChanges);
		Assert.AreEqual(1, stack.SaveBoundaries.Count);

		stack.Execute(command);
		Assert.IsTrue(stack.HasUnsavedChanges);

		stack.Undo();
		Assert.IsFalse(stack.HasUnsavedChanges); // Back to save boundary
	}

	[TestMethod]
	public void CompositeCommand_ExecutesAndUndoesInCorrectOrder()
	{
		// Arrange
		var stack = new UndoRedoStack();
		var values = new List<string>();

		var commands = new[]
		{
			new DelegateCommand("Add A", () => values.Add("A"), () => values.RemoveAt(values.Count - 1)),
			new DelegateCommand("Add B", () => values.Add("B"), () => values.RemoveAt(values.Count - 1)),
			new DelegateCommand("Add C", () => values.Add("C"), () => values.RemoveAt(values.Count - 1))
		};

		var composite = new CompositeCommand("Add ABC", commands);

		// Act
		stack.Execute(composite);

		// Assert
		CollectionAssert.AreEqual(new[] { "A", "B", "C" }, values);

		stack.Undo();
		Assert.AreEqual(0, values.Count);

		stack.Redo();
		CollectionAssert.AreEqual(new[] { "A", "B", "C" }, values);
	}

	[TestMethod]
	public async Task NavigationProvider_CallsNavigateToOnUndoRedo()
	{
		// Arrange
		var stack = new UndoRedoStack();
		var navigationProvider = new MockNavigationProvider();
		stack.SetNavigationProvider(navigationProvider);

		var command = new DelegateCommand(
			"Test Command",
			() => { },
			() => { },
			navigationContext: "test-context");

		stack.Execute(command);

		// Act & Assert
		await stack.UndoAsync();
		Assert.AreEqual("test-context", navigationProvider.LastNavigatedContext);

		await stack.RedoAsync();
		Assert.AreEqual("test-context", navigationProvider.LastNavigatedContext);
	}

	[TestMethod]
	public void GetChangeVisualizations_ReturnsCorrectData()
	{
		// Arrange
		var stack = new UndoRedoStack();
		var command1 = new DelegateCommand("Command 1", () => { }, () => { }, ChangeType.Insert, ["item1"]);
		var command2 = new DelegateCommand("Command 2", () => { }, () => { }, ChangeType.Delete, ["item2"]);

		// Act
		stack.Execute(command1);
		stack.MarkAsSaved();
		stack.Execute(command2);

		var visualizations = stack.GetChangeVisualizations().ToList();

		// Assert
		Assert.AreEqual(2, visualizations.Count);

		Assert.AreEqual("Command 1", visualizations[0].Command.Description);
		Assert.IsTrue(visualizations[0].IsExecuted);
		Assert.IsTrue(visualizations[0].HasSaveBoundary);

		Assert.AreEqual("Command 2", visualizations[1].Command.Description);
		Assert.IsTrue(visualizations[1].IsExecuted);
		Assert.IsFalse(visualizations[1].HasSaveBoundary);
	}

	[TestMethod]
	public void Events_FiredCorrectly()
	{
		// Arrange
		var stack = new UndoRedoStack();
		var events = new List<string>();

		stack.CommandExecuted += (_, e) => events.Add($"Executed: {e.Command.Description}");
		stack.CommandUndone += (_, e) => events.Add($"Undone: {e.Command.Description}");
		stack.CommandRedone += (_, e) => events.Add($"Redone: {e.Command.Description}");
		stack.SaveBoundaryCreated += (_, e) => events.Add($"Saved: {e.SaveBoundary.Description}");

		var command = new DelegateCommand("Test", () => { }, () => { });

		// Act
		stack.Execute(command);
		stack.MarkAsSaved("Test Save");
		stack.Undo();
		stack.Redo();

		// Assert
		Assert.AreEqual(4, events.Count);
		Assert.AreEqual("Executed: Test", events[0]);
		Assert.AreEqual("Saved: Test Save", events[1]);
		Assert.AreEqual("Undone: Test", events[2]);
		Assert.AreEqual("Redone: Test", events[3]);
	}

	private class MockNavigationProvider : INavigationProvider
	{
		public string? LastNavigatedContext { get; private set; }

		public Task<bool> NavigateToAsync(string context, CancellationToken cancellationToken = default)
		{
			LastNavigatedContext = context;
			return Task.FromResult(true);
		}

		public bool IsValidContext(string context) => true;
	}

	#region Edge Cases and Complex Scenarios

	[TestMethod]
	public void Execute_MaxStackSizeReached_RemovesOldestCommands()
	{
		// Arrange
		var options = UndoRedoOptions.Create(maxStackSize: 3);
		var stack = new UndoRedoStack(options);
		var value = 0;

		// Act - Execute 5 commands when max is 3
		for (int i = 1; i <= 5; i++)
		{
			var localI = i;
			stack.Execute(new DelegateCommand($"Set to {localI}", () => value = localI, () => value = localI - 1));
		}

		// Assert - Only last 3 commands should remain
		Assert.AreEqual(3, stack.CommandCount);
		Assert.AreEqual(5, value);

		// Should be able to undo 3 times (back to value = 2)
		stack.Undo(); // 4
		stack.Undo(); // 3
		stack.Undo(); // 2
		Assert.AreEqual(2, value);
		Assert.IsFalse(stack.CanUndo); // Should not be able to undo further
	}

	[TestMethod]
	public void CommandMerging_ConsecutiveCommands_MergesCorrectly()
	{
		// Arrange
		var stack = new UndoRedoStack();
		var text = "";

		// Act - Execute mergeable commands
		var cmd1 = new TestMergeableCommand(t => text = t, "a");
		var cmd2 = new TestMergeableCommand(t => text = t, "ab");
		var cmd3 = new TestMergeableCommand(t => text = t, "abc");
		var nonMergeable = new DelegateCommand("Non-mergeable", () => { }, () => { });

		stack.Execute(cmd1);
		stack.Execute(cmd2);
		stack.Execute(cmd3);
		stack.Execute(nonMergeable);

		// Assert
		Assert.AreEqual(2, stack.CommandCount); // Merged + non-mergeable
		Assert.AreEqual("abc", text);

		stack.Undo(); // Undo non-mergeable
		stack.Undo(); // Undo merged command
		Assert.AreEqual("", text);
	}

	[TestMethod]
	public void CompositeCommand_NestedComposites_HandlesCorrectly()
	{
		// Arrange
		var stack = new UndoRedoStack();
		var values = new List<string>();

		var innerComposite1 = new CompositeCommand("Inner 1", new[]
		{
			new DelegateCommand("Add X", () => values.Add("X"), () => values.RemoveAt(values.Count - 1)),
			new DelegateCommand("Add Y", () => values.Add("Y"), () => values.RemoveAt(values.Count - 1))
		});

		var innerComposite2 = new CompositeCommand("Inner 2", new[]
		{
			new DelegateCommand("Add Z", () => values.Add("Z"), () => values.RemoveAt(values.Count - 1))
		});

		var outerComposite = new CompositeCommand("Outer", new ICommand[]
		{
			innerComposite1,
			new DelegateCommand("Add W", () => values.Add("W"), () => values.RemoveAt(values.Count - 1)),
			innerComposite2
		});

		// Act
		stack.Execute(outerComposite);

		// Assert
		CollectionAssert.AreEqual(new[] { "X", "Y", "W", "Z" }, values);
		Assert.AreEqual(1, stack.CommandCount); // Single composite command

		stack.Undo();
		Assert.AreEqual(0, values.Count);
	}

	[TestMethod]
	public void SaveBoundaries_MultipleUndoRedoOperations_MaintainsCorrectState()
	{
		// Arrange
		var stack = new UndoRedoStack();
		var value = 0;

		// Act - Complex sequence with save boundaries
		stack.Execute(new DelegateCommand("Set 1", () => value = 1, () => value = 0));
		stack.MarkAsSaved("Save 1");

		stack.Execute(new DelegateCommand("Set 2", () => value = 2, () => value = 1));
		stack.Execute(new DelegateCommand("Set 3", () => value = 3, () => value = 2));
		stack.MarkAsSaved("Save 2");

		stack.Execute(new DelegateCommand("Set 4", () => value = 4, () => value = 3));

		// Assert states
		Assert.IsTrue(stack.HasUnsavedChanges);
		Assert.AreEqual(2, stack.SaveBoundaries.Count);

		// Undo to previous save boundary
		stack.Undo(); // Back to value = 3 (at Save 2)
		Assert.IsFalse(stack.HasUnsavedChanges);

		// Undo past save boundary
		stack.Undo(); // value = 2
		Assert.IsTrue(stack.HasUnsavedChanges); // We're past the save boundary

		// Redo back to save boundary
		stack.Redo(); // value = 3
		Assert.IsFalse(stack.HasUnsavedChanges);
	}

	[TestMethod]
	public async Task NavigationProvider_CancellationToken_HandlesCorrectly()
	{
		// Arrange
		var stack = new UndoRedoStack();
		var slowNavigationProvider = new SlowNavigationProvider();
		stack.SetNavigationProvider(slowNavigationProvider);

		var command = new DelegateCommand("Test", () => { }, () => { }, navigationContext: "test");
		stack.Execute(command);

		// Act & Assert - Test cancellation
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));
		var result = await stack.UndoAsync(navigateToChange: true, cts.Token);

		Assert.IsFalse(result); // Should fail due to timeout
		Assert.IsTrue(slowNavigationProvider.WasCancelled);
	}

	[TestMethod]
	public void Execute_CommandThrowsException_DoesNotCorruptStack()
	{
		// Arrange
		var stack = new UndoRedoStack();
		var value = 0;

		var goodCommand = new DelegateCommand("Good", () => value = 1, () => value = 0);
		var badCommand = new DelegateCommand("Bad", () => throw new InvalidOperationException("Test error"), () => { });

		// Act & Assert
		stack.Execute(goodCommand);
		Assert.AreEqual(1, value);
		Assert.AreEqual(1, stack.CommandCount);

		// Bad command should throw but not corrupt stack
		Assert.ThrowsException<InvalidOperationException>(() => stack.Execute(badCommand));
		Assert.AreEqual(1, stack.CommandCount); // Stack should be unchanged
		Assert.AreEqual(1, value); // Value should be unchanged

		// Stack should still be functional
		Assert.IsTrue(stack.CanUndo);
		stack.Undo();
		Assert.AreEqual(0, value);
	}

	[TestMethod]
	public void UndoToSaveBoundary_NoSaveBoundaries_ThrowsException()
	{
		// Arrange
		var stack = new UndoRedoStack();
		var fakeBoundary = new SaveBoundary(0, DateTime.Now, "Fake");

		// Act & Assert
		Assert.ThrowsException<ArgumentException>(() =>
			stack.UndoToSaveBoundaryAsync(fakeBoundary).GetAwaiter().GetResult());
	}

	[TestMethod]
	public void GetChangeVisualizations_WithLimits_ReturnsCorrectCount()
	{
		// Arrange
		var stack = new UndoRedoStack();

		// Add 10 commands
		for (int i = 0; i < 10; i++)
		{
			stack.Execute(new DelegateCommand($"Command {i}", () => { }, () => { }));
		}

		// Act & Assert
		var allVisualizations = stack.GetChangeVisualizations().ToList();
		var limitedVisualizations = stack.GetChangeVisualizations(5).ToList();

		Assert.AreEqual(10, allVisualizations.Count);
		Assert.AreEqual(5, limitedVisualizations.Count);
	}

	[TestMethod]
	public void Clear_WithSaveBoundariesAndCommands_ClearsEverything()
	{
		// Arrange
		var stack = new UndoRedoStack();
		stack.Execute(new DelegateCommand("Test", () => { }, () => { }));
		stack.MarkAsSaved("Test save");

		// Act
		stack.Clear();

		// Assert
		Assert.AreEqual(0, stack.CommandCount);
		Assert.AreEqual(0, stack.SaveBoundaries.Count);
		Assert.IsFalse(stack.CanUndo);
		Assert.IsFalse(stack.CanRedo);
		Assert.IsFalse(stack.HasUnsavedChanges);
	}

	[TestMethod]
	public void Events_ExecutionOrder_FiresInCorrectSequence()
	{
		// Arrange
		var stack = new UndoRedoStack();
		var eventOrder = new List<string>();

		stack.CommandExecuted += (_, _) => eventOrder.Add("Executed");
		stack.CommandUndone += (_, _) => eventOrder.Add("Undone");
		stack.CommandRedone += (_, _) => eventOrder.Add("Redone");
		stack.SaveBoundaryCreated += (_, _) => eventOrder.Add("SaveBoundary");

		var command = new DelegateCommand("Test", () => { }, () => { });

		// Act
		stack.Execute(command);
		stack.MarkAsSaved();
		stack.Undo();
		stack.Redo();

		// Assert
		CollectionAssert.AreEqual(new[] { "Executed", "SaveBoundary", "Undone", "Redone" }, eventOrder);
	}

	#endregion

	#region Helper Classes for Testing

	private class TestMergeableCommand : BaseCommand
	{
		private readonly Action<string> _setter;
		private readonly string _newValue;
		private string _oldValue = "";

		public TestMergeableCommand(Action<string> setter, string newValue)
			: base(ChangeType.Modify, new[] { "text" })
		{
			_setter = setter;
			_newValue = newValue;
		}

		public override string Description => $"Set text to '{_newValue}'";

		public override void Execute()
		{
			_setter(_newValue);
		}

		public override void Undo()
		{
			_setter(_oldValue);
		}

		public override bool CanMergeWith(ICommand other)
		{
			return other is TestMergeableCommand otherCmd &&
				   otherCmd._newValue.StartsWith(_newValue);
		}

		public override ICommand MergeWith(ICommand other)
		{
			var otherCmd = (TestMergeableCommand)other;
			return new TestMergeableCommand(_setter, otherCmd._newValue);
		}
	}

	private class SlowNavigationProvider : INavigationProvider
	{
		public bool WasCancelled { get; private set; }

		public async Task<bool> NavigateToAsync(string context, CancellationToken cancellationToken = default)
		{
			try
			{
				await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
				return true;
			}
			catch (OperationCanceledException)
			{
				WasCancelled = true;
				return false;
			}
		}

		public bool IsValidContext(string context) => true;
	}

	#endregion
}
