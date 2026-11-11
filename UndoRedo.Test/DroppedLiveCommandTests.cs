// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Test;

using ktsu.UndoRedo.Core.Services;
using ktsu.UndoRedo.Models;
using static HistoryTestSupport;

/// <summary>
/// CommandsDropped reports only commands that have left the history, so a handler can release them (#184)
/// </summary>
[TestClass]
public sealed class DroppedLiveCommandTests
{
	/// <summary>
	/// A reusable command that fails once its resources are released
	/// </summary>
	private sealed class Bitmap : BaseCommand, IDisposable
	{
		private byte[]? _buffer = new byte[16];

		public Bitmap() : base(ChangeType.Modify, []) { }

		public override string Description => "paint";

		public override void Execute() => _ = _buffer ?? throw new ObjectDisposedException("paint");

		public override void Undo() => _ = _buffer ?? throw new ObjectDisposedException("paint");

		public void Dispose() => _buffer = null;
	}

	private static List<ICommand> DisposeDropped(UndoRedoService service)
	{
		List<ICommand> dropped = [];
		service.CommandsDropped += (_, e) =>
		{
			foreach (ICommand command in e.Commands)
			{
				dropped.Add(command);
				(command as IDisposable)?.Dispose();
			}
		};
		return dropped;
	}

	[TestMethod]
	public void MaxStackSizeTrim_OfAnInstanceStillInTheHistory_DoesNotReportIt()
	{
		UndoRedoService service = Service(UndoRedoOptions.Create(maxStackSize: 3, autoMerge: false));
		List<ICommand> dropped = DisposeDropped(service);
		using Bitmap paint = new();
		StrongBox<int> value = new(0);

		service.Execute(paint);
		service.Execute(Increment(value));
		service.Execute(Increment(value));
		service.Execute(paint);

		Assert.IsEmpty(dropped);
		Assert.AreSame(paint, service.Commands[^1]);
		service.Undo();
		Assert.AreEqual(1, service.CurrentPosition);
	}

	[TestMethod]
	public void MaxStackSizeTrim_ReportsAnInstanceOnceItsLastSlotLeaves()
	{
		UndoRedoService service = Service(UndoRedoOptions.Create(maxStackSize: 2, autoMerge: false));
		List<ICommand> dropped = DisposeDropped(service);
		using Bitmap paint = new();
		StrongBox<int> value = new(0);
		DelegateCommand last = Increment(value);

		service.Execute(paint);
		service.Execute(paint);
		service.Execute(Increment(value));
		Assert.IsEmpty(dropped, "the second slot still holds paint");

		service.Execute(last);

		CollectionAssert.AreEqual(new ICommand[] { paint }, dropped);
	}

	[TestMethod]
	public void DropOldest_OfAnInstanceStillInTheHistory_DoesNotReportIt()
	{
		UndoRedoService service = Service(UndoRedoOptions.Create(autoMerge: false));
		List<ICommand> dropped = DisposeDropped(service);
		using Bitmap paint = new();
		StrongBox<int> value = new(0);

		service.Execute(paint);
		service.Execute(Increment(value));
		service.Execute(paint);

		Assert.AreEqual(1, service.DropOldest(1));
		Assert.IsEmpty(dropped);
		service.Undo();
		Assert.AreEqual(0, service.CurrentPosition);
	}

	[TestMethod]
	public void DropOldest_ReportsEachInstanceOnce()
	{
		UndoRedoService service = Service(UndoRedoOptions.Create(autoMerge: false));
		List<ICommand> dropped = DisposeDropped(service);
		using Bitmap paint = new();
		StrongBox<int> value = new(0);

		service.Execute(paint);
		service.Execute(paint);
		service.Execute(Increment(value));

		Assert.AreEqual(2, service.DropOldest(2));
		CollectionAssert.AreEqual(new ICommand[] { paint }, dropped);
	}

	[TestMethod]
	public void RestoreTrim_OfAnInstanceStillInTheHistory_DoesNotReportIt()
	{
		using Bitmap paint = new();
		StrongBox<int> value = new(0);
		UndoRedoStackState state = new([paint, Increment(value), paint], 2, [], "test", DateTime.UtcNow);
		UndoRedoService service = Service(UndoRedoOptions.Create(maxStackSize: 2, autoMerge: false));
		List<ICommand> dropped = DisposeDropped(service);

		Assert.IsTrue(service.RestoreFromState(state));

		Assert.HasCount(2, service.Commands);
		Assert.IsEmpty(dropped);
		service.Undo();
		Assert.AreEqual(0, service.CurrentPosition);
	}
}
