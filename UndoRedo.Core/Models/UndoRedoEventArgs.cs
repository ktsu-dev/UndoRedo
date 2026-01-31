// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

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
