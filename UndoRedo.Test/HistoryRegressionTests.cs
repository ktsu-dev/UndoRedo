// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Test;

using ktsu.UndoRedo.Models;
using ktsu.UndoRedo.Core.Services;
using static ktsu.UndoRedo.Test.HistoryTestSupport;

/// <summary>
/// Merging after an undo, clearing, reverting to a save point from either side, and restoring which save is
/// the latest and which boundaries a caller holds
/// </summary>
[TestClass]
public sealed class HistoryRegressionTests
{
	private static UndoRedoOptions AutoMerge => UndoRedoOptions.Create(autoMerge: true);

	[TestMethod]
	public void AnEditAfterUndo_DoesNotMergeIntoTheCommandUndoneTo()
	{
		StrongBox<string> buffer = new(string.Empty);
		UndoRedoService service = Service(AutoMerge);
		service.Execute(new InsertText(buffer, 0, "Hello"));
		service.Execute(new DelegateCommand("Delete", () => buffer.Value = string.Empty, () => buffer.Value = "Hello"));
		service.Undo();

		service.Execute(new InsertText(buffer, 5, " world"));

		Assert.AreEqual("Hello world", buffer.Value);
		Assert.AreEqual(2, service.CommandCount, "The edit should be a command of its own");
		service.Undo();
		Assert.AreEqual("Hello", buffer.Value, "One undo should return to the state the user undid to");
	}

	[TestMethod]
	public void AnEditAfterRedo_DoesNotMergeIntoTheCommandRedoneTo()
	{
		StrongBox<string> buffer = new(string.Empty);
		UndoRedoService service = Service(AutoMerge);
		service.Execute(new InsertText(buffer, 0, "Hello"));
		service.Undo();
		service.Redo();

		service.Execute(new InsertText(buffer, 5, " world"));

		Assert.AreEqual(2, service.CommandCount);
		service.Undo();
		Assert.AreEqual("Hello", buffer.Value);
	}

	[TestMethod]
	public void ConsecutiveEdits_StillMerge()
	{
		StrongBox<string> buffer = new(string.Empty);
		UndoRedoService service = Service(AutoMerge);
		foreach ((int position, string text) in new[] { (0, "h"), (1, "e"), (2, "l"), (3, "l"), (4, "o") })
		{
			service.Execute(new InsertText(buffer, position, text));
		}

		Assert.AreEqual(1, service.CommandCount);
		Assert.AreEqual("hello", buffer.Value);
	}

	[TestMethod]
	public void Clear_KeepsUnsavedChangesUnsaved()
	{
		StrongBox<int> value = new(0);
		UndoRedoService service = Service();
		service.Execute(Increment(value));
		service.MarkAsSaved();
		service.Execute(Increment(value));
		Assert.IsTrue(service.HasUnsavedChanges);

		service.Clear();

		Assert.IsTrue(service.HasUnsavedChanges, "Clearing history doesn't save the document");
		Assert.AreEqual(2, value.Value);
	}

	[TestMethod]
	public void Clear_KeepsASavedDocumentSaved()
	{
		StrongBox<int> value = new(0);
		UndoRedoService service = Service();
		service.Execute(Increment(value));
		service.MarkAsSaved();

		service.Clear();

		Assert.IsFalse(service.HasUnsavedChanges);
		service.Execute(Increment(value));
		Assert.IsTrue(service.HasUnsavedChanges, "An edit after the clear is unsaved");
	}

	[TestMethod]
	public async Task RevertingToASavePointAfterUndoingPastIt_RedoesForward()
	{
		StrongBox<int> value = new(0);
		UndoRedoService service = Service();
		for (int i = 0; i < 3; i++)
		{
			service.Execute(Increment(value));
		}

		service.MarkAsSaved("save");
		SaveBoundary boundary = service.SaveBoundaries[^1];
		List<int> redone = [];
		service.CommandRedone += (_, e) => redone.Add(e.Position);
		await service.UndoAsync().ConfigureAwait(false);
		await service.UndoAsync().ConfigureAwait(false);

		bool reverted = await service.UndoToSaveBoundaryAsync(boundary).ConfigureAwait(false);

		Assert.IsTrue(reverted);
		Assert.AreEqual(3, value.Value);
		Assert.AreEqual(2, service.CurrentPosition);
		Assert.IsFalse(service.HasUnsavedChanges);
		Assert.HasCount(2, redone);
		Assert.AreEqual(1, redone[0]);
		Assert.AreEqual(2, redone[1]);
	}

	[TestMethod]
	public async Task RevertingToTheSavePointAlreadyAt_ReportsNothingToDo()
	{
		StrongBox<int> value = new(0);
		UndoRedoService service = Service();
		service.Execute(Increment(value));
		service.MarkAsSaved();

		Assert.IsFalse(await service.UndoToSaveBoundaryAsync(service.SaveBoundaries[0]).ConfigureAwait(false));
		Assert.AreEqual(1, value.Value);
	}

	[TestMethod]
	public async Task RestoringState_KeepsTheBoundariesACallerHolds()
	{
		StrongBox<int> value = new(0);
		UndoRedoService service = Service();
		SaveBoundary? fromEvent = null;
		service.SaveBoundaryCreated += (_, e) => fromEvent = e.SaveBoundary;
		service.Execute(Increment(value));
		service.MarkAsSaved("s");
		service.Execute(Increment(value));
		service.Execute(Increment(value));
		UndoRedoStackState state = service.GetCurrentState();

		Assert.IsTrue(service.RestoreFromState(state));

		Assert.AreEqual(2, service.GetCommandsToUndo(fromEvent!).Count());
		Assert.AreEqual(2, service.GetCommandsToUndo(state.SaveBoundaries[0]).Count());
		Assert.IsTrue(await service.UndoToSaveBoundaryAsync(state.SaveBoundaries[0]).ConfigureAwait(false));
		Assert.AreEqual(0, service.CurrentPosition);
		Assert.AreEqual(1, value.Value);
		Assert.AreEqual(state.SaveBoundaries[0].Timestamp, service.SaveBoundaries[0].Timestamp);
	}

	private static UndoRedoService BranchedAwayFromTheLatestSave(StrongBox<int> value)
	{
		UndoRedoService service = JsonService();
		service.Execute(Increment(value, "a"));
		service.MarkAsSaved("A");
		service.Execute(Increment(value, "b"));
		service.MarkAsSaved("B");
		service.Undo();
		service.Execute(Increment(value, "c"));
		service.Undo();
		Assert.IsTrue(service.HasUnsavedChanges, "Disk holds B, which is no longer in the history");
		return service;
	}

	[TestMethod]
	public void RestoringState_KeepsALatestSaveThatBranchingRemoved()
	{
		UndoRedoService original = BranchedAwayFromTheLatestSave(new StrongBox<int>(0));
		UndoRedoService restored = Service();

		Assert.IsTrue(restored.RestoreFromState(original.GetCurrentState()));

		Assert.AreEqual(0, restored.CurrentPosition);
		Assert.IsTrue(restored.HasUnsavedChanges);
	}

	[TestMethod]
	public async Task LoadingState_KeepsALatestSaveThatBranchingRemoved()
	{
		StrongBox<int> value = new(0);
		UndoRedoService original = BranchedAwayFromTheLatestSave(value);
		byte[] data = await original.SaveStateAsync().ConfigureAwait(false);
		UndoRedoService loaded = JsonService();

		Assert.IsTrue(await loaded.LoadStateAsync(data).ConfigureAwait(false));

		Assert.AreEqual(0, loaded.CurrentPosition);
		Assert.IsTrue(loaded.HasUnsavedChanges);
	}

	[TestMethod]
	public void RestoringStateWithoutALatestSave_TreatsTheLastBoundaryAsLatest()
	{
		StrongBox<int> value = new(0);
		UndoRedoService original = Service();
		original.Execute(Increment(value));
		original.MarkAsSaved();
		UndoRedoStackState older = original.GetCurrentState() with { LatestSaveBoundaryIndex = null };
		UndoRedoService restored = Service();

		Assert.IsTrue(restored.RestoreFromState(older));

		Assert.IsFalse(restored.HasUnsavedChanges);
	}
}
