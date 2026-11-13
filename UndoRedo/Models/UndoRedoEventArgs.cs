// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo;

/// <summary>
/// Base class for undo/redo related events
/// </summary>
/// <param name="command">The command associated with this event</param>
/// <param name="position">The current position in the undo/redo stack</param>
public abstract class UndoRedoEventArgs(ICommand command, int position) : EventArgs
{
	/// <summary>
	/// The command associated with this event
	/// </summary>
	public ICommand Command { get; } = command;

	/// <summary>
	/// The current position in the undo/redo stack
	/// </summary>
	public int Position { get; } = position;
}

/// <summary>
/// Event args for when a command is executed
/// </summary>
/// <param name="command">The executed command</param>
/// <param name="position">Current position</param>
public sealed class CommandExecutedEventArgs(ICommand command, int position) : UndoRedoEventArgs(command, position);

/// <summary>
/// Event args for when a command is undone
/// </summary>
/// <param name="command">The undone command</param>
/// <param name="position">Current position</param>
public sealed class CommandUndoneEventArgs(ICommand command, int position) : UndoRedoEventArgs(command, position);

/// <summary>
/// Event args for when a command is redone
/// </summary>
/// <param name="command">The redone command</param>
/// <param name="position">Current position</param>
public sealed class CommandRedoneEventArgs(ICommand command, int position) : UndoRedoEventArgs(command, position);

/// <summary>
/// Event args for when a save boundary is created
/// </summary>
/// <param name="saveBoundary">The save boundary</param>
public sealed class SaveBoundaryCreatedEventArgs(SaveBoundary saveBoundary) : EventArgs
{
	/// <summary>
	/// The created save boundary
	/// </summary>
	public SaveBoundary SaveBoundary { get; } = saveBoundary;
}

/// <summary>
/// Event args for when the oldest commands are dropped from the history
/// </summary>
/// <param name="commands">The dropped commands, oldest first</param>
/// <param name="position">The current position after the drop</param>
public sealed class CommandsDroppedEventArgs(IReadOnlyList<ICommand> commands, int position) : EventArgs
{
	/// <summary>
	/// The dropped commands, oldest first, each listed once. They can no longer be undone or redone, so
	/// anything they held only for undo can be released.
	/// </summary>
	/// <remarks>
	/// A command instance executed more than once fills more than one slot. It is listed only once the
	/// last slot holding it, directly or inside a composite, has gone, so this can be empty when every
	/// dropped slot held an instance that is still in the history.
	/// </remarks>
	public IReadOnlyList<ICommand> Commands { get; } = commands;

	/// <summary>
	/// The current position in the undo/redo stack after the drop
	/// </summary>
	public int Position { get; } = position;
}

/// <summary>
/// Why commands left the history
/// </summary>
public enum CommandDiscardReason
{
	/// <summary>
	/// The oldest commands were dropped, by <c>DropOldest</c> or by <c>UndoRedoOptions.MaxStackSize</c>
	/// </summary>
	Trimmed,

	/// <summary>
	/// A new command was executed after an undo, so the commands that could have been redone were thrown away
	/// </summary>
	Branched,

	/// <summary>
	/// Two commands were merged into one, and these are the ones the merged command replaced
	/// </summary>
	Merged,

	/// <summary>
	/// The history was cleared
	/// </summary>
	Cleared,

	/// <summary>
	/// The history was replaced by a restored or loaded one that does not contain these commands
	/// </summary>
	Replaced,

	/// <summary>
	/// A merge failed after the command it was merging into had been undone, and that command could not
	/// be applied again, so it is no longer applied: either the merged command's Execute threw after a
	/// MergeWith that merged in place, or applying the original again threw as well
	/// </summary>
	MergeFailed,
}

/// <summary>
/// Event args for when commands leave the history for any reason
/// </summary>
/// <param name="commands">The discarded commands, in the order they were in the history</param>
/// <param name="reason">Why they were discarded</param>
/// <param name="position">The current position after they were discarded</param>
public sealed class CommandsDiscardedEventArgs(IReadOnlyList<ICommand> commands, CommandDiscardReason reason, int position) : EventArgs
{
	/// <summary>
	/// The discarded commands. None of them can be undone or redone any more, so anything they held for
	/// undo or redo can be released. Each command is reported once, and a command still in the history
	/// is never reported.
	/// </summary>
	public IReadOnlyList<ICommand> Commands { get; } = commands;

	/// <summary>
	/// Why the commands were discarded
	/// </summary>
	public CommandDiscardReason Reason { get; } = reason;

	/// <summary>
	/// The current position in the undo/redo stack after the commands were discarded
	/// </summary>
	public int Position { get; } = position;
}
