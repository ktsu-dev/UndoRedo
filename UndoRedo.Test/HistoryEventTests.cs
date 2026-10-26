// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Test;

using System.Diagnostics;
using ktsu.UndoRedo.Models;
using ktsu.UndoRedo.Core.Services;
using static ktsu.UndoRedo.Test.HistoryTestSupport;

/// <summary>
/// CommandsDiscarded, event handlers that throw, and a navigation provider that ignores its timeout
/// </summary>
[TestClass]
public sealed class HistoryEventTests
{
	[TestMethod]
	public void EveryCommandLeavingTheHistory_IsReportedOnce()
	{
		StrongBox<int> value = new(0);
		UndoRedoService service = Service();
		List<(ICommand Command, CommandDiscardReason Reason)> discarded = [];
		service.CommandsDiscarded += (_, e) => discarded.AddRange(e.Commands.Select(command => (command, e.Reason)));
		DelegateCommand a = Increment(value, "a");
		DelegateCommand b = Increment(value, "b");
		DelegateCommand c = Increment(value, "c");

		service.Execute(a);
		service.Execute(b);
		service.Undo();
		service.Execute(c);
		service.Clear();

		CollectionAssert.AreEqual(
			new (ICommand, CommandDiscardReason)[] { (b, CommandDiscardReason.Branched), (a, CommandDiscardReason.Cleared), (c, CommandDiscardReason.Cleared) },
			discarded);
	}

	[TestMethod]
	public void AMerge_ReportsTheCommandsItReplaced()
	{
		StrongBox<string> buffer = new(string.Empty);
		UndoRedoService service = Service(UndoRedoOptions.Create(autoMerge: true));
		List<ICommand> discarded = [];
		service.CommandsDiscarded += (_, e) =>
		{
			Assert.AreEqual(CommandDiscardReason.Merged, e.Reason);
			discarded.AddRange(e.Commands);
		};
		InsertText first = new(buffer, 0, "a");
		InsertText second = new(buffer, 1, "b");

		service.Execute(first);
		service.Execute(second);

		CollectionAssert.AreEqual(new ICommand[] { first, second }, discarded);
		Assert.IsFalse(discarded.Contains(service.Commands[0]));
	}

	[TestMethod]
	public void Trimming_IsReportedAsDroppedAndDiscarded()
	{
		StrongBox<int> value = new(0);
		UndoRedoService service = Service(UndoRedoOptions.Create(maxStackSize: 1));
		List<ICommand> dropped = [];
		List<ICommand> discarded = [];
		service.CommandsDropped += (_, e) => dropped.AddRange(e.Commands);
		service.CommandsDiscarded += (_, e) =>
		{
			Assert.AreEqual(CommandDiscardReason.Trimmed, e.Reason);
			discarded.AddRange(e.Commands);
		};
		DelegateCommand a = Increment(value);

		service.Execute(a);
		service.Execute(Increment(value));

		CollectionAssert.AreEqual(new ICommand[] { a }, dropped);
		CollectionAssert.AreEqual(new ICommand[] { a }, discarded);
	}

	[TestMethod]
	public async Task LoadingOverAHistory_ReportsTheCommandsItReplaced()
	{
		string register = AddToRegister.NewRegister();
		UndoRedoService saved = JsonService();
		saved.Execute(new AddToRegister(register, 1));
		byte[] data = await saved.SaveStateAsync().ConfigureAwait(false);

		StrongBox<int> value = new(0);
		UndoRedoService service = JsonService();
		DelegateCommand old = Increment(value);
		service.Execute(old);
		List<ICommand> discarded = [];
		service.CommandsDiscarded += (_, e) =>
		{
			Assert.AreEqual(CommandDiscardReason.Replaced, e.Reason);
			discarded.AddRange(e.Commands);
		};

		Assert.IsTrue(await service.LoadStateAsync(data).ConfigureAwait(false));

		CollectionAssert.AreEqual(new ICommand[] { old }, discarded);
	}

	[TestMethod]
	public void RestoringTheSameHistory_ReportsNothing()
	{
		StrongBox<int> value = new(0);
		UndoRedoService service = Service();
		service.Execute(Increment(value));
		int reports = 0;
		service.CommandsDiscarded += (_, _) => reports++;

		Assert.IsTrue(service.RestoreFromState(service.GetCurrentState()));

		Assert.AreEqual(0, reports);
	}

	[TestMethod]
	public async Task AThrowingHandler_DuringARevert_StillLandsOnTheSavePoint()
	{
		StrongBox<int> value = new(0);
		UndoRedoService service = Service();
		service.MarkAsSaved();
		for (int i = 0; i < 3; i++)
		{
			service.Execute(Increment(value));
		}

		int handled = 0;
		service.CommandUndone += (_, _) => throw new InvalidOperationException("refresh failed");
		service.CommandUndone += (_, _) => handled++;

		UndoRedoHandlerException thrown = await Assert.ThrowsExactlyAsync<UndoRedoHandlerException>(() => service.UndoToSaveBoundaryAsync(service.SaveBoundaries[0])).ConfigureAwait(false);

		Assert.AreEqual(-1, service.CurrentPosition);
		Assert.AreEqual(0, value.Value);
		Assert.AreEqual(3, handled, "Every other handler still runs");
		Assert.HasCount(3, thrown.InnerExceptions);
	}

	[TestMethod]
	public void AThrowingHandler_AfterExecute_LeavesTheCommandAppliedAndSaysSo()
	{
		StrongBox<int> value = new(0);
		UndoRedoService service = Service();
		service.CommandExecuted += (_, _) => throw new InvalidOperationException("refresh failed");

		Assert.ThrowsExactly<UndoRedoHandlerException>(() => service.Execute(Increment(value)));

		Assert.AreEqual(1, value.Value);
		Assert.AreEqual(1, service.CommandCount);
		Assert.IsTrue(service.CanUndo);
	}

	[TestMethod]
	public void AThrowingHandler_AfterUndo_LeavesItUndone()
	{
		StrongBox<int> value = new(0);
		UndoRedoService service = Service();
		service.Execute(Increment(value));
		service.Execute(Increment(value));
		service.StateChanged += (_, _) => throw new InvalidOperationException("refresh failed");

		Assert.ThrowsExactly<UndoRedoHandlerException>(() => service.Undo());

		Assert.AreEqual(1, value.Value);
		Assert.AreEqual(0, service.CurrentPosition);
	}

	private sealed class TokenIgnoringNavigation : INavigationProvider
	{
		public async Task<bool> NavigateToAsync(string context, CancellationToken cancellationToken = default)
		{
			await Task.Delay(TimeSpan.FromSeconds(10), CancellationToken.None).ConfigureAwait(false);
			return true;
		}

		public bool IsValidContext(string context) => true;
	}

	[TestMethod]
	public async Task ANavigationProviderThatIgnoresItsToken_IsAbandonedAtTheTimeout()
	{
		UndoRedoService service = new(
			new StackManager(),
			new SaveBoundaryManager(),
			new CommandMerger(),
			UndoRedoOptions.Create(navigationTimeout: TimeSpan.FromMilliseconds(200)),
			new TokenIgnoringNavigation());
		service.Execute(new DelegateCommand("inc", () => { }, () => { }, navigationContext: "file:1:1"));

		Stopwatch stopwatch = Stopwatch.StartNew();
		bool undone = await service.UndoAsync().ConfigureAwait(false);
		stopwatch.Stop();

		Assert.IsTrue(undone);
		Assert.IsLessThan(5000, stopwatch.ElapsedMilliseconds, $"UndoAsync took {stopwatch.ElapsedMilliseconds} ms with a 200 ms timeout");
	}
}
