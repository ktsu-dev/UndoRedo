// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.UndoRedo.Models;

/// <summary>
/// Represents the complete state of an undo/redo stack for serialization
/// </summary>
/// <param name="Commands">The commands in the stack</param>
/// <param name="CurrentPosition">The current position in the stack</param>
/// <param name="SaveBoundaries">The save boundaries</param>
/// <param name="FormatVersion">The format version used for serialization</param>
/// <param name="Timestamp">When this state was serialized</param>
public record UndoRedoStackState(
	IReadOnlyList<ICommand> Commands,
	int CurrentPosition,
	IReadOnlyList<SaveBoundary> SaveBoundaries,
	string FormatVersion,
	DateTime Timestamp
)
{
	/// <summary>
	/// Creates an empty stack state
	/// </summary>
	/// <param name="formatVersion">The format version</param>
	/// <returns>Empty stack state</returns>
	public static UndoRedoStackState CreateEmpty(string formatVersion) =>
		new([], 0, [], formatVersion, DateTime.UtcNow);

	/// <summary>
	/// Gets whether this state represents an empty stack
	/// </summary>
	public bool IsEmpty => Commands.Count == 0;

	/// <summary>
	/// Gets whether there are commands that can be undone
	/// </summary>
	public bool CanUndo => CurrentPosition > 0;

	/// <summary>
	/// Gets whether there are commands that can be redone
	/// </summary>
	public bool CanRedo => CurrentPosition < Commands.Count - 1;

	/// <summary>
	/// Gets the total number of commands in the stack
	/// </summary>
	public int CommandCount => Commands.Count;
}
