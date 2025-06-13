// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.UndoRedo.Core.Services;
using ktsu.UndoRedo.Core.Contracts;
using ktsu.UndoRedo.Core.Models;

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
	private readonly IStackManager _stackManager = stackManager ?? throw new ArgumentNullException(nameof(stackManager));
	private readonly ISaveBoundaryManager _saveBoundaryManager = saveBoundaryManager ?? throw new ArgumentNullException(nameof(saveBoundaryManager));
	private readonly ICommandMerger _commandMerger = commandMerger ?? throw new ArgumentNullException(nameof(commandMerger));
	private readonly UndoRedoOptions _options = options ?? UndoRedoOptions.Default;
	private INavigationProvider? _navigationProvider = navigationProvider;
	private IUndoRedoSerializer? _serializer;

	/// <inheritdoc />
	public bool CanUndo => _stackManager.CanUndo;

	/// <inheritdoc />
	public bool CanRedo => _stackManager.CanRedo;

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

	/// <summary>
	/// Sets the navigation provider
	/// </summary>
	/// <param name="navigationProvider">The navigation provider</param>
	public void SetNavigationProvider(INavigationProvider? navigationProvider) => _navigationProvider = navigationProvider;

	/// <inheritdoc />
	public void Execute(ICommand command)
	{
		ArgumentNullException.ThrowIfNull(command);

		// Try to merge with the last command if auto-merge is enabled
		if (_options.AutoMergeCommands && _stackManager.CanUndo)
		{
			ICommand? lastCommand = _stackManager.GetCurrentCommand();
			if (lastCommand != null && _commandMerger.CanMerge(lastCommand, command))
			{
				ICommand mergedCommand = _commandMerger.Merge(lastCommand, command);

				// Replace the last command with the merged one
				int position = _stackManager.CurrentPosition;
				_stackManager.MovePrevious(); // Move back to remove the last command
				_stackManager.ClearForward(); // Clear the old command
				_stackManager.AddCommand(mergedCommand); // Add the merged command

				// Execute the merged command
				mergedCommand.Execute();

				CommandExecuted?.Invoke(this, new CommandExecutedEventArgs(mergedCommand, _stackManager.CurrentPosition));
				return;
			}
		}

		// Clear any commands after the current position and cleanup save boundaries
		_stackManager.ClearForward();
		_saveBoundaryManager.CleanupInvalidBoundaries(_stackManager.CurrentPosition);

		// Execute the command
		command.Execute();

		// Add to stack
		_stackManager.AddCommand(command);

		// Trim stack if needed
		if (_options.MaxStackSize > 0)
		{
			int removedCount = _stackManager.TrimToSize(_options.MaxStackSize);
			if (removedCount > 0)
			{
				_saveBoundaryManager.AdjustPositions(-removedCount);
			}
		}

		CommandExecuted?.Invoke(this, new CommandExecutedEventArgs(command, _stackManager.CurrentPosition));
	}

	/// <inheritdoc />
	public async Task<bool> UndoAsync(bool navigateToChange = true, CancellationToken cancellationToken = default)
	{
		ICommand? command = _stackManager.MovePrevious();
		if (command == null)
		{
			return false;
		}

		command.Undo();
		CommandUndone?.Invoke(this, new CommandUndoneEventArgs(command, _stackManager.CurrentPosition));

		if (navigateToChange && _options.EnableNavigation && _navigationProvider != null && !string.IsNullOrEmpty(command.NavigationContext))
		{
			using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.CancelAfter(_options.EffectiveNavigationTimeout);

			try
			{
				await _navigationProvider.NavigateToAsync(command.NavigationContext, cts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Navigation timeout or cancellation - not critical
			}
		}

		return true;
	}

	/// <inheritdoc />
	public bool Undo() => UndoAsync(navigateToChange: false).GetAwaiter().GetResult();

	/// <inheritdoc />
	public async Task<bool> RedoAsync(bool navigateToChange = true, CancellationToken cancellationToken = default)
	{
		ICommand? command = _stackManager.MoveNext();
		if (command == null)
		{
			return false;
		}

		command.Execute();
		CommandRedone?.Invoke(this, new CommandRedoneEventArgs(command, _stackManager.CurrentPosition));

		if (navigateToChange && _options.EnableNavigation && _navigationProvider != null && !string.IsNullOrEmpty(command.NavigationContext))
		{
			using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.CancelAfter(_options.EffectiveNavigationTimeout);

			try
			{
				await _navigationProvider.NavigateToAsync(command.NavigationContext, cts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Navigation timeout or cancellation - not critical
			}
		}

		return true;
	}

	/// <inheritdoc />
	public bool Redo() => RedoAsync(navigateToChange: false).GetAwaiter().GetResult();

	/// <inheritdoc />
	public void MarkAsSaved(string? description = null)
	{
		SaveBoundary saveBoundary = _saveBoundaryManager.CreateSaveBoundary(_stackManager.CurrentPosition, description);
		SaveBoundaryCreated?.Invoke(this, new SaveBoundaryCreatedEventArgs(saveBoundary));
	}

	/// <inheritdoc />
	public void Clear()
	{
		_stackManager.Clear();
		_saveBoundaryManager.Clear();
	}

	/// <inheritdoc />
	public IEnumerable<ICommand> GetCommandsToUndo(SaveBoundary saveBoundary) =>
		_saveBoundaryManager.GetCommandsToUndo(saveBoundary, _stackManager.CurrentPosition, _stackManager.Commands);

	/// <inheritdoc />
	public async Task<bool> UndoToSaveBoundaryAsync(SaveBoundary saveBoundary, bool navigateToLastChange = true, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(saveBoundary);

		if (_stackManager.CurrentPosition <= saveBoundary.Position)
		{
			return false;
		}

		ICommand? lastCommand = null;
		while (_stackManager.CurrentPosition > saveBoundary.Position)
		{
			ICommand? command = _stackManager.MovePrevious();
			if (command == null)
			{
				break;
			}

			command.Undo();
			lastCommand = command;
			CommandUndone?.Invoke(this, new CommandUndoneEventArgs(command, _stackManager.CurrentPosition));
		}

		if (navigateToLastChange && lastCommand != null && _options.EnableNavigation && _navigationProvider != null &&
			!string.IsNullOrEmpty(lastCommand.NavigationContext))
		{
			using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.CancelAfter(_options.EffectiveNavigationTimeout);

			try
			{
				await _navigationProvider.NavigateToAsync(lastCommand.NavigationContext, cts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Navigation timeout or cancellation - not critical
			}
		}

		return true;
	}

	/// <inheritdoc />
	public IEnumerable<ChangeVisualization> GetChangeVisualizations(int maxItems = 50)
	{
		IReadOnlyList<ICommand> commands = _stackManager.Commands;
		int currentPosition = _stackManager.CurrentPosition;
		IReadOnlyList<SaveBoundary> saveBoundaries = _saveBoundaryManager.SaveBoundaries;

		return commands
			.Take(Math.Min(commands.Count, maxItems))
			.Select((cmd, index) => new ChangeVisualization(
				cmd,
				index,
				index <= currentPosition,
				saveBoundaries.Any(sb => sb.Position == index)
			));
	}

	/// <inheritdoc />
	public void SetSerializer(IUndoRedoSerializer? serializer) => _serializer = serializer;

	/// <inheritdoc />
	public async Task<byte[]> SaveStateAsync(CancellationToken cancellationToken = default)
	{
		return _serializer == null
			? throw new InvalidOperationException("No serializer configured. Call SetSerializer() first.")
			: await _serializer.SerializeAsync(
			_stackManager.Commands,
			_stackManager.CurrentPosition,
			_saveBoundaryManager.SaveBoundaries,
			cancellationToken).ConfigureAwait(false);
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
		catch
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
	);

	/// <inheritdoc />
	public bool RestoreFromState(UndoRedoStackState state)
	{
		ArgumentNullException.ThrowIfNull(state);

		try
		{
			_stackManager.Clear();
			_saveBoundaryManager.Clear();

			foreach (ICommand command in state.Commands)
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

			// Recreate save boundaries by creating them at the stored positions
			foreach (SaveBoundary boundary in state.SaveBoundaries)
			{
				_saveBoundaryManager.CreateSaveBoundary(boundary.Position, boundary.Description);
			}

			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}
}
