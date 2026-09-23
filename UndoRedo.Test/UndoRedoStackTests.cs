// Copyright (c) 2023-2026 ktsu-dev contributors

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace ktsu.UndoRedo.Test;

using ktsu.UndoRedo.Models;
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
		Assert.IsFalse(stack.CanUndo, "CanUndo should be false when stack is empty");
		Assert.IsFalse(stack.CanRedo, "CanRedo should be false when stack is empty");

		stack.Execute(command);
		Assert.AreEqual(1, value);
		Assert.IsTrue(stack.CanUndo, "CanUndo should be true after executing a command");
		Assert.IsFalse(stack.CanRedo, "CanRedo should be false after executing a command");

		stack.Undo();
		Assert.AreEqual(0, value);
		Assert.IsFalse(stack.CanUndo, "CanUndo should be false after undoing the only command");
		Assert.IsTrue(stack.CanRedo, "CanRedo should be true after undoing a command");

		stack.Redo();
		Assert.AreEqual(1, value);
		Assert.IsTrue(stack.CanUndo, "CanUndo should be true after redoing a command");
		Assert.IsFalse(stack.CanRedo, "CanRedo should be false after redoing the last command");
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
		int[] expectedAfterThree = [1, 2, 3];
		CollectionAssert.AreEqual(expectedAfterThree, values);

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
		Assert.IsTrue(stack.CanUndo, "CanUndo should be true after executing commands");
		Assert.IsFalse(stack.CanRedo, "CanRedo should be false when future commands have been cleared");
	}

	[TestMethod]
	public void MarkAsSaved_TracksUnsavedChanges()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		int value = 0;
		DelegateCommand command = new("Increment", () => value++, () => value--);

		// Act & Assert
		Assert.IsFalse(stack.HasUnsavedChanges, "HasUnsavedChanges should be false when stack is empty");

		stack.Execute(command);
		Assert.IsTrue(stack.HasUnsavedChanges, "HasUnsavedChanges should be true after executing a command");

		stack.MarkAsSaved("Saved at 1");
		Assert.IsFalse(stack.HasUnsavedChanges, "HasUnsavedChanges should be false after marking as saved");
		Assert.HasCount(1, stack.SaveBoundaries);

		stack.Execute(command);
		Assert.IsTrue(stack.HasUnsavedChanges, "HasUnsavedChanges should be true after executing a command past save boundary");

		stack.Undo();
		Assert.IsFalse(stack.HasUnsavedChanges, "HasUnsavedChanges should be false when back at save boundary");
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
		string[] expectedComposite = ["A", "B", "C"];
		CollectionAssert.AreEqual(expectedComposite, values);

		stack.Undo();
		Assert.IsEmpty(values);

		stack.Redo();
		CollectionAssert.AreEqual(expectedComposite, values);
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
		Assert.HasCount(2, visualizations);

		Assert.AreEqual("Command 1", visualizations[0].Command.Description);
		Assert.IsTrue(visualizations[0].IsExecuted, "First command should be marked as executed");
		Assert.IsTrue(visualizations[0].HasSaveBoundary, "First command should have a save boundary");

		Assert.AreEqual("Command 2", visualizations[1].Command.Description);
		Assert.IsTrue(visualizations[1].IsExecuted, "Second command should be marked as executed");
		Assert.IsFalse(visualizations[1].HasSaveBoundary, "Second command should not have a save boundary");
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
		Assert.IsTrue(commandExecutedFired, "CommandExecuted event should fire when executing a command");

		stack.Undo();
		Assert.IsTrue(commandUndoneFired, "CommandUndone event should fire when undoing a command");

		stack.Redo();
		Assert.IsTrue(commandRedoneFired, "CommandRedone event should fire when redoing a command");

		stack.MarkAsSaved("Test");
		Assert.IsTrue(saveBoundaryCreatedFired, "SaveBoundaryCreated event should fire when marking as saved");
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
	public void CommandMerging_IncrementalCommands_AppliesEachEffectOnce()
	{
		// Arrange
		UndoRedoOptions options = UndoRedoOptions.Create(autoMerge: true);
		UndoRedoService stack = new(new StackManager(), new SaveBoundaryManager(), new CommandMerger(), options);
		List<char> value = [];

		// Act
		stack.Execute(new TestInsertMergeCommand(value, 0, "a"));
		stack.Execute(new TestInsertMergeCommand(value, 1, "b"));

		// Assert
		Assert.AreEqual(1, stack.CommandCount, "Merged commands should occupy a single stack slot");
		Assert.AreEqual("ab", new string([.. value]), "Merged execution should not duplicate already-applied effects");

		stack.Undo();
		Assert.AreEqual("", new string([.. value]), "Undo should revert the merged command completely");
	}

	[TestMethod]
	public void CommandMerging_AfterUndo_CleansInvalidSaveBoundaries()
	{
		// Arrange
		UndoRedoOptions options = UndoRedoOptions.Create(autoMerge: true);
		UndoRedoService stack = new(new StackManager(), new SaveBoundaryManager(), new CommandMerger(), options);
		string value = "";

		stack.Execute(new TestMergeableCommand(s => value = s, "A"));
		stack.Execute(new DelegateCommand("Set AX", () => value = "AX", () => value = "A"));
		stack.MarkAsSaved("after second command");
		stack.Undo();

		// Act
		stack.Execute(new TestMergeableCommand(s => value = s, "AB"));

		// Assert
		Assert.AreEqual("AB", value);
		Assert.AreEqual(1, stack.CommandCount, "Forward commands should be removed when executing after undo");
		Assert.IsEmpty(stack.SaveBoundaries, "Save boundaries beyond the current position should be cleaned up on merge");
		Assert.IsFalse(stack.CanRedo, "Redo should be unavailable after forward commands are cleared");
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
		string[] expectedNested = ["A", "B", "C", "D"];
		CollectionAssert.AreEqual(expectedNested, values);

		stack.Undo();
		Assert.IsEmpty(values);
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
		Assert.HasCount(2, stack.SaveBoundaries);
		Assert.IsTrue(stack.HasUnsavedChanges, "HasUnsavedChanges should be true after executing commands past save boundary");

		stack.Undo(); // Back to 3
		Assert.IsFalse(stack.HasUnsavedChanges, "HasUnsavedChanges should be false when at save boundary");

		stack.Undo(); // Back to 2
		stack.Undo(); // Back to 1
		Assert.IsFalse(stack.HasUnsavedChanges, "HasUnsavedChanges should be false when at earlier save boundary");

		stack.Undo(); // Back to 0
		Assert.IsTrue(stack.HasUnsavedChanges, "HasUnsavedChanges should be true before first save boundary");
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
		Assert.IsTrue(navigationProvider.WasCancelled, "Navigation should have been cancelled due to timeout");
	}

	[TestMethod]
	public void Execute_CommandThrowsException_DoesNotCorruptStack()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		int value = 0;

		stack.Execute(new DelegateCommand("Good Command", () => value = 1, () => value = 0));

		// Act & Assert
		Assert.ThrowsExactly<InvalidOperationException>(() =>
			stack.Execute(new DelegateCommand("Bad Command", () => throw new InvalidOperationException(), () => { })));

		// Stack should still be in good state
		Assert.AreEqual(1, stack.CommandCount);
		Assert.AreEqual(1, value);
		Assert.IsTrue(stack.CanUndo, "CanUndo should remain true after failed command execution");
	}

	[TestMethod]
	public void Execute_CommandThrowsException_PreservesRedoHistory()
	{
		// Arrange: three commands, then undo twice so B and C are available to redo
		UndoRedoService stack = CreateService();
		int value = 0;

		stack.Execute(new DelegateCommand("A", () => value = 1, () => value = 0));
		stack.Execute(new DelegateCommand("B", () => value = 2, () => value = 1));
		stack.Execute(new DelegateCommand("C", () => value = 3, () => value = 2));

		stack.Undo();
		stack.Undo();
		Assert.AreEqual(1, value);
		Assert.IsTrue(stack.CanRedo, "B and C should be available to redo before the failing command");

		// Act: a command that fails to apply must not branch the stack
		Assert.ThrowsExactly<InvalidOperationException>(() =>
			stack.Execute(new DelegateCommand("Bad Command", () => throw new InvalidOperationException(), () => { })));

		// Assert: nothing was applied, so nothing may have been discarded
		Assert.AreEqual(3, stack.CommandCount, "A command that failed to apply must not discard the forward history");
		Assert.IsTrue(stack.CanRedo, "CanRedo should remain true after a failed command execution");
		Assert.AreEqual(1, value, "The failed command must not have changed the application state");

		// And the preserved history must still be replayable
		stack.Redo();
		Assert.AreEqual(2, value, "Redo after a failed command must reapply B");
		stack.Redo();
		Assert.AreEqual(3, value, "Redo after a failed command must reapply C");
		Assert.IsFalse(stack.CanRedo, "The stack should be fully redone after replaying both preserved commands");
	}

	[TestMethod]
	public void Execute_CommandThrowsException_PreservesSaveBoundariesInForwardHistory()
	{
		// Arrange: a save boundary that lives inside the forward history
		UndoRedoService stack = CreateService();
		int value = 0;

		stack.Execute(new DelegateCommand("A", () => value = 1, () => value = 0));
		stack.Execute(new DelegateCommand("B", () => value = 2, () => value = 1));
		stack.MarkAsSaved("saved at B");
		stack.Undo();

		Assert.AreEqual(1, value);
		Assert.AreEqual(1, stack.SaveBoundaries.Count, "The save boundary should exist before the failing command");

		// Act
		Assert.ThrowsExactly<InvalidOperationException>(() =>
			stack.Execute(new DelegateCommand("Bad Command", () => throw new InvalidOperationException(), () => { })));

		// Assert: the boundary only becomes invalid once the branch actually happens
		Assert.AreEqual(1, stack.SaveBoundaries.Count, "A command that failed to apply must not invalidate save boundaries");
	}

	[TestMethod]
	public void Execute_MergedCommandThrowsException_DoesNotCorruptStack()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		string value = "";

		stack.Execute(new MergeFailureCommand(v => value = v, "a"));
		Assert.AreEqual("a", value);
		Assert.AreEqual(1, stack.CommandCount);

		// Act: a second mergeable command, where the merged result throws on Execute
		Assert.ThrowsExactly<InvalidOperationException>(() =>
			stack.Execute(new MergeFailureCommand(v => value = v, "b")));

		// Assert: the stack must describe what was actually applied
		Assert.AreEqual(1, stack.CommandCount, "A merge that failed to apply must not be recorded on the stack");
		Assert.AreEqual("a", value, "The application state must be restored to the last successfully applied command");
		Assert.IsTrue(stack.CanUndo, "CanUndo should remain true after a failed merge");

		// And undoing must return to the pre-command state, not double-apply an undo
		stack.Undo();
		Assert.AreEqual("", value, "Undo after a failed merge must undo exactly the one applied command");
		Assert.IsFalse(stack.CanUndo, "The stack should be back at the start after undoing the single applied command");
	}

	[TestMethod]
	public void Execute_MergedCommandThrowsAndRestoreAlsoThrows_SurfacesTheOriginalFailure()
	{
		// Arrange: the merged edit throws, and restoring the previous command throws too
		UndoRedoService stack = CreateService();
		string value = "";

		stack.Execute(new MergeFailureCommand(v => value = v, "a", throwOnRestore: true));
		Assert.AreEqual("a", value);

		// Act & Assert: the caller sees the failure that actually broke the merge, not the one from
		// the best-effort restore, which is swallowed the way CompositeCommand swallows its rollback failures
		Assert.ThrowsExactly<InvalidOperationException>(() =>
			stack.Execute(new MergeFailureCommand(v => value = v, "b")));

		// The stack is still unchanged, so it describes the one command it recorded
		Assert.AreEqual(1, stack.CommandCount, "A merge that failed to apply must not be recorded on the stack");
		Assert.IsTrue(stack.CanUndo, "CanUndo should remain true after a failed merge");
	}

	[TestMethod]
	public async Task UndoToSaveBoundary_WhenAlreadyAtPosition_ReturnsFalse()
	{
		// Arrange
		UndoRedoService stack = CreateService();
		stack.Execute(new DelegateCommand("Test", () => { }, () => { }));

		// Act - Try to undo to position 0, but we're already at position 0
		// (CurrentPosition starts at -1 and becomes 0 after first command)
		bool result = await stack.UndoToSaveBoundaryAsync(new SaveBoundary(0, "Test")).ConfigureAwait(false);

		// Assert - Should return false since there's nothing to undo to reach position 0
		Assert.IsFalse(result, "UndoToSaveBoundary should return false when already at target position");
		Assert.AreEqual(0, stack.CurrentPosition);
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
		Assert.HasCount(5, visualizations);
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
		Assert.IsEmpty(stack.SaveBoundaries);
		Assert.IsFalse(stack.CanUndo, "CanUndo should be false after clearing the stack");
		Assert.IsFalse(stack.CanRedo, "CanRedo should be false after clearing the stack");
		Assert.IsFalse(stack.HasUnsavedChanges, "HasUnsavedChanges should be false after clearing the stack");
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
		string[] expectedEvents = ["Executed", "SaveBoundary", "Undone", "Redone"];
		CollectionAssert.AreEqual(expectedEvents, eventOrder);
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

	/// <summary>
	/// A mergeable command whose merged result always throws on <see cref="Execute"/>, standing in for
	/// a merge that produces an invalid combined edit
	/// </summary>
	private sealed class MergeFailureCommand(Action<string> setter, string newValue, bool throwOnExecute = false, bool throwOnRestore = false) : BaseCommand(ChangeType.Modify, ["text"])
	{
		private readonly Action<string> _setter = setter;
		private readonly string _newValue = newValue;
		private readonly bool _throwOnExecute = throwOnExecute;
		private readonly bool _throwOnRestore = throwOnRestore;
		private readonly string _oldValue = "";
		private bool _hasExecuted;

		public override string Description => $"Set text to '{_newValue}'";

		public override void Execute()
		{
			if (_throwOnExecute)
			{
				throw new InvalidOperationException("The merged edit is invalid");
			}

			// A second Execute is the service restoring this command after a failed merge
			if (_throwOnRestore && _hasExecuted)
			{
				throw new NotSupportedException("The command cannot be re-applied");
			}

			_hasExecuted = true;
			_setter(_newValue);
		}

		public override void Undo() => _setter(_oldValue);

		public override bool CanMergeWith(ICommand other) => other is MergeFailureCommand;

		public override ICommand MergeWith(ICommand other)
		{
			MergeFailureCommand otherCmd = (MergeFailureCommand)other;
			return new MergeFailureCommand(_setter, otherCmd._newValue, throwOnExecute: true);
		}
	}

	private sealed class TestInsertMergeCommand(List<char> target, int position, string text) : BaseCommand(ChangeType.Modify, ["text"])
	{
		private readonly List<char> _target = target;
		private readonly int _position = position;
		private readonly string _text = text;

		public override string Description => $"Insert '{_text}' at {_position}";

		public override void Execute()
		{
			_target.InsertRange(_position, _text);
		}

		public override void Undo()
		{
			_target.RemoveRange(_position, _text.Length);
		}

		public override bool CanMergeWith(ICommand other)
		{
			return other is TestInsertMergeCommand otherCmd &&
				ReferenceEquals(_target, otherCmd._target) &&
				otherCmd._position == _position + _text.Length;
		}

		public override ICommand MergeWith(ICommand other)
		{
			TestInsertMergeCommand otherCmd = (TestInsertMergeCommand)other;
			return new TestInsertMergeCommand(_target, _position, _text + otherCmd._text);
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
