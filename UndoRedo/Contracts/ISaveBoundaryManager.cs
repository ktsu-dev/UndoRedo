// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Contracts;

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
	/// Gets whether position -1 still holds the clean initial state, which needs no save boundary to
	/// count as saved
	/// </summary>
	/// <remarks>
	/// This becomes <see langword="false"/> once a save boundary is created, and once trimming the
	/// oldest commands makes -1 the state after them. Persisted stack state carries it, so a reloaded
	/// stack reports unsaved changes at -1 exactly as the original did.
	/// </remarks>
	public bool InitialStateIsClean { get; }

	/// <summary>
	/// Sets whether position -1 holds the clean initial state
	/// </summary>
	/// <remarks>
	/// Used when restoring saved stack state, since <see cref="Clear"/> resets it to
	/// <see langword="true"/>. Creating a save boundary afterwards still sets it to
	/// <see langword="false"/>.
	/// </remarks>
	/// <param name="isClean">Whether position -1 holds the clean initial state</param>
	public void SetInitialStateClean(bool isClean);

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
