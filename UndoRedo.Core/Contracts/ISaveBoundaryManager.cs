// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

using ktsu.UndoRedo.Core.Models;

namespace ktsu.UndoRedo.Core.Contracts;

/// <summary>
/// Service for managing save boundaries
/// </summary>
public interface ISaveBoundaryManager
{
	/// <summary>
	/// Gets all save boundaries
	/// </summary>
	public IReadOnlyList<SaveBoundary> SaveBoundaries { get; }

	/// <summary>
	/// Gets whether there are unsaved changes since the last save boundary
	/// </summary>
	/// <param name="currentPosition">Current position in the stack</param>
	/// <returns>True if there are unsaved changes</returns>
	public bool HasUnsavedChanges(int currentPosition);

	/// <summary>
	/// Creates a save boundary at the specified position
	/// </summary>
	/// <param name="position">Position in the stack</param>
	/// <param name="description">Optional description</param>
	/// <returns>The created save boundary</returns>
	public SaveBoundary CreateSaveBoundary(int position, string? description = null);

	/// <summary>
	/// Removes save boundaries that are no longer valid
	/// </summary>
	/// <param name="maxValidPosition">Maximum valid position</param>
	/// <returns>Number of boundaries removed</returns>
	public int CleanupInvalidBoundaries(int maxValidPosition);

	/// <summary>
	/// Adjusts save boundary positions after stack operations
	/// </summary>
	/// <param name="adjustment">Position adjustment (can be negative)</param>
	public void AdjustPositions(int adjustment);

	/// <summary>
	/// Gets the most recent save boundary
	/// </summary>
	/// <returns>The most recent save boundary, or null if none</returns>
	public SaveBoundary? GetLastSaveBoundary();

	/// <summary>
	/// Gets commands that would be undone to reach a save boundary
	/// </summary>
	/// <param name="saveBoundary">Target save boundary</param>
	/// <param name="currentPosition">Current position in the stack</param>
	/// <param name="commands">All commands in the stack</param>
	/// <returns>Commands that would be undone</returns>
	public IEnumerable<ICommand> GetCommandsToUndo(SaveBoundary saveBoundary, int currentPosition, IReadOnlyList<ICommand> commands);

	/// <summary>
	/// Clears all save boundaries
	/// </summary>
	public void Clear();
}
