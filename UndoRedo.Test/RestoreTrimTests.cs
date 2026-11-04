// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Test;

using ktsu.UndoRedo.Core.Services;
using ktsu.UndoRedo.Models;

/// <summary>
/// Restoring a history saved under a larger MaxStackSize keeps no more than this service's limit
/// </summary>
[TestClass]
public sealed class RestoreTrimTests
{
	private static UndoRedoService CreateService(int maxStackSize) =>
		new(new StackManager(), new SaveBoundaryManager(), new CommandMerger(), UndoRedoOptions.Create(maxStackSize: maxStackSize));

	private static UndoRedoService WithSteps(int count, List<string> log)
	{
		UndoRedoService service = CreateService(maxStackSize: count);
		for (int i = 0; i < count; i++)
		{
			string name = $"S{i}";
			service.Execute(new DelegateCommand(name, () => log.Add(name), () => log.RemoveAt(log.Count - 1)));
		}

		return service;
	}

	[TestMethod]
	public void Restore_IntoASmallerLimit_KeepsOnlyTheNewestCommands()
	{
		List<string> log = [];
		UndoRedoService source = WithSteps(100, log);
		UndoRedoService capped = CreateService(maxStackSize: 10);

		Assert.IsTrue(capped.RestoreFromState(source.GetCurrentState()));

		Assert.AreEqual(10, capped.CommandCount);
		Assert.AreEqual(9, capped.CurrentPosition);
		Assert.AreEqual("S90", capped.Commands[0].Description);
		Assert.AreEqual("S99", capped.Commands[9].Description);
	}

	[TestMethod]
	public void Restore_IntoASmallerLimit_ReportsTheTrimmedCommands()
	{
		List<string> log = [];
		UndoRedoService source = WithSteps(100, log);
		UndoRedoService capped = CreateService(maxStackSize: 10);
		List<CommandsDroppedEventArgs> dropped = [];
		List<CommandsDiscardedEventArgs> discarded = [];
		capped.CommandsDropped += (_, e) => dropped.Add(e);
		capped.CommandsDiscarded += (_, e) => discarded.Add(e);

		Assert.IsTrue(capped.RestoreFromState(source.GetCurrentState()));

		Assert.HasCount(1, dropped);
		Assert.HasCount(90, dropped[0].Commands);
		Assert.AreEqual("S0", dropped[0].Commands[0].Description);
		Assert.AreEqual("S89", dropped[0].Commands[89].Description);
		Assert.HasCount(1, discarded);
		Assert.AreEqual(CommandDiscardReason.Trimmed, discarded[0].Reason);
		Assert.HasCount(90, discarded[0].Commands);
	}

	[TestMethod]
	public void Restore_IntoASmallerLimit_ThenExecute_DropsOnlyOne()
	{
		List<string> log = [];
		UndoRedoService source = WithSteps(100, log);
		UndoRedoService capped = CreateService(maxStackSize: 10);
		Assert.IsTrue(capped.RestoreFromState(source.GetCurrentState()));
		List<CommandsDroppedEventArgs> dropped = [];
		capped.CommandsDropped += (_, e) => dropped.Add(e);

		capped.Execute(new DelegateCommand("next", () => { }, () => { }));

		Assert.HasCount(1, dropped);
		Assert.HasCount(1, dropped[0].Commands);
		Assert.AreEqual(10, capped.CommandCount);
	}

	[TestMethod]
	public void Restore_IntoASmallerLimit_MovesTheSaveBoundaryDown()
	{
		List<string> log = [];
		UndoRedoService source = WithSteps(100, log);
		source.Undo();
		source.Undo();
		source.MarkAsSaved("saved at S97");
		source.Redo();
		source.Redo();
		UndoRedoService capped = CreateService(maxStackSize: 10);

		Assert.IsTrue(capped.RestoreFromState(source.GetCurrentState()));

		Assert.HasCount(1, capped.SaveBoundaries);
		Assert.AreEqual(7, capped.SaveBoundaries[0].Position);
		Assert.IsTrue(capped.HasUnsavedChanges);
		Assert.IsTrue(capped.Undo());
		Assert.IsTrue(capped.Undo());
		Assert.IsFalse(capped.HasUnsavedChanges, "Undoing back to S97 returns to the saved state");
	}

	[TestMethod]
	public void Restore_IntoASmallerLimit_NeverTrimsTheRedoHistory()
	{
		List<string> log = [];
		UndoRedoService source = WithSteps(20, log);
		for (int i = 0; i < 15; i++)
		{
			source.Undo();
		}

		Assert.AreEqual(4, source.CurrentPosition);
		UndoRedoService capped = CreateService(maxStackSize: 10);

		Assert.IsTrue(capped.RestoreFromState(source.GetCurrentState()));

		// Only S0 to S4 are applied, so only they can go; the 15 undone commands stay redoable
		Assert.AreEqual(15, capped.CommandCount);
		Assert.AreEqual(-1, capped.CurrentPosition);
		Assert.AreEqual("S5", capped.Commands[0].Description);
		Assert.IsTrue(capped.Redo());
	}

	[TestMethod]
	public void Restore_WithinTheLimit_KeepsEverything()
	{
		List<string> log = [];
		UndoRedoService source = WithSteps(10, log);
		UndoRedoService capped = CreateService(maxStackSize: 10);
		List<CommandsDroppedEventArgs> dropped = [];
		capped.CommandsDropped += (_, e) => dropped.Add(e);

		Assert.IsTrue(capped.RestoreFromState(source.GetCurrentState()));

		Assert.AreEqual(10, capped.CommandCount);
		Assert.IsEmpty(dropped);
	}
}
