// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.UndoRedo.Test;
using ktsu.UndoRedo.Core.Models;
using ktsu.UndoRedo.Core.Services;

[TestClass]
public class UndoRedoStackTests
{
	private static UndoRedoService CreateService() => new(new StackManager(), new SaveBoundaryManager(), new CommandMerger());

	[TestMethod]
	public void Execute_SingleCommand_CanUndoAndRedo()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		int value = 0;
		DelegateCommand command = new(
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
	private static readonly int[] expected = [1, 2];
	private static readonly int[] expectedArray = [1];

	[TestMethod]
	public void Execute_MultipleCommands_MaintainsCorrectOrder()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		List<int> values = [];

		DelegateCommand command1 = new("Add 1", () => values.Add(1), () => values.RemoveAt(values.Count - 1));
		DelegateCommand command2 = new("Add 2", () => values.Add(2), () => values.RemoveAt(values.Count - 1));
		DelegateCommand command3 = new("Add 3", () => values.Add(3), () => values.RemoveAt(values.Count - 1));

		// Act
		stack.Execute(command1);
		stack.Execute(command2);
		stack.Execute(command3);

		// Assert
		CollectionAssert.AreEqual(expected, values);

		stack.Undo();
		CollectionAssert.AreEqual(expected, values);

		stack.Undo();
		CollectionAssert.AreEqual(expectedArray, values);

		stack.Redo();
		CollectionAssert.AreEqual(expected, values);
	}

	[TestMethod]
	public void Execute_AfterUndo_ClearsFutureCommands()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		int value = 0;

		DelegateCommand command1 = new("Set to 1", () => value = 1, () => value = 0);
		DelegateCommand command2 = new("Set to 2", () => value = 2, () => value = 1);
		DelegateCommand command3 = new("Set to 3", () => value = 3, () => value = 2);

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
		UndoRedoService stack = CreateService();
		int value = 0;
		DelegateCommand command = new("Increment", () => value++, () => value--);

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
		UndoRedoService stack = CreateService();
		List<string> values = [];

		DelegateCommand[] commands =
		[
			new DelegateCommand("Add A", () => values.Add("A"), () => values.RemoveAt(values.Count - 1)),
			new DelegateCommand("Add B", () => values.Add("B"), () => values.RemoveAt(values.Count - 1)),
			new DelegateCommand("Add C", () => values.Add("C"), () => values.RemoveAt(values.Count - 1))
		];

		CompositeCommand composite = new("Add ABC", commands);

		// Act
		stack.Execute(composite);

		// Assert
		CollectionAssert.AreEqual(expected, values);

		stack.Undo();
		Assert.AreEqual(0, values.Count);

		stack.Redo();
		CollectionAssert.AreEqual(expected, values);
	}

	[TestMethod]
	public async Task NavigationProvider_CallsNavigateToOnUndoRedo()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		MockNavigationProvider navigationProvider = new();
		stack.SetNavigationProvider(navigationProvider);

		DelegateCommand command = new(
			"Test Command",
			() => { },
			() => { },
			navigationContext: "test-context");

		stack.Execute(command);

		// Act & Assert
		await stack.UndoAsync().ConfigureAwait(false);
		Assert.AreEqual("test-context", navigationProvider.LastNavigatedContext);

		await stack.RedoAsync().ConfigureAwait(false);
		Assert.AreEqual("test-context", navigationProvider.LastNavigatedContext);
	}

	[TestMethod]
	public void GetChangeVisualizations_ReturnsCorrectData()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		DelegateCommand command1 = new("Command 1", () => { }, () => { }, ChangeType.Insert, ["item1"]);
		DelegateCommand command2 = new("Command 2", () => { }, () => { }, ChangeType.Delete, ["item2"]);

		// Act
		stack.Execute(command1);
		stack.MarkAsSaved();
		stack.Execute(command2);

		List<ChangeVisualization> visualizations = [.. stack.GetChangeVisualizations()];

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
		UndoRedoService stack = CreateService();
		bool commandExecutedFired = false;
		bool commandUndoneFired = false;
		bool commandRedoneFired = false;
		bool saveBoundaryCreatedFired = false;

		stack.CommandExecuted += (_, _) => commandExecutedFired = true;
		stack.CommandUndone += (_, _) => commandUndoneFired = true;
		stack.CommandRedone += (_, _) => commandRedoneFired = true;
		stack.SaveBoundaryCreated += (_, _) => saveBoundaryCreatedFired = true;

		DelegateCommand command = new("Test", () => { }, () => { });

		// Act & Assert
		stack.Execute(command);
		Assert.IsTrue(commandExecutedFired);

		stack.Undo();
		Assert.IsTrue(commandUndoneFired);

		stack.Redo();
		Assert.IsTrue(commandRedoneFired);

		stack.MarkAsSaved("Test");
		Assert.IsTrue(saveBoundaryCreatedFired);
	}

	private sealed class MockNavigationProvider : INavigationProvider
	{
		public string? LastNavigatedContext { get; private set; }

		public Task<bool> NavigateToAsync(string context, CancellationToken cancellationToken = default)
		{
			LastNavigatedContext = context;
			return Task.FromResult(true);
		}

		public bool IsValidContext(string context) => true;
	}

	[TestMethod]
	public void Execute_MaxStackSizeReached_RemovesOldestCommands()
	{
		// Arrange
		UndoRedoOptions options = UndoRedoOptions.Create(maxStackSize: 3);
		UndoRedoService stack = new(new StackManager(), new SaveBoundaryManager(), new CommandMerger(), options);

		// Act
		for (int i = 1; i <= 5; i++)
		{
			int localI = i;
			stack.Execute(new DelegateCommand($"Command {i}", () => { }, () => { }));
		}

		// Assert
		Assert.AreEqual(3, stack.CommandCount); // Should be limited to max size
		Assert.AreEqual("Command 3", stack.Commands[0].Description); // Oldest retained command
		Assert.AreEqual("Command 5", stack.Commands[2].Description); // Newest command
	}

	[TestMethod]
	public void CommandMerging_ConsecutiveCommands_MergesCorrectly()
	{
		// Arrange
		UndoRedoOptions options = UndoRedoOptions.Create(autoMerge: true);
		UndoRedoService stack = new(new StackManager(), new SaveBoundaryManager(), new CommandMerger(), options);
		string value = "";

		// Act
		stack.Execute(new TestMergeableCommand(s => value = s, "A"));
		stack.Execute(new TestMergeableCommand(s => value = s, "AB"));
		stack.Execute(new TestMergeableCommand(s => value = s, "ABC"));

		// Assert
		Assert.AreEqual(1, stack.CommandCount); // Commands should be merged
		Assert.AreEqual("ABC", value);

		stack.Undo();
		Assert.AreEqual("", value); // Should undo all merged operations
	}

	[TestMethod]
	public void CompositeCommand_NestedComposites_HandlesCorrectly()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		List<string> values = [];

		DelegateCommand[] innerCommands1 =
		[
			new DelegateCommand("Add A", () => values.Add("A"), () => values.RemoveAt(values.Count - 1)),
			new DelegateCommand("Add B", () => values.Add("B"), () => values.RemoveAt(values.Count - 1))
		];

		DelegateCommand[] innerCommands2 =
		[
			new DelegateCommand("Add C", () => values.Add("C"), () => values.RemoveAt(values.Count - 1)),
			new DelegateCommand("Add D", () => values.Add("D"), () => values.RemoveAt(values.Count - 1))
		];

		CompositeCommand inner1 = new("Add AB", innerCommands1);
		CompositeCommand inner2 = new("Add CD", innerCommands2);
		CompositeCommand outer = new("Add ABCD", [inner1, inner2]);

		// Act
		stack.Execute(outer);

		// Assert
		CollectionAssert.AreEqual(expected, values);

		stack.Undo();
		Assert.AreEqual(0, values.Count);
	}

	[TestMethod]
	public void SaveBoundaries_MultipleUndoRedoOperations_MaintainsCorrectState()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		int value = 0;

		// Act
		stack.Execute(new DelegateCommand("Set 1", () => value = 1, () => value = 0));
		stack.MarkAsSaved("After 1");

		stack.Execute(new DelegateCommand("Set 2", () => value = 2, () => value = 1));
		stack.Execute(new DelegateCommand("Set 3", () => value = 3, () => value = 2));
		stack.MarkAsSaved("After 3");

		stack.Execute(new DelegateCommand("Set 4", () => value = 4, () => value = 3));

		// Assert
		Assert.AreEqual(4, value); // Verify current value
		Assert.AreEqual(2, stack.SaveBoundaries.Count);
		Assert.IsTrue(stack.HasUnsavedChanges);

		stack.Undo(); // Back to 3
		Assert.IsFalse(stack.HasUnsavedChanges); // At save boundary

		stack.Undo(); // Back to 2
		stack.Undo(); // Back to 1
		Assert.IsFalse(stack.HasUnsavedChanges); // At save boundary

		stack.Undo(); // Back to 0
		Assert.IsTrue(stack.HasUnsavedChanges); // Before first save boundary
	}

	[TestMethod]
	public async Task NavigationProvider_CancellationToken_HandlesCorrectly()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		SlowNavigationProvider navigationProvider = new();
		stack.SetNavigationProvider(navigationProvider);

		DelegateCommand command = new("Test", () => { }, () => { }, navigationContext: "test");
		stack.Execute(command);

		using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(10));

		// Act
		await stack.UndoAsync(cancellationToken: cts.Token).ConfigureAwait(false);

		// Assert - navigation should have been cancelled
		Assert.IsTrue(navigationProvider.WasCancelled);
	}

	[TestMethod]
	public void Execute_CommandThrowsException_DoesNotCorruptStack()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		int value = 0;

		stack.Execute(new DelegateCommand("Good Command", () => value = 1, () => value = 0));

		// Act & Assert
		Assert.ThrowsException<InvalidOperationException>(() =>
			stack.Execute(new DelegateCommand("Bad Command", () => throw new InvalidOperationException(), () => { })));

		// Stack should still be in good state
		Assert.AreEqual(1, stack.CommandCount);
		Assert.AreEqual(1, value);
		Assert.IsTrue(stack.CanUndo);
	}

	[TestMethod]
	public void UndoToSaveBoundary_NoSaveBoundaries_ThrowsException()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		stack.Execute(new DelegateCommand("Test", () => { }, () => { }));

		// Act & Assert
		Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
			await stack.UndoToSaveBoundaryAsync(new SaveBoundary(0, "Test")).ConfigureAwait(false));
	}

	[TestMethod]
	public void GetChangeVisualizations_WithLimits_ReturnsCorrectCount()
	{
		// Arrange
		UndoRedoService stack = CreateService();

		for (int i = 1; i <= 10; i++)
		{
			stack.Execute(new DelegateCommand($"Command {i}", () => { }, () => { }));
		}

		// Act
		List<ChangeVisualization> visualizations = [.. stack.GetChangeVisualizations(5)];

		// Assert
		Assert.AreEqual(5, visualizations.Count);
	}

	[TestMethod]
	public void Clear_WithSaveBoundariesAndCommands_ClearsEverything()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		stack.Execute(new DelegateCommand("Test 1", () => { }, () => { }));
		stack.MarkAsSaved("Save 1");
		stack.Execute(new DelegateCommand("Test 2", () => { }, () => { }));

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
		UndoRedoService stack = CreateService();
		List<string> eventOrder = [];

		stack.CommandExecuted += (_, _) => eventOrder.Add("Executed");
		stack.CommandUndone += (_, _) => eventOrder.Add("Undone");
		stack.CommandRedone += (_, _) => eventOrder.Add("Redone");
		stack.SaveBoundaryCreated += (_, _) => eventOrder.Add("SaveBoundary");

		// Act
		stack.Execute(new DelegateCommand("Test", () => { }, () => { }));
		stack.MarkAsSaved("Test");
		stack.Undo();
		stack.Redo();

		// Assert
		CollectionAssert.AreEqual(expected, eventOrder);
	}

	private sealed class TestMergeableCommand(Action<string> setter, string newValue) : BaseCommand(ChangeType.Modify, ["text"])
	{
		private readonly Action<string> _setter = setter;
		private readonly string _newValue = newValue;
		private readonly string _oldValue = "";

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
			return other is TestMergeableCommand;
		}

		public override ICommand MergeWith(ICommand other)
		{
			TestMergeableCommand otherCmd = (TestMergeableCommand)other;
			return new TestMergeableCommand(_setter, otherCmd._newValue);
		}
	}

	private sealed class SlowNavigationProvider : INavigationProvider
	{
		public bool WasCancelled { get; private set; }

		public async Task<bool> NavigateToAsync(string context, CancellationToken cancellationToken = default)
		{
			try
			{
				await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
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
}
