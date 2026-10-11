// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Test;

using ktsu.UndoRedo.Core.Services;
using ktsu.UndoRedo.Models;

[TestClass]
public class DropOldestTests
{
	private static UndoRedoService CreateService(UndoRedoOptions? options = null) =>
		new(new StackManager(), new SaveBoundaryManager(), new CommandMerger(), options);

	private static DelegateCommand Step(string name, List<string> log) =>
		new(name, () => log.Add(name), () => log.RemoveAt(log.Count - 1));

	private static UndoRedoService WithSteps(List<string> log, params string[] names)
	{
		UndoRedoService stack = CreateService();
		foreach (string name in names)
		{
			stack.Execute(Step(name, log));
		}

		return stack;
	}

	[TestMethod]
	public void DropOldest_RemovesTheOldestCommandsAndMovesThePositionDown()
	{
		List<string> log = [];
		UndoRedoService stack = WithSteps(log, "A", "B", "C", "D");

		int dropped = stack.DropOldest(2);

		Assert.AreEqual(2, dropped);
		Assert.AreEqual(2, stack.CommandCount);
		Assert.AreEqual("C", stack.Commands[0].Description);
		Assert.AreEqual(1, stack.CurrentPosition);
	}

	[TestMethod]
	public void DropOldest_DoesNotUndoOrRedoAnything()
	{
		List<string> log = [];
		UndoRedoService stack = WithSteps(log, "A", "B", "C");

		stack.DropOldest(2);

		Assert.AreEqual("A,B,C", string.Join(",", log));
		Assert.IsTrue(stack.Undo());
		Assert.AreEqual("A,B", string.Join(",", log));
		Assert.IsFalse(stack.Undo(), "The dropped commands can no longer be undone");
	}

	[TestMethod]
	public void DropOldest_NeverDropsAnUndoneCommand()
	{
		List<string> log = [];
		UndoRedoService stack = WithSteps(log, "A", "B", "C", "D");
		stack.Undo();
		stack.Undo();

		int dropped = stack.DropOldest(10);

		Assert.AreEqual(2, dropped, "Only A and B are applied");
		Assert.AreEqual(-1, stack.CurrentPosition);
		Assert.AreEqual(2, stack.CommandCount);
		Assert.IsTrue(stack.Redo());
		Assert.AreEqual("A,B,C", string.Join(",", log));
	}

	[TestMethod]
	public void DropOldest_CanDropEveryAppliedCommand()
	{
		List<string> log = [];
		UndoRedoService stack = WithSteps(log, "A", "B");

		int dropped = stack.DropOldest(2);

		Assert.AreEqual(2, dropped);
		Assert.AreEqual(0, stack.CommandCount);
		Assert.AreEqual(-1, stack.CurrentPosition);
		Assert.IsFalse(stack.CanUndo);
		Assert.IsFalse(stack.CanRedo);
	}

	[TestMethod]
	public void DropOldest_ZeroOrNothingApplied_DropsNothingAndRaisesNothing()
	{
		List<string> log = [];
		UndoRedoService stack = WithSteps(log, "A");
		stack.Undo();
		int events = 0;
		stack.StateChanged += (_, _) => events++;
		stack.CommandsDropped += (_, _) => events++;

		Assert.AreEqual(0, stack.DropOldest(0));
		Assert.AreEqual(0, stack.DropOldest(3));
		Assert.AreEqual(1, stack.CommandCount);
		Assert.AreEqual(0, events);
	}

	[TestMethod]
	public void DropOldest_NegativeCount_Throws()
	{
		UndoRedoService stack = CreateService();
		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => stack.DropOldest(-1));
	}

	[TestMethod]
	public void DropOldest_RaisesCommandsDroppedThenStateChanged()
	{
		List<string> log = [];
		UndoRedoService stack = WithSteps(log, "A", "B", "C");
		List<string> raised = [];
		CommandsDroppedEventArgs? args = null;
		stack.CommandsDropped += (_, e) =>
		{
			raised.Add("dropped");
			args = e;
		};
		stack.StateChanged += (_, _) => raised.Add("state");

		stack.DropOldest(2);

		Assert.AreEqual("dropped,state", string.Join(",", raised));
		Assert.IsNotNull(args);
		Assert.AreEqual("A,B", string.Join(",", args.Commands.Select(command => command.Description).ToArray()));
		Assert.AreEqual(0, args.Position);
	}

	[TestMethod]
	public void DropOldest_SaveOnLastDroppedCommand_LandsOnMinusOne()
	{
		List<string> log = [];
		UndoRedoService stack = WithSteps(log, "A", "B");
		stack.MarkAsSaved("after B");
		stack.Execute(Step("C", log));

		stack.DropOldest(2);

		Assert.AreEqual(1, stack.SaveBoundaries.Count);
		Assert.AreEqual(-1, stack.SaveBoundaries[0].Position);
		Assert.AreEqual("after B", stack.SaveBoundaries[0].Description);
		Assert.IsTrue(stack.HasUnsavedChanges);
		stack.Undo();
		Assert.IsFalse(stack.HasUnsavedChanges, "Position -1 is the saved state");
	}

	[TestMethod]
	public void DropOldest_SaveOnAnEarlierCommand_IsRemoved()
	{
		List<string> log = [];
		UndoRedoService stack = WithSteps(log, "A");
		stack.MarkAsSaved();
		stack.Execute(Step("B", log));
		stack.Execute(Step("C", log));

		stack.DropOldest(2);

		Assert.AreEqual(0, stack.SaveBoundaries.Count);
		stack.Undo();
		Assert.IsTrue(stack.HasUnsavedChanges, "The save after A can no longer be reached");
	}

	[TestMethod]
	public void DropOldest_SaveAfterTheDroppedCommands_MovesDownAndStaysClean()
	{
		List<string> log = [];
		UndoRedoService stack = WithSteps(log, "A", "B", "C");
		stack.MarkAsSaved();
		DateTimeOffset savedAt = stack.SaveBoundaries[0].Timestamp;

		stack.DropOldest(1);

		Assert.AreEqual(1, stack.SaveBoundaries[0].Position);
		Assert.AreEqual(savedAt, stack.SaveBoundaries[0].Timestamp, "Dropping must not restamp the save point");
		Assert.IsFalse(stack.HasUnsavedChanges);
	}

	[TestMethod]
	public void DropOldest_NothingSaved_StartIsNoLongerClean()
	{
		List<string> log = [];
		UndoRedoService stack = WithSteps(log, "A", "B");

		stack.DropOldest(1);
		stack.Undo();

		Assert.AreEqual(-1, stack.CurrentPosition);
		Assert.IsTrue(stack.HasUnsavedChanges, "Position -1 now holds A's result, which was never saved");
	}

	[TestMethod]
	public async Task DropOldest_BoundaryHeldAcrossTheDrop_StillUndoesToIt()
	{
		List<string> log = [];
		UndoRedoService stack = WithSteps(log, "A", "B");
		stack.MarkAsSaved();
		SaveBoundary held = stack.SaveBoundaries[0];
		stack.Execute(Step("C", log));
		stack.Execute(Step("D", log));

		stack.DropOldest(1);

		Assert.IsTrue(await stack.UndoToSaveBoundaryAsync(held, navigateToLastChange: false).ConfigureAwait(false));
		Assert.AreEqual("A,B", string.Join(",", log));
		Assert.IsFalse(stack.HasUnsavedChanges);
	}

	[TestMethod]
	public void DropOldest_StateRoundTripsAfterwards()
	{
		List<string> log = [];
		UndoRedoService stack = WithSteps(log, "A", "B", "C");
		stack.DropOldest(1);
		UndoRedoStackState state = stack.GetCurrentState();

		UndoRedoService restored = CreateService();
		Assert.IsTrue(restored.RestoreFromState(state));

		Assert.AreEqual(2, restored.CommandCount);
		Assert.AreEqual(1, restored.CurrentPosition);
		Assert.IsFalse(state.InitialStateIsClean);
	}

	[TestMethod]
	public void Execute_MaxStackSizeTrim_RaisesCommandsDropped()
	{
		UndoRedoService stack = CreateService(UndoRedoOptions.Create(maxStackSize: 2));
		List<string> log = [];
		List<CommandsDroppedEventArgs> raised = [];
		stack.CommandsDropped += (_, e) => raised.Add(e);

		stack.Execute(Step("A", log));
		stack.Execute(Step("B", log));
		Assert.AreEqual(0, raised.Count);
		stack.Execute(Step("C", log));

		Assert.AreEqual(1, raised.Count);
		Assert.AreEqual("A", raised[0].Commands.Single().Description);
		Assert.AreEqual(1, raised[0].Position);
	}
}
