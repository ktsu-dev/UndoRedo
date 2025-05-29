// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

using ktsu.UndoRedo.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
}
