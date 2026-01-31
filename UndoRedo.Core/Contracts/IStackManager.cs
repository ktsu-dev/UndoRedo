// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.UndoRedo.Contracts;

/// <summary>
/// Service for managing the command stack
/// </summary>
public interface IStackManager
{
	/// <summary>
	/// Gets all commands in the stack
	/// </summary>
	public IReadOnlyList<ICommand> Commands { get; }

	/// <summary>
	/// Gets the current position in the stack
	/// </summary>
	public int CurrentPosition { get; }

	/// <summary>
	/// Gets whether there are commands available to undo
	/// </summary>
	public bool CanUndo { get; }

	/// <summary>
	/// Gets whether there are commands available to redo
	/// </summary>
	public bool CanRedo { get; }

	/// <summary>
	/// Adds a command to the stack
	/// </summary>
	/// <param name="command">The command to add</param>
	public void AddCommand(ICommand command);

	/// <summary>
	/// Moves the position back (for undo)
	/// </summary>
	/// <returns>The command that was undone, or null if none</returns>
	public ICommand? MovePrevious();

	/// <summary>
	/// Moves the position forward (for redo)
	/// </summary>
	/// <returns>The command that was redone, or null if none</returns>
	public ICommand? MoveNext();

	/// <summary>
	/// Clears all commands after the current position
	/// </summary>
	public void ClearForward();

	/// <summary>
	/// Clears the entire stack
	/// </summary>
	public void Clear();

	/// <summary>
	/// Trims the stack to the specified maximum size
	/// </summary>
	/// <param name="maxSize">Maximum number of commands to keep</param>
	/// <returns>Number of commands removed</returns>
	public int TrimToSize(int maxSize);

	/// <summary>
	/// Gets the command at the current position
	/// </summary>
	/// <returns>The current command, or null if none</returns>
	public ICommand? GetCurrentCommand();

	/// <summary>
	/// Gets commands in a specified range
	/// </summary>
	/// <param name="startIndex">Start index (inclusive)</param>
	/// <param name="count">Number of commands to get</param>
	/// <returns>Commands in the range</returns>
	public IEnumerable<ICommand> GetCommandsInRange(int startIndex, int count);
}
