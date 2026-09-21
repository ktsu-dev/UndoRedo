// Copyright (c) 2023-2026 ktsu-dev contributors

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
	/// The <see cref="CurrentPosition"/> of a stack with nothing applied, matching the convention
	/// used by <c>IStackManager.CurrentPosition</c>
	/// </summary>
	private const int EmptyPosition = -1;

	/// <summary>
	/// Creates an empty stack state
	/// </summary>
	/// <param name="formatVersion">The format version</param>
	/// <returns>Empty stack state</returns>
	public static UndoRedoStackState CreateEmpty(string formatVersion) =>
		new([], EmptyPosition, [], formatVersion, DateTime.UtcNow);

	/// <summary>
	/// Gets whether this state represents an empty stack
	/// </summary>
	public bool IsEmpty => Commands.Count == 0;

	/// <summary>
	/// Gets whether there are commands that can be undone
	/// </summary>
	/// <remarks>
	/// <see cref="CurrentPosition"/> indexes the command that has been applied, so position 0 means
	/// one command is applied and can be undone. This matches <c>IStackManager.CanUndo</c>, which
	/// <c>IUndoRedoService.CanUndo</c> reflects.
	/// </remarks>
	public bool CanUndo => CurrentPosition >= 0;

	/// <summary>
	/// Gets whether there are commands that can be redone
	/// </summary>
	public bool CanRedo => CurrentPosition < Commands.Count - 1;

	/// <summary>
	/// Gets the total number of commands in the stack
	/// </summary>
	public int CommandCount => Commands.Count;
}
