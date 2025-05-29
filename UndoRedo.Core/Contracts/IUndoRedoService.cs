// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

using ktsu.UndoRedo.Core.Models;

namespace ktsu.UndoRedo.Core.Contracts;

/// <summary>
/// Main service interface for undo/redo operations
/// </summary>
public interface IUndoRedoService
{
	/// <summary>
	/// Gets whether there are commands available to undo
	/// </summary>
	public bool CanUndo { get; }

	/// <summary>
	/// Gets whether there are commands available to redo
	/// </summary>
	public bool CanRedo { get; }

	/// <summary>
	/// Gets the current position in the stack
	/// </summary>
	public int CurrentPosition { get; }

	/// <summary>
	/// Gets the total number of commands in the stack
	/// </summary>
	public int CommandCount { get; }

	/// <summary>
	/// Gets whether there are unsaved changes
	/// </summary>
	public bool HasUnsavedChanges { get; }

	/// <summary>
	/// Gets all save boundaries
	/// </summary>
	public IReadOnlyList<SaveBoundary> SaveBoundaries { get; }

	/// <summary>
	/// Gets all commands in the stack
	/// </summary>
	public IReadOnlyList<ICommand> Commands { get; }

	/// <summary>
	/// Fired when a command is executed
	/// </summary>
	public event EventHandler<CommandExecutedEventArgs>? CommandExecuted;

	/// <summary>
	/// Fired when a command is undone
	/// </summary>
	public event EventHandler<CommandUndoneEventArgs>? CommandUndone;

	/// <summary>
	/// Fired when a command is redone
	/// </summary>
	public event EventHandler<CommandRedoneEventArgs>? CommandRedone;

	/// <summary>
	/// Fired when a save boundary is created
	/// </summary>
	public event EventHandler<SaveBoundaryCreatedEventArgs>? SaveBoundaryCreated;

	/// <summary>
	/// Executes a command and adds it to the stack
	/// </summary>
	/// <param name="command">The command to execute</param>
	public void Execute(ICommand command);

	/// <summary>
	/// Undoes the last command
	/// </summary>
	/// <param name="navigateToChange">Whether to navigate to where the change was made</param>
	/// <param name="cancellationToken">Cancellation token for navigation</param>
	/// <returns>True if undo was successful</returns>
	public Task<bool> UndoAsync(bool navigateToChange = true, CancellationToken cancellationToken = default);

	/// <summary>
	/// Undoes the last command (synchronous version)
	/// </summary>
	/// <returns>True if undo was successful</returns>
	public bool Undo();

	/// <summary>
	/// Redoes the next command
	/// </summary>
	/// <param name="navigateToChange">Whether to navigate to where the change was made</param>
	/// <param name="cancellationToken">Cancellation token for navigation</param>
	/// <returns>True if redo was successful</returns>
	public Task<bool> RedoAsync(bool navigateToChange = true, CancellationToken cancellationToken = default);

	/// <summary>
	/// Redoes the next command (synchronous version)
	/// </summary>
	/// <returns>True if redo was successful</returns>
	public bool Redo();

	/// <summary>
	/// Creates a save boundary at the current position
	/// </summary>
	/// <param name="description">Optional description of what was saved</param>
	public void MarkAsSaved(string? description = null);

	/// <summary>
	/// Clears the entire stack and all save boundaries
	/// </summary>
	public void Clear();

	/// <summary>
	/// Gets commands that would be undone to reach the specified save boundary
	/// </summary>
	/// <param name="saveBoundary">The target save boundary</param>
	/// <returns>Commands that would be undone</returns>
	public IEnumerable<ICommand> GetCommandsToUndo(SaveBoundary saveBoundary);

	/// <summary>
	/// Undoes commands until reaching the specified save boundary
	/// </summary>
	/// <param name="saveBoundary">The target save boundary</param>
	/// <param name="navigateToLastChange">Whether to navigate to the last change</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>True if successful</returns>
	public Task<bool> UndoToSaveBoundaryAsync(SaveBoundary saveBoundary, bool navigateToLastChange = true, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets change visualization data for the commands in the stack
	/// </summary>
	/// <param name="maxItems">Maximum number of items to return</param>
	/// <returns>Visualization data for changes</returns>
	public IEnumerable<ChangeVisualization> GetChangeVisualizations(int maxItems = 50);
}
