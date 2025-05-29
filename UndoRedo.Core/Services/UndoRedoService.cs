// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

using ktsu.UndoRedo.Core.Contracts;
using ktsu.UndoRedo.Core.Models;

namespace ktsu.UndoRedo.Core.Services;

/// <summary>
/// Main service implementation for undo/redo operations
/// </summary>
public sealed class UndoRedoService : IUndoRedoService
{
	private readonly IStackManager _stackManager;
	private readonly ISaveBoundaryManager _saveBoundaryManager;
	private readonly ICommandMerger _commandMerger;
	private readonly UndoRedoOptions _options;
	private INavigationProvider? _navigationProvider;

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
	/// Creates a new undo/redo service
	/// </summary>
	/// <param name="stackManager">Stack manager service</param>
	/// <param name="saveBoundaryManager">Save boundary manager service</param>
	/// <param name="commandMerger">Command merger service</param>
	/// <param name="options">Configuration options</param>
	/// <param name="navigationProvider">Optional navigation provider</param>
	public UndoRedoService(
		IStackManager stackManager,
		ISaveBoundaryManager saveBoundaryManager,
		ICommandMerger commandMerger,
		UndoRedoOptions? options = null,
		INavigationProvider? navigationProvider = null)
	{
		_stackManager = stackManager ?? throw new ArgumentNullException(nameof(stackManager));
		_saveBoundaryManager = saveBoundaryManager ?? throw new ArgumentNullException(nameof(saveBoundaryManager));
		_commandMerger = commandMerger ?? throw new ArgumentNullException(nameof(commandMerger));
		_options = options ?? UndoRedoOptions.Default;
		_navigationProvider = navigationProvider;
	}

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
			var lastCommand = _stackManager.GetCurrentCommand();
			if (lastCommand != null && _commandMerger.CanMerge(lastCommand, command))
			{
				var mergedCommand = _commandMerger.Merge(lastCommand, command);

				// Replace the last command with the merged one
				var position = _stackManager.CurrentPosition;
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
			var removedCount = _stackManager.TrimToSize(_options.MaxStackSize);
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
		var command = _stackManager.MovePrevious();
		if (command == null)
		{
			return false;
		}

		command.Undo();
		CommandUndone?.Invoke(this, new CommandUndoneEventArgs(command, _stackManager.CurrentPosition));

		if (navigateToChange && _options.EnableNavigation && _navigationProvider != null && !string.IsNullOrEmpty(command.NavigationContext))
		{
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
		var command = _stackManager.MoveNext();
		if (command == null)
		{
			return false;
		}

		command.Execute();
		CommandRedone?.Invoke(this, new CommandRedoneEventArgs(command, _stackManager.CurrentPosition));

		if (navigateToChange && _options.EnableNavigation && _navigationProvider != null && !string.IsNullOrEmpty(command.NavigationContext))
		{
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
		var saveBoundary = _saveBoundaryManager.CreateSaveBoundary(_stackManager.CurrentPosition, description);
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
			var command = _stackManager.MovePrevious();
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
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
		var commands = _stackManager.Commands;
		var currentPosition = _stackManager.CurrentPosition;
		var saveBoundaries = _saveBoundaryManager.SaveBoundaries;

		return commands
			.Take(Math.Min(commands.Count, maxItems))
			.Select((cmd, index) => new ChangeVisualization(
				cmd,
				index,
				index <= currentPosition,
				saveBoundaries.Any(sb => sb.Position == index)
			));
	}
}
