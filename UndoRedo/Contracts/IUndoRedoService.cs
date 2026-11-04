// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Contracts;

using ktsu.UndoRedo.Models;

/// <summary>
/// Main service interface for undo/redo operations
/// </summary>
public interface IUndoRedoService
{
	/// <summary>
	/// Gets whether there are commands available to undo
	/// </summary>
	/// <remarks>
	/// <see langword="false"/> while the current command is an <see cref="IPlaceholderCommand"/>, which can't be undone.
	/// </remarks>
	public bool CanUndo { get; }

	/// <summary>
	/// Gets whether there are commands available to redo
	/// </summary>
	/// <remarks>
	/// <see langword="false"/> while the next command is an <see cref="IPlaceholderCommand"/>, which can't be redone.
	/// </remarks>
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
	/// <remarks>
	/// Every event is raised after the history has changed. A handler that throws neither stops the other
	/// handlers nor undoes the change: the operation finishes and then throws
	/// <see cref="UndoRedoHandlerException"/> with what each failing handler threw.
	/// </remarks>
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
	/// Fired when the oldest commands are dropped, by <see cref="DropOldest"/> or by
	/// <see cref="UndoRedoOptions.MaxStackSize"/>
	/// </summary>
	public event EventHandler<CommandsDroppedEventArgs>? CommandsDropped;

	/// <summary>
	/// Fired whenever commands leave the history, whatever the reason: trimmed as
	/// <see cref="CommandsDropped"/> reports, thrown away when a new edit follows an undo, replaced by a
	/// merge, cleared, or replaced by a restored or loaded history
	/// </summary>
	/// <remarks>
	/// Each command is reported once, and a command still in the history never is. Subscribe to this to
	/// release resources commands hold for undo or redo, such as bitmaps, buffers or file handles.
	/// </remarks>
	public event EventHandler<CommandsDiscardedEventArgs>? CommandsDiscarded;

	/// <summary>
	/// Fired once after any operation that changes the history: execute (including a merge), undo, redo,
	/// marking a save point, clearing, and a successful load or restore. Subscribe to this one event to
	/// keep Undo/Redo buttons and the unsaved-changes indicator in sync.
	/// </summary>
	public event EventHandler? StateChanged;

	/// <summary>
	/// Executes a command and adds it to the stack
	/// </summary>
	/// <remarks>
	/// With auto-merge on, the command is merged into the current one when they can merge, unless the
	/// current one ends at a save boundary or was reached by undo, redo, reverting, clearing or restoring
	/// rather than by executing it: those are states the user can return to with one undo.
	/// </remarks>
	/// <param name="command">The command to execute</param>
	/// <exception cref="UndoRedoHandlerException">An event handler threw after the command was executed and recorded</exception>
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
	/// Drops the oldest commands from the history, so they can no longer be undone
	/// </summary>
	/// <remarks>
	/// <para>
	/// Only commands at or before the current position are dropped: a command that has been undone is
	/// not applied, so the state before it is not the state after the commands below it, and dropping it
	/// from the start would leave the first position standing for a state that never existed. The count
	/// is clamped to <c>CurrentPosition + 1</c>, so this never drops a redoable command.
	/// </para>
	/// <para>
	/// Positions move down by the number dropped. A save boundary on the last dropped command lands on
	/// -1, which is the state the history now starts from; boundaries on earlier commands name states
	/// that can no longer be reached, and are removed. Once anything is dropped, -1 is no longer the
	/// clean initial state, so it counts as unsaved unless a save boundary is on it.
	/// </para>
	/// <para>
	/// Raises <see cref="CommandsDropped"/> and then <see cref="StateChanged"/> when anything is dropped,
	/// and neither when nothing is.
	/// </para>
	/// </remarks>
	/// <param name="count">How many of the oldest commands to drop</param>
	/// <returns>How many commands were dropped</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative</exception>
	public int DropOldest(int count);

	/// <summary>
	/// Clears the entire stack and all save boundaries
	/// </summary>
	/// <remarks>
	/// The document is unchanged, so <see cref="HasUnsavedChanges"/> stays what it was: true if there were
	/// unsaved changes, false if there weren't.
	/// </remarks>
	public void Clear();

	/// <summary>
	/// Gets commands that would be undone to reach the specified save boundary
	/// </summary>
	/// <remarks>
	/// Empty when the boundary is at or after the current position, where
	/// <see cref="UndoToSaveBoundaryAsync"/> redoes rather than undoes.
	/// </remarks>
	/// <param name="saveBoundary">The target save boundary</param>
	/// <returns>Commands that would be undone</returns>
	public IEnumerable<ICommand> GetCommandsToUndo(SaveBoundary saveBoundary);

	/// <summary>
	/// Undoes commands until reaching the specified save boundary, or redoes them when the user has undone
	/// back past it, so the history lands on the saved state either way
	/// </summary>
	/// <remarks>
	/// Returns <see langword="false"/> without changing anything when the boundary is no longer in the
	/// history, when the current position is already on it, or when an <see cref="IPlaceholderCommand"/> lies
	/// between here and there. <see cref="CommandUndone"/> or <see cref="CommandRedone"/> is raised for each step.
	/// </remarks>
	/// <param name="saveBoundary">The target save boundary</param>
	/// <param name="navigateToLastChange">Whether to navigate to the last change</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>True if successful</returns>
	public Task<bool> UndoToSaveBoundaryAsync(SaveBoundary saveBoundary, bool navigateToLastChange = true, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets change visualization data for the commands in the stack
	/// </summary>
	/// <remarks>
	/// When the stack holds more than <paramref name="maxItems"/> commands, the result is the most
	/// recent <paramref name="maxItems"/> commands. If the current position has been undone to before
	/// that window, the window is centred on the current position instead, so it always contains the
	/// current command along with the redoable commands after it. Each item's position is its
	/// absolute index in the stack. The result is a snapshot taken at the time of the call.
	/// </remarks>
	/// <param name="maxItems">Maximum number of items to return; zero or less returns no items</param>
	/// <returns>Visualization data for changes</returns>
	public IEnumerable<ChangeVisualization> GetChangeVisualizations(int maxItems = 50);

	/// <summary>
	/// Sets the serializer to use for persistent stack state
	/// </summary>
	/// <param name="serializer">The serializer to use</param>
	public void SetSerializer(IUndoRedoSerializer? serializer);

	/// <summary>
	/// Saves the current stack state using the configured serializer
	/// </summary>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Serialized stack state as byte array</returns>
	/// <exception cref="InvalidOperationException">Thrown when no serializer is configured</exception>
	public Task<byte[]> SaveStateAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Loads stack state from serialized data using the configured serializer
	/// </summary>
	/// <param name="data">The serialized stack state</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>True if state was loaded successfully</returns>
	/// <exception cref="InvalidOperationException">Thrown when no serializer is configured</exception>
	public Task<bool> LoadStateAsync(byte[] data, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the current stack state for serialization
	/// </summary>
	/// <returns>The current stack state</returns>
	public UndoRedoStackState GetCurrentState();

	/// <summary>
	/// Restores the stack from a given state
	/// </summary>
	/// <param name="state">The state to restore</param>
	/// <returns>True if restoration was successful</returns>
	public bool RestoreFromState(UndoRedoStackState state);
}
