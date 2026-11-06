// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Test;

using ktsu.UndoRedo.Core.Services;
using ktsu.UndoRedo.Models;
using static ktsu.UndoRedo.Test.HistoryTestSupport;

/// <summary>
/// Merging asks CanMergeWith once, and a merge that fails leaves the history describing the document
/// </summary>
[TestClass]
public sealed class MergeFailureTests
{
	private static UndoRedoOptions AutoMerge => UndoRedoOptions.Create(autoMerge: true);

	/// <summary>Merges only while the target still holds the value it set, and counts the times it is asked</summary>
	private sealed class SetValue(StrongBox<int> target, int oldValue, int newValue) : BaseCommand(ChangeType.Modify, ["v"])
	{
		public static int CanMergeCalls { get; set; }

		public int Old { get; } = oldValue;

		public int New { get; } = newValue;

		public override string Description => $"{Old}->{New}";

		public override void Execute() => target.Value = New;

		public override void Undo() => target.Value = Old;

		public override bool CanMergeWith(ICommand other)
		{
			CanMergeCalls++;
			return other is SetValue && target.Value == New;
		}

		public override ICommand MergeWith(ICommand other) => new SetValue(target, Old, ((SetValue)other).New);
	}

	/// <summary>Merges in place, and whose Execute throws for a value over 10</summary>
	private sealed class SetCapped(StrongBox<int> target, int oldValue, int newValue) : BaseCommand(ChangeType.Modify, ["v"])
	{
		public int Old { get; } = oldValue;

		public int New { get; private set; } = newValue;

		public override string Description => $"{Old}->{New}";

		public override void Execute()
		{
			if (New > 10)
			{
				throw new ArgumentOutOfRangeException(nameof(New), New, "Over the cap");
			}

			target.Value = New;
		}

		public override void Undo() => target.Value = Old;

		public override bool CanMergeWith(ICommand other) => other is SetCapped;

		public override ICommand MergeWith(ICommand other)
		{
			New = ((SetCapped)other).New;
			return this;
		}
	}

	/// <summary>Merges into a new command whose Execute throws</summary>
	private sealed class FailingMerge(StrongBox<int> target, int value) : BaseCommand(ChangeType.Modify, ["v"])
	{
		public bool ThrowOnExecute { get; set; }

		public override string Description => $"set {value}";

		public override void Execute()
		{
			if (ThrowOnExecute)
			{
				throw new InvalidOperationException("Execute failed");
			}

			target.Value = value;
		}

		public override void Undo() => target.Value = 0;

		public override bool CanMergeWith(ICommand other) => other is FailingMerge;

		public override ICommand MergeWith(ICommand other) => new FailingMerge(target, -1) { ThrowOnExecute = true };
	}

	[TestMethod]
	public void ACanMergeWithThatReadsTheDocument_StillMerges()
	{
		StrongBox<int> box = new(0);
		UndoRedoService service = Service(AutoMerge);
		service.Execute(new SetValue(box, 0, 1));
		SetValue.CanMergeCalls = 0;

		service.Execute(new SetValue(box, 1, 2));

		Assert.AreEqual(2, box.Value);
		Assert.AreEqual(1, service.CommandCount);
		Assert.AreEqual("0->2", service.Commands[0].Description);
		Assert.AreEqual(1, SetValue.CanMergeCalls, "CanMergeWith should be asked once per Execute");
		service.Undo();
		Assert.AreEqual(0, box.Value);
	}

	[TestMethod]
	public void AnInPlaceMergeWhoseExecuteThrows_LeavesTheHistoryMatchingTheDocument()
	{
		StrongBox<int> box = new(0);
		UndoRedoService service = Service(AutoMerge);
		service.Execute(new SetCapped(box, 0, 5));
		List<CommandsDiscardedEventArgs> discarded = [];
		service.CommandsDiscarded += (_, e) => discarded.Add(e);

		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => service.Execute(new SetCapped(box, 5, 20)));

		// The edit was changed in place, so it can't be applied again: it leaves the history instead
		Assert.AreEqual(0, box.Value);
		Assert.AreEqual(0, service.CommandCount);
		Assert.IsFalse(service.CanUndo);
		Assert.IsFalse(service.CanRedo);
		Assert.HasCount(1, discarded);
		Assert.AreEqual(CommandDiscardReason.MergeFailed, discarded[0].Reason);
	}

	[TestMethod]
	public void AnInPlaceMergeWhoseExecuteThrows_LeavesTheEarlierHistoryWorking()
	{
		StrongBox<int> box = new(0);
		UndoRedoService service = Service(AutoMerge);
		service.Execute(new DelegateCommand("to 3", () => box.Value = 3, () => box.Value = 0));
		service.Execute(new SetCapped(box, 3, 5));

		Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => service.Execute(new SetCapped(box, 5, 20)));

		Assert.AreEqual(3, box.Value);
		Assert.AreEqual(1, service.CommandCount);
		Assert.IsTrue(service.Undo());
		Assert.AreEqual(0, box.Value);
		Assert.IsTrue(service.Redo());
		Assert.AreEqual(3, box.Value);
	}

	[TestMethod]
	public void AMergeWhoseExecuteThrows_StillRestoresTheOriginalWhenItCan()
	{
		StrongBox<int> box = new(0);
		UndoRedoService service = Service(AutoMerge);
		service.Execute(new FailingMerge(box, 5));

		Assert.ThrowsExactly<InvalidOperationException>(() => service.Execute(new FailingMerge(box, 7)));

		Assert.AreEqual(5, box.Value);
		Assert.AreEqual(1, service.CommandCount);
		Assert.IsTrue(service.Undo());
		Assert.AreEqual(0, box.Value);
	}
}
