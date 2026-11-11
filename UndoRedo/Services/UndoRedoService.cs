// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Core.Services;

using System.Runtime.ExceptionServices;
using System.Text.Json;
using ktsu.UndoRedo.Contracts;
using ktsu.UndoRedo.Models;

/// <summary>
/// Main service implementation for undo/redo operations
/// </summary>
/// <remarks>
/// Creates a new undo/redo service
/// </remarks>
/// <param name="stackManager">Stack manager service</param>
/// <param name="saveBoundaryManager">Save boundary manager service</param>
/// <param name="commandMerger">Command merger service</param>
/// <param name="options">Configuration options</param>
/// <param name="navigationProvider">Optional navigation provider</param>
public sealed class UndoRedoService(
	IStackManager stackManager,
	ISaveBoundaryManager saveBoundaryManager,
	ICommandMerger commandMerger,
	UndoRedoOptions? options = null,
	INavigationProvider? navigationProvider = null) : IUndoRedoService
{
	private readonly IStackManager _stackManager = Ensure.NotNull(stackManager);
	private readonly ISaveBoundaryManager _saveBoundaryManager = Ensure.NotNull(saveBoundaryManager);
	private readonly ICommandMerger _commandMerger = Ensure.NotNull(commandMerger);
	private readonly UndoRedoOptions _options = options ?? UndoRedoOptions.Default;
	private INavigationProvider? _navigationProvider = navigationProvider;
	private IUndoRedoSerializer? _serializer;

	// Set by anything that moves the position other than executing, so the next Execute starts a new
	// command instead of merging into the one the user undid or redid to, or one restored from saved state
	private bool _mergeChainBroken;

	/// <summary>
	/// Creates a service over the default <see cref="StackManager"/>, <see cref="SaveBoundaryManager"/> and
	/// <see cref="CommandMerger"/>, which is what <c>services.AddUndoRedo()</c> registers
	/// </summary>
	/// <param name="options">Configuration options, or null for <see cref="UndoRedoOptions.Default"/></param>
	/// <param name="navigationProvider">Optional navigation provider</param>
	/// <returns>A new, empty undo/redo service</returns>
	public static UndoRedoService Create(UndoRedoOptions? options = null, INavigationProvider? navigationProvider = null) =>
		new(new StackManager(), new SaveBoundaryManager(), new CommandMerger(), options, navigationProvider);

	/// <inheritdoc />
	public bool CanUndo => _stackManager.GetCurrentCommand() is { } command && command is not IPlaceholderCommand;

	/// <inheritdoc />
	public bool CanRedo => NextCommand() is { } command && command is not IPlaceholderCommand;

	/// <inheritdoc />
	public int CurrentPosition => _stackManager.CurrentPosition;

	/// <inheritdoc />
	public int CommandCount => _stackManager.Commands.Count;

	/// <inheritdoc />
	public bool HasUnsavedChanges => _saveBoundaryManager.HasUnsavedChanges(_stackManager.CurrentPosition);

	/// <inheritdoc />
	public IReadOnlyList<SaveBoundary> SaveBoundaries => _saveBoundaryManager.SaveBoundaries;

	/// <inheritdoc />
	public IReadOnlyList<ICommand> Commands => _stackManager.Commands;

	/// <inheritdoc />
	public event EventHandler<CommandExecutedEventArgs>? CommandExecuted;

	/// <inheritdoc />
	public event EventHandler<CommandUndoneEventArgs>? CommandUndone;

	/// <inheritdoc />
	public event EventHandler<CommandRedoneEventArgs>? CommandRedone;

	/// <inheritdoc />
	public event EventHandler<SaveBoundaryCreatedEventArgs>? SaveBoundaryCreated;

	/// <inheritdoc />
	public event EventHandler<CommandsDroppedEventArgs>? CommandsDropped;

	/// <inheritdoc />
	public event EventHandler<CommandsDiscardedEventArgs>? CommandsDiscarded;

	/// <inheritdoc />
	public event EventHandler? StateChanged;

	/// <summary>
	/// Raises <see cref="StateChanged"/> after an operation has changed the history
	/// </summary>
	private void OnStateChanged(HandlerErrors errors) => errors.Raise(StateChanged, this, EventArgs.Empty);

	/// <summary>
	/// Raises <see cref="CommandsDiscarded"/> for those of <paramref name="commands"/> that have really
	/// left the history, if there are any
	/// </summary>
	/// <remarks>
	/// One command instance can fill more than one slot of the history, because nothing stops a caller
	/// executing the same instance again. Removing one slot then leaves the instance in another, and a
	/// handler that disposed it would break the next Undo or Redo. So a command is reported only once no
	/// slot holds it any more, and only once per operation, however many of its slots left. A composite
	/// in the history holds its sub-commands too, so none of them is reported while it stays.
	/// </remarks>
	/// <param name="errors">Collects exceptions thrown by handlers</param>
	/// <param name="commands">The commands removed from their slots</param>
	/// <param name="reason">Why they were removed</param>
	/// <param name="reported">
	/// The commands already reported by this operation, for an operation that raises the event more than
	/// once. Those reported now are added to it.
	/// </param>
	private void OnCommandsDiscarded(HandlerErrors errors, List<ICommand> commands, CommandDiscardReason reason, HashSet<ICommand>? reported = null)
	{
		if (commands.Count == 0)
		{
			return;
		}

		HashSet<ICommand> unavailable = ReachableFromHistory();
		if (reported is not null)
		{
			unavailable.UnionWith(reported);
		}

		// Add() is false for a command still in the history, already reported, or met earlier in this list
		List<ICommand> leaving = [.. commands.Where(unavailable.Add)];
		reported?.UnionWith(leaving);
		if (leaving.Count > 0)
		{
			errors.Raise(CommandsDiscarded, this, new CommandsDiscardedEventArgs(leaving, reason, _stackManager.CurrentPosition));
		}
	}

	/// <summary>
	/// Raises <see cref="CommandsDropped"/> for the trim that removed <paramref name="dropped"/>, listing
	/// each instance once and only those no longer in the history
	/// </summary>
	/// <remarks>
	/// The event is raised for every trim, since the history and positions have changed, but an instance
	/// that still fills a newer slot, or sits inside a composite that does, is left out: a handler releasing
	/// what it is given would otherwise break the next Undo or Redo. This is the same rule
	/// <see cref="OnCommandsDiscarded"/> applies.
	/// </remarks>
	/// <param name="errors">Collects exceptions thrown by handlers</param>
	/// <param name="dropped">The commands removed from their slots, oldest first</param>
	private void OnCommandsDropped(HandlerErrors errors, List<ICommand> dropped)
	{
		// Add() is false for a command still in the history or met earlier in this list
		HashSet<ICommand> unavailable = ReachableFromHistory();
		List<ICommand> leaving = [.. dropped.Where(unavailable.Add)];
		errors.Raise(CommandsDropped, this, new CommandsDroppedEventArgs(leaving, _stackManager.CurrentPosition));
	}

	/// <summary>
	/// Every command the history can still run: its top-level commands, and the sub-commands of any
	/// composite among them, at any depth
	/// </summary>
	/// <remarks>
	/// A command held by a composite in the history runs on that composite's next Undo or Redo, so it
	/// is still in the history even though it fills no slot of its own.
	/// </remarks>
	private HashSet<ICommand> ReachableFromHistory()
	{
		HashSet<ICommand> reachable = new(ReferenceComparer.Instance);
		Stack<ICommand> pending = new(_stackManager.Commands);
		while (pending.Count > 0)
		{
			ICommand command = pending.Pop();

			// Add() is false for a command already walked, so a composite met twice is walked once
			if (reachable.Add(command) && command is CompositeCommand composite)
			{
				foreach (ICommand sub in composite.Commands)
				{
					pending.Push(sub);
				}
			}
		}

		return reachable;
	}

	/// <summary>
	/// Compares commands by identity, whatever equality a command type defines for itself
	/// </summary>
	private sealed class ReferenceComparer : IEqualityComparer<ICommand>
	{
		public static readonly ReferenceComparer Instance = new();

		public bool Equals(ICommand? x, ICommand? y) => ReferenceEquals(x, y);

		public int GetHashCode(ICommand obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
	}

	private ICommand? NextCommand()
	{
		int next = _stackManager.CurrentPosition + 1;
		return next >= 0 && next < _stackManager.Commands.Count ? _stackManager.Commands[next] : null;
	}

	// The commands after the current position, which executing anything throws away
	private List<ICommand> RedoTail() => [.. _stackManager.Commands.Skip(_stackManager.CurrentPosition + 1)];

	/// <summary>
	/// Sets the navigation provider
	/// </summary>
	/// <param name="navigationProvider">The navigation provider</param>
	public void SetNavigationProvider(INavigationProvider? navigationProvider) => _navigationProvider = navigationProvider;

	private bool IsAtSaveBoundary(int position) => _saveBoundaryManager.SaveBoundaries.Any(boundary => boundary.Position == position);

	/// <inheritdoc />
	public void Execute(ICommand command)
	{
		Ensure.NotNull(command);

		// Try to merge with the last command if auto-merge is enabled. Never merge into a command that
		// ends at a save boundary: that would fold the new edit into the saved state and delete the
		// boundary, so Undo could never return to what was saved. Nor into one the user undid or redid
		// to: that is a state they chose, and merging would leave Undo unable to return to it.
		if (_options.AutoMergeCommands && !_mergeChainBroken && _stackManager.CanUndo && !IsAtSaveBoundary(_stackManager.CurrentPosition))
		{
			ICommand? lastCommand = _stackManager.GetCurrentCommand();
			if (lastCommand is not null and not IPlaceholderCommand && _commandMerger.CanMerge(lastCommand, command)
				&& TryExecuteMerged(lastCommand, command))
			{
				return;
			}
		}

		// Apply the real state change before discarding anything, so a failure here can never throw
		// away redo history for a command that was not actually applied. This mirrors the merge path
		// and CompositeCommand.Execute().
		command.Execute();
		_mergeChainBroken = false;

		// Clear any commands after the current position and cleanup save boundaries
		List<ICommand> branched = RedoTail();
		_stackManager.ClearForward();
		_saveBoundaryManager.CleanupInvalidBoundaries(_stackManager.CurrentPosition);

		// Add to stack
		_stackManager.AddCommand(command);

		// Trim stack if needed. The new command is the current one, so everything below it is applied.
		List<ICommand> dropped = [];
		if (_options.MaxStackSize > 0 && _stackManager.Commands.Count > _options.MaxStackSize)
		{
			dropped = RemoveOldest(_stackManager.Commands.Count - _options.MaxStackSize);
		}

		HandlerErrors errors = new();
		HashSet<ICommand> reported = new(ReferenceComparer.Instance);
		errors.Raise(CommandExecuted, this, new CommandExecutedEventArgs(command, _stackManager.CurrentPosition));
		OnCommandsDiscarded(errors, branched, CommandDiscardReason.Branched, reported);
		if (dropped.Count > 0)
		{
			OnCommandsDropped(errors, dropped);
			OnCommandsDiscarded(errors, dropped, CommandDiscardReason.Trimmed, reported);
		}

		OnStateChanged(errors);
		errors.ThrowIfAny();
	}

	/// <summary>
	/// Merges <paramref name="command"/> into <paramref name="lastCommand"/> and executes the result
	/// </summary>
	/// <returns>
	/// False, having changed nothing, when <paramref name="lastCommand"/> can't be undone to merge into.
	/// Merging is only an optimisation, so the caller then executes <paramref name="command"/> on its own.
	/// </returns>
	private bool TryExecuteMerged(ICommand lastCommand, ICommand command)
	{
		// Apply the real state change before touching the stack, so a failure here can never
		// leave the stack recording a command that was not actually applied. This mirrors the
		// non-merge path and CompositeCommand.Execute().
		// Undo before merging: a MergeWith that merges in place and returns `this` changes what
		// lastCommand would undo, so undoing afterwards would revert changes never applied.
#pragma warning disable CA1031 // Do not catch general exception types
		try
		{
			lastCommand.Undo();
		}
		catch (Exception)
		{
			// Nothing has been mutated and the stack is still accurate, but the new edit must not be lost
			// to the previous command's failure. Once it is executed on its own, lastCommand is no longer
			// the current command, so the next edit can't retry this merge.
			return false;
		}

		ICommand? mergedCommand = null;
		try
		{
			// Execute has already asked whether these merge, with lastCommand applied. The built-in merger
			// would ask again, now with lastCommand undone, and a CanMergeWith that reads the document can
			// answer differently, so it is skipped and MergeWith called directly.
			mergedCommand = _commandMerger is CommandMerger
				? lastCommand.MergeWith(command)
				: _commandMerger.Merge(lastCommand, command);
			mergedCommand.Execute();
		}
		catch (Exception)
		{
			// The merged edit failed, so put the application back where the stack says it is. That is only
			// possible while lastCommand is still the original edit: a MergeWith that merged in place and
			// returned it has already changed what it applies.
			bool restored = false;
			if (!ReferenceEquals(mergedCommand, lastCommand))
			{
				try
				{
					lastCommand.Execute();
					restored = true;
				}
				catch (Exception)
				{
					// Handled below: lastCommand is not applied, so it leaves the history
				}
			}

			if (!restored)
			{
				RemoveUnappliedCurrent(lastCommand);
			}

			throw; // Re-throw the original exception
		}
#pragma warning restore CA1031 // Do not catch general exception types

		// Replace the last command with the merged one
		List<ICommand> branched = RedoTail();
		_stackManager.MovePrevious(); // Move back to remove the last command
		_stackManager.ClearForward(); // Clear the old command
		_saveBoundaryManager.CleanupInvalidBoundaries(_stackManager.CurrentPosition);
		_stackManager.AddCommand(mergedCommand); // Add the merged command

		// The commands the merged one replaced, unless the history can still run them through it: the
		// merger returned one of them, or a composite that holds them
		List<ICommand> merged = [.. new[] { lastCommand, command }.Where(replaced => !CanReach(mergedCommand, replaced))];

		HandlerErrors errors = new();
		HashSet<ICommand> reported = new(ReferenceComparer.Instance);
		errors.Raise(CommandExecuted, this, new CommandExecutedEventArgs(mergedCommand, _stackManager.CurrentPosition));
		OnCommandsDiscarded(errors, branched, CommandDiscardReason.Branched, reported);
		OnCommandsDiscarded(errors, merged, CommandDiscardReason.Merged, reported);
		OnStateChanged(errors);
		errors.ThrowIfAny();
		return true;
	}

	/// <summary>
	/// Removes the current command after a failed merge left it undone and unable to be applied again,
	/// so the history describes what the application holds
	/// </summary>
	/// <remarks>
	/// Handler exceptions are not rethrown: the caller is already getting the exception that made the
	/// merge fail, which is the one it needs.
	/// </remarks>
	private void RemoveUnappliedCurrent(ICommand lastCommand)
	{
		List<ICommand> leaving = [lastCommand, .. RedoTail()];
		_stackManager.MovePrevious();
		_stackManager.ClearForward();
		_saveBoundaryManager.CleanupInvalidBoundaries(_stackManager.CurrentPosition);
		_mergeChainBroken = true;

		HandlerErrors errors = new();
		OnCommandsDiscarded(errors, leaving, CommandDiscardReason.MergeFailed);
		OnStateChanged(errors);
	}

	/// <summary>
	/// Whether <paramref name="target"/> is <paramref name="command"/> itself or a sub-command of it,
	/// at any depth of nested composites
	/// </summary>
	private static bool CanReach(ICommand command, ICommand target) =>
		ReferenceEquals(command, target)
		|| (command is CompositeCommand composite && composite.Commands.Any(sub => CanReach(sub, target)));

	/// <inheritdoc />
	public int DropOldest(int count)
	{
		if (count < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(count), count, "The number of commands to drop cannot be negative.");
		}

		int drop = Math.Min(count, _stackManager.CurrentPosition + 1);
		if (drop <= 0)
		{
			return 0;
		}

		List<ICommand> dropped = RemoveOldest(drop);
		HandlerErrors errors = new();
		OnCommandsDropped(errors, dropped);
		OnCommandsDiscarded(errors, dropped, CommandDiscardReason.Trimmed);
		OnStateChanged(errors);
		errors.ThrowIfAny();
		return dropped.Count;
	}

	/// <summary>
	/// Removes the oldest <paramref name="count"/> commands, all at or before the current position, and
	/// moves the save boundaries down with them
	/// </summary>
	/// <returns>The removed commands, oldest first</returns>
	private List<ICommand> RemoveOldest(int count)
	{
		List<ICommand> dropped = [.. _stackManager.Commands.Take(count)];
		if (count >= _stackManager.Commands.Count)
		{
			// TrimToSize keeps at least one command, so dropping the whole stack is a clear
			_stackManager.Clear();
		}
		else
		{
			_stackManager.TrimToSize(_stackManager.Commands.Count - count);
		}

		_saveBoundaryManager.AdjustPositions(-count);
		return dropped;
	}

	/// <inheritdoc />
	public async Task<bool> UndoAsync(bool navigateToChange = true, CancellationToken cancellationToken = default)
	{
		// A placeholder can't be undone, so report nothing to do rather than throwing on every attempt
		ICommand? command = _stackManager.GetCurrentCommand();
		if (command is null or IPlaceholderCommand)
		{
			return false;
		}

		// Undo the real state change before moving the position, so a command that throws leaves the
		// stack describing what is actually applied. This mirrors Execute() above.
		command.Undo();
		_stackManager.MovePrevious();
		_mergeChainBroken = true;
		HandlerErrors errors = new();
		errors.Raise(CommandUndone, this, new CommandUndoneEventArgs(command, _stackManager.CurrentPosition));
		OnStateChanged(errors);

		if (navigateToChange)
		{
			await NavigateSafelyAsync(command.NavigationContext, cancellationToken).ConfigureAwait(false);
		}

		errors.ThrowIfAny();
		return true;
	}

	/// <inheritdoc />
	public bool Undo() => UndoAsync(navigateToChange: false).GetAwaiter().GetResult();

	/// <inheritdoc />
	public async Task<bool> RedoAsync(bool navigateToChange = true, CancellationToken cancellationToken = default)
	{
		ICommand? command = NextCommand();
		if (command is null or IPlaceholderCommand)
		{
			return false;
		}

		// Reapply the real state change before moving the position, so a command that throws can still
		// be redone and is never undone without having been applied. This mirrors Execute() above.
		command.Execute();
		_stackManager.MoveNext();
		_mergeChainBroken = true;
		HandlerErrors errors = new();
		errors.Raise(CommandRedone, this, new CommandRedoneEventArgs(command, _stackManager.CurrentPosition));
		OnStateChanged(errors);

		if (navigateToChange)
		{
			await NavigateSafelyAsync(command.NavigationContext, cancellationToken).ConfigureAwait(false);
		}

		errors.ThrowIfAny();
		return true;
	}

	/// <inheritdoc />
	public bool Redo() => RedoAsync(navigateToChange: false).GetAwaiter().GetResult();

	/// <inheritdoc />
	public void MarkAsSaved(string? description = null)
	{
		SaveBoundary saveBoundary = _saveBoundaryManager.CreateSaveBoundary(_stackManager.CurrentPosition, description);
		HandlerErrors errors = new();
		errors.Raise(SaveBoundaryCreated, this, new SaveBoundaryCreatedEventArgs(saveBoundary));
		OnStateChanged(errors);
		errors.ThrowIfAny();
	}

	/// <inheritdoc />
	public void Clear()
	{
		// Clearing the history doesn't change the document, so it is exactly as saved or unsaved as before
		bool clean = !HasUnsavedChanges;
		List<ICommand> cleared = [.. _stackManager.Commands];
		_stackManager.Clear();
		_saveBoundaryManager.Clear();
		_saveBoundaryManager.SetInitialStateClean(clean);
		_mergeChainBroken = true;

		HandlerErrors errors = new();
		OnCommandsDiscarded(errors, cleared, CommandDiscardReason.Cleared);
		OnStateChanged(errors);
		errors.ThrowIfAny();
	}

	/// <inheritdoc />
	public IEnumerable<ICommand> GetCommandsToUndo(SaveBoundary saveBoundary)
	{
		Ensure.NotNull(saveBoundary);

		SaveBoundary? liveBoundary = FindLiveSaveBoundary(saveBoundary);
		return liveBoundary == null
			? []
			: _saveBoundaryManager.GetCommandsToUndo(liveBoundary, _stackManager.CurrentPosition, _stackManager.Commands);
	}

	/// <inheritdoc />
	public async Task<bool> UndoToSaveBoundaryAsync(SaveBoundary saveBoundary, bool navigateToLastChange = true, CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(saveBoundary);

		// A boundary the caller has held since before the stack was trimmed carries a stale position,
		// and one removed by branching or clearing no longer marks a reachable saved state
		SaveBoundary? liveBoundary = FindLiveSaveBoundary(saveBoundary);
		if (liveBoundary == null || _stackManager.CurrentPosition == liveBoundary.Position)
		{
			return false;
		}

		// The commands between here and the boundary, in the order they are undone or redone. A placeholder
		// among them can't be stepped through, so refuse before changing anything.
		int current = _stackManager.CurrentPosition;
		bool forward = current < liveBoundary.Position;
		List<ICommand> path = forward
			? [.. _stackManager.Commands.Skip(current + 1).Take(liveBoundary.Position - current)]
			: [.. _stackManager.Commands.Skip(liveBoundary.Position + 1).Take(current - liveBoundary.Position).Reverse()];
		if (path.Any(command => command is IPlaceholderCommand))
		{
			return false;
		}

		_mergeChainBroken = true;
		HandlerErrors errors = new();
		ICommand? lastCommand = null;
		Exception? commandFailure = null;
		try
		{
			foreach (ICommand command in path)
			{
				// Stop at the first failure with the position still where the failed command left it
				if (forward)
				{
					command.Execute();
					_stackManager.MoveNext();
					errors.Raise(CommandRedone, this, new CommandRedoneEventArgs(command, _stackManager.CurrentPosition));
				}
				else
				{
					command.Undo();
					_stackManager.MovePrevious();
					errors.Raise(CommandUndone, this, new CommandUndoneEventArgs(command, _stackManager.CurrentPosition));
				}

				lastCommand = command;
			}
		}
#pragma warning disable CA1031 // Do not catch general exception types
		catch (Exception ex)
		{
			// Thrown below, once StateChanged has run, together with what the handlers of the steps
			// already taken threw, so a command failure can't hide a handler failure
			commandFailure = ex;
		}
#pragma warning restore CA1031 // Do not catch general exception types

		// Once for the whole walk, including the commands moved past before one that failed
		if (lastCommand != null)
		{
			OnStateChanged(errors);
		}

		if (commandFailure != null)
		{
			errors.ThrowWithCommandFailure(commandFailure);
		}

		if (navigateToLastChange && lastCommand != null)
		{
			await NavigateSafelyAsync(lastCommand.NavigationContext, cancellationToken).ConfigureAwait(false);
		}

		errors.ThrowIfAny();
		return true;
	}

	/// <summary>
	/// Resolves a save boundary, which may have been obtained before the stack was trimmed, to the live
	/// boundary for the same save point, or null if that save point no longer exists
	/// </summary>
	private SaveBoundary? FindLiveSaveBoundary(SaveBoundary saveBoundary) =>
		_saveBoundaryManager.SaveBoundaries.FirstOrDefault(boundary => boundary.IsSameSavePointAs(saveBoundary));

	/// <summary>
	/// The longest navigation timeout <see cref="Task.Delay(TimeSpan, CancellationToken)"/> accepts on every target framework.
	/// </summary>
	private static readonly TimeSpan MaxNavigationTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

	/// <summary>
	/// How long a cancelled navigation is given to stop before it is abandoned.
	/// </summary>
	private static readonly TimeSpan CancellationGrace = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Navigates to where a change was made, after the undo or redo has already been applied.
	/// Navigation is best effort: any failure is swallowed, because an exception here would tell the
	/// caller the undo or redo did not happen when it did, and a retry would then undo a second command.
	/// </summary>
	private async Task NavigateSafelyAsync(string? navigationContext, CancellationToken cancellationToken)
	{
		if (!_options.EnableNavigation || _navigationProvider == null || string.IsNullOrEmpty(navigationContext))
		{
			return;
		}

		try
		{
			// A context the provider reports as unreachable, such as a closed document or a deleted element,
			// is skipped rather than handed to NavigateToAsync
			if (!_navigationProvider.IsValidContext(navigationContext!))
			{
				return;
			}

			using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

			// Task.Delay throws for negative delays and for delays past its limit, which is
			// int.MaxValue ms on older targets. Treat those as "no timeout" rather than failing here.
			TimeSpan timeout = _options.EffectiveNavigationTimeout;
			Task navigation = _navigationProvider.NavigateToAsync(navigationContext!, cts.Token);
			if (timeout <= TimeSpan.Zero || timeout > MaxNavigationTimeout)
			{
				await navigation.ConfigureAwait(false);
				return;
			}

			// A provider that ignores the token, such as one awaiting a UI dispatcher, would otherwise hold
			// the caller past the timeout, so stop waiting for it rather than relying on it to cancel
			Task delay = Task.Delay(timeout, cts.Token);
			Task finished = await Task.WhenAny(navigation, delay).ConfigureAwait(false);
			await cts.CancelAsync().ConfigureAwait(false);

			// A provider that observes the token stops as soon as it is cancelled, so give it a moment to
			// do that before abandoning it
			if (finished != navigation)
			{
				finished = await Task.WhenAny(navigation, Task.Delay(CancellationGrace, CancellationToken.None)).ConfigureAwait(false);
			}

			if (finished == navigation)
			{
				await navigation.ConfigureAwait(false);
				return;
			}

			// Observe whatever the abandoned navigation throws later, so it doesn't go unobserved
			_ = navigation.ContinueWith(task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
		}
#pragma warning disable CA1031 // Do not catch general exception types
		catch (Exception)
		{
			// Navigation timeout, cancellation or provider failure - not critical
		}
#pragma warning restore CA1031 // Do not catch general exception types
	}

	/// <inheritdoc />
	public IEnumerable<ChangeVisualization> GetChangeVisualizations(int maxItems = 50)
	{
		IReadOnlyList<ICommand> commands = _stackManager.Commands;
		int currentPosition = _stackManager.CurrentPosition;
		IReadOnlyList<SaveBoundary> saveBoundaries = _saveBoundaryManager.SaveBoundaries;

		if (maxItems <= 0 || commands.Count == 0)
		{
			return [];
		}

		// Show the most recent commands, unless the current position has been undone back past
		// them; then centre the window on it so both its history and its redo commands show.
		int take = Math.Min(commands.Count, maxItems);
		int start = commands.Count - take;
		if (currentPosition < start)
		{
			start = Math.Max(0, currentPosition - (take / 2));
		}

		return [.. commands
			.Skip(start)
			.Take(take)
			.Select((cmd, offset) =>
			{
				int index = start + offset;
				return new ChangeVisualization(
					cmd,
					index,
					index <= currentPosition,
					saveBoundaries.Any(sb => sb.Position == index));
			})];
	}

	/// <inheritdoc />
	public void SetSerializer(IUndoRedoSerializer? serializer) => _serializer = serializer;

	/// <inheritdoc />
	public async Task<byte[]> SaveStateAsync(CancellationToken cancellationToken = default)
	{
		return _serializer switch
		{
			null => throw new InvalidOperationException("No serializer configured. Call SetSerializer() first."),
			IUndoRedoStateSerializer stateSerializer => await stateSerializer.SerializeAsync(GetCurrentState(), cancellationToken).ConfigureAwait(false),
			_ => await _serializer.SerializeAsync(
				_stackManager.Commands,
				_stackManager.CurrentPosition,
				_saveBoundaryManager.SaveBoundaries,
				_saveBoundaryManager.InitialStateIsClean,
				cancellationToken).ConfigureAwait(false),
		};
	}

	/// <inheritdoc />
	public async Task<bool> LoadStateAsync(byte[] data, CancellationToken cancellationToken = default)
	{
		if (_serializer == null)
		{
			throw new InvalidOperationException("No serializer configured. Call SetSerializer() first.");
		}

		try
		{
			UndoRedoStackState? state = await _serializer.DeserializeAsync(data, cancellationToken).ConfigureAwait(false);
			return state != null && RestoreFromState(state);
		}
		catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
		{
			return false;
		}
	}

	/// <inheritdoc />
	public UndoRedoStackState GetCurrentState() => new(
		[.. _stackManager.Commands],
		_stackManager.CurrentPosition,
		[.. _saveBoundaryManager.SaveBoundaries],
		"1.0", // Format version
		DateTime.UtcNow
	)
	{
		InitialStateIsClean = _saveBoundaryManager.InitialStateIsClean,

		// ISaveBoundaryManager can't say which boundary is the latest, so for a custom manager the last
		// one in the list stands for it, as restoring has always assumed
		LatestSaveBoundaryIndex = _saveBoundaryManager is SaveBoundaryManager builtInManager
			? builtInManager.LatestSaveBoundaryIndex
			: _saveBoundaryManager.SaveBoundaries.Count - 1,
	};

	/// <inheritdoc />
	public bool RestoreFromState(UndoRedoStackState state)
	{
		Ensure.NotNull(state);

		// Validate everything before clearing, so a state that cannot be loaded leaves the current
		// history untouched instead of wiping it and then failing partway through.
		if (!IsRestorable(state))
		{
			return false;
		}

		// Copy the lists before clearing. A state built from the live Commands or SaveBoundaries
		// views would otherwise be emptied by the Clear() calls below.
		List<ICommand> commands = [.. state.Commands];
		List<SaveBoundary> saveBoundaries = [.. state.SaveBoundaries];

		// The commands leaving the history: those the restored state doesn't hold as well
		List<ICommand> replaced = [.. _stackManager.Commands.Where(command => !commands.Any(restored => ReferenceEquals(restored, command)))];
		_mergeChainBroken = true;
		HandlerErrors errors = new();

		try
		{
			_stackManager.Clear();
			_saveBoundaryManager.Clear();

			foreach (ICommand command in commands)
			{
				_stackManager.AddCommand(command);
			}

			// Move to the correct position by undoing/redoing as needed
			while (_stackManager.CurrentPosition > state.CurrentPosition && _stackManager.CanUndo)
			{
				_stackManager.MovePrevious();
			}
			while (_stackManager.CurrentPosition < state.CurrentPosition && _stackManager.CanRedo)
			{
				_stackManager.MoveNext();
			}

			// Clear() reset this to true. Restore it before the boundaries, which set it to false.
			_saveBoundaryManager.SetInitialStateClean(state.InitialStateIsClean);

			// Recreate save boundaries at the stored positions. The built-in manager keeps each one's
			// original timestamp; ISaveBoundaryManager has no member for that, so a custom manager
			// creates them afresh.
			foreach (SaveBoundary boundary in saveBoundaries)
			{
				if (_saveBoundaryManager is SaveBoundaryManager builtInManager)
				{
					builtInManager.RestoreSaveBoundary(boundary);
				}
				else
				{
					_saveBoundaryManager.CreateSaveBoundary(boundary.Position, boundary.Description);
				}
			}

			// Restoring made the last boundary the latest. State that records which one was, including
			// none because a branch removed it, says otherwise.
			if (state.LatestSaveBoundaryIndex is int latest && _saveBoundaryManager is SaveBoundaryManager restoredManager)
			{
				restoredManager.RestoreLatestSaveBoundary(latest);
			}
		}
		catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
		{
			// The history was already cleared, so it has changed even though the restore failed
			OnCommandsDiscarded(errors, replaced, CommandDiscardReason.Replaced);
			OnStateChanged(errors);
			errors.ThrowIfAny();
			return false;
		}

		// A history saved under a larger limit holds more than this service keeps. Trim it as Execute
		// would, but only from the commands at or before the current position: those after it are the
		// redo history the state chose to keep, and RemoveOldest only removes applied commands.
		List<ICommand> dropped = [];
		if (_options.MaxStackSize > 0 && _stackManager.Commands.Count > _options.MaxStackSize)
		{
			int drop = Math.Min(_stackManager.Commands.Count - _options.MaxStackSize, _stackManager.CurrentPosition + 1);
			if (drop > 0)
			{
				dropped = RemoveOldest(drop);
			}
		}

		HashSet<ICommand> reported = new(ReferenceComparer.Instance);
		OnCommandsDiscarded(errors, replaced, CommandDiscardReason.Replaced, reported);
		if (dropped.Count > 0)
		{
			OnCommandsDropped(errors, dropped);
			OnCommandsDiscarded(errors, dropped, CommandDiscardReason.Trimmed, reported);
		}

		OnStateChanged(errors);
		errors.ThrowIfAny();
		return true;
	}

	private static bool IsRestorable(UndoRedoStackState state) =>
		state.Commands is not null &&
		state.SaveBoundaries is not null &&
		!state.Commands.Any(command => command is null) &&
		!state.SaveBoundaries.Any(boundary => boundary is null) &&
		state.SaveBoundaries.All(boundary => boundary.Position >= -1 && boundary.Position < state.Commands.Count) &&
		state.CurrentPosition >= -1 &&
		state.CurrentPosition < state.Commands.Count;

	/// <summary>
	/// Raises events after an operation has changed the history, calling every handler even when one throws,
	/// so a failing handler can neither stop the others nor make the operation look as though it failed
	/// </summary>
	private sealed class HandlerErrors
	{
		private const string CommandAndHandlersFailedMessage = "A command failed partway through the operation, and one or more event handlers of the steps before it threw.";

		private List<Exception>? _exceptions;

		public void Raise<TEventArgs>(EventHandler<TEventArgs>? handler, object sender, TEventArgs args)
		{
			if (handler == null)
			{
				return;
			}

			foreach (EventHandler<TEventArgs> each in handler.GetInvocationList().Cast<EventHandler<TEventArgs>>())
			{
				Invoke(() => each(sender, args));
			}
		}

		public void Raise(EventHandler? handler, object sender, EventArgs args)
		{
			if (handler == null)
			{
				return;
			}

			foreach (EventHandler each in handler.GetInvocationList().Cast<EventHandler>())
			{
				Invoke(() => each(sender, args));
			}
		}

		/// <summary>
		/// Throws <see cref="UndoRedoHandlerException"/> if any handler threw, once the operation is complete
		/// </summary>
		public void ThrowIfAny()
		{
			if (_exceptions != null)
			{
				throw new UndoRedoHandlerException(_exceptions);
			}
		}

		/// <summary>
		/// Rethrows a command's failure unchanged when no handler threw, or throws an
		/// <see cref="AggregateException"/> holding the command's failure first and then what each failing
		/// handler threw
		/// </summary>
		/// <param name="commandFailure">What the command threw</param>
		public void ThrowWithCommandFailure(Exception commandFailure)
		{
			if (_exceptions != null)
			{
				throw new AggregateException(CommandAndHandlersFailedMessage, _exceptions.Prepend(commandFailure));
			}

			ExceptionDispatchInfo.Capture(commandFailure).Throw();
		}

		private void Invoke(Action handler)
		{
			try
			{
				handler();
			}
#pragma warning disable CA1031 // Do not catch general exception types
			catch (Exception ex)
			{
				// Reported by ThrowIfAny once the operation, and every other handler, has finished
				(_exceptions ??= []).Add(ex);
			}
#pragma warning restore CA1031 // Do not catch general exception types
		}
	}
}
