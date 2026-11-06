// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Test;

using ktsu.UndoRedo.Models;
using ktsu.UndoRedo.Core.Services;
using static ktsu.UndoRedo.Test.HistoryTestSupport;

/// <summary>
/// CommandsDiscarded never reports a command that a composite in the history still holds, whatever
/// removed the command's own slot
/// </summary>
[TestClass]
public sealed class CompositeDiscardTests
{
	[TestMethod]
	public void GroupingUndoneEdits_DoesNotReportThemAsBranched()
	{
		List<string> text = [];
		UndoRedoService service = Service(UndoRedoOptions.Create(autoMerge: false));
		List<ICommand> discarded = [];
		service.CommandsDiscarded += (_, e) => discarded.AddRange(e.Commands);
		DelegateCommand a = Add(text, "a");
		DelegateCommand b = Add(text, "b");

		service.Execute(a);
		service.Execute(b);
		service.Undo();
		service.Undo();
		service.Execute(new CompositeCommand("group", [a, b]));

		Assert.AreEqual(0, discarded.Count);
		Assert.AreEqual("ab", string.Concat(text));
	}

	[TestMethod]
	public void GroupingUndoneEdits_StillReportsTheEditsLeftOut()
	{
		List<string> text = [];
		UndoRedoService service = Service(UndoRedoOptions.Create(autoMerge: false));
		List<ICommand> discarded = [];
		service.CommandsDiscarded += (_, e) =>
		{
			Assert.AreEqual(CommandDiscardReason.Branched, e.Reason);
			discarded.AddRange(e.Commands);
		};
		DelegateCommand a = Add(text, "a");
		DelegateCommand b = Add(text, "b");

		service.Execute(a);
		service.Execute(b);
		service.Undo();
		service.Undo();
		service.Execute(new CompositeCommand("group", [new CompositeCommand("inner", [a])]));

		CollectionAssert.AreEqual(new ICommand[] { b }, discarded);
	}

	[TestMethod]
	public void RestoringAStateWhoseCompositeHoldsACurrentCommand_DoesNotReportItAsReplaced()
	{
		StrongBox<int> value = new(0);
		UndoRedoService service = Service(UndoRedoOptions.Create(autoMerge: false));
		DelegateCommand x = Increment(value, "x");
		service.Execute(x);
		List<ICommand> discarded = [];
		service.CommandsDiscarded += (_, e) => discarded.AddRange(e.Commands);
		UndoRedoStackState current = service.GetCurrentState();
		UndoRedoStackState grouped = current with { Commands = [new CompositeCommand("gx", [x])] };

		Assert.IsTrue(service.RestoreFromState(grouped));

		Assert.AreEqual(0, discarded.Count);
	}

	[TestMethod]
	public void DroppingTheOldestCommand_DoesNotReportOneARemainingCompositeHolds()
	{
		StrongBox<int> value = new(0);
		UndoRedoService service = Service(UndoRedoOptions.Create(autoMerge: false));
		List<ICommand> discarded = [];
		service.CommandsDiscarded += (_, e) =>
		{
			Assert.AreEqual(CommandDiscardReason.Trimmed, e.Reason);
			discarded.AddRange(e.Commands);
		};
		DelegateCommand x = Increment(value, "x");

		service.Execute(x);
		service.Execute(new CompositeCommand("again", [x]));
		Assert.AreEqual(1, service.DropOldest(1));

		Assert.AreEqual(0, discarded.Count);
	}

	[TestMethod]
	public void TrimmingToMaxStackSize_DoesNotReportOneARemainingCompositeHolds()
	{
		StrongBox<int> value = new(0);
		UndoRedoService service = Service(UndoRedoOptions.Create(maxStackSize: 1, autoMerge: false));
		List<ICommand> discarded = [];
		service.CommandsDiscarded += (_, e) => discarded.AddRange(e.Commands);
		DelegateCommand x = Increment(value, "x");

		service.Execute(x);
		service.Execute(new CompositeCommand("again", [x]));

		Assert.AreEqual(0, discarded.Count);
	}

	private static DelegateCommand Add(List<string> text, string item) =>
		new(item, () => text.Add(item), () => text.Remove(item));
}
