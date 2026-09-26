// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Core.Services;

using ktsu.UndoRedo.Contracts;

/// <summary>
/// Service for managing save boundaries
/// </summary>
public sealed class SaveBoundaryManager : ISaveBoundaryManager
{
	private readonly List<SaveBoundary> _saveBoundaries = [];

	// Whether position -1 still holds the untouched initial state, which is clean without a boundary.
	// It stops being true once anything is saved, since the saved state replaces it, and once trimming
	// shifts later commands' results down to -1.
	private bool _initialStateIsClean = true;

	/// <inheritdoc />
	public IReadOnlyList<SaveBoundary> SaveBoundaries => _saveBoundaries.AsReadOnly();

	/// <inheritdoc />
	public bool HasUnsavedChanges(int currentPosition)
	{
		if (currentPosition == -1 && _initialStateIsClean)
		{
			return false;
		}

		// No unsaved changes if we're exactly at a save boundary position
		return !_saveBoundaries.Any(boundary => boundary.Position == currentPosition);
	}

	/// <inheritdoc />
	public SaveBoundary CreateSaveBoundary(int position, string? description = null)
	{
		SaveBoundary saveBoundary = new(position, description);
		_saveBoundaries.Add(saveBoundary);
		_initialStateIsClean = false;
		return saveBoundary;
	}

	/// <inheritdoc />
	public int CleanupInvalidBoundaries(int maxValidPosition)
	{
		int removed = 0;
		for (int i = _saveBoundaries.Count - 1; i >= 0; i--)
		{
			if (_saveBoundaries[i].Position > maxValidPosition)
			{
				_saveBoundaries.RemoveAt(i);
				removed++;
			}
		}
		return removed;
	}

	/// <inheritdoc />
	public void AdjustPositions(int adjustment)
	{
		if (adjustment == 0)
		{
			return;
		}

		if (adjustment < 0)
		{
			// Commands were trimmed from the bottom, so -1 is now the state after them, not the initial one
			_initialStateIsClean = false;
		}

		for (int i = _saveBoundaries.Count - 1; i >= 0; i--)
		{
			SaveBoundary boundary = _saveBoundaries[i];
			int newPosition = boundary.Position + adjustment;

			// -1 is a reachable position, so a boundary shifted exactly there is still a valid save point
			if (newPosition < -1)
			{
				_saveBoundaries.RemoveAt(i);
			}
			else
			{
				// Create a new boundary with adjusted position
				_saveBoundaries[i] = new SaveBoundary(newPosition, boundary.Description);
			}
		}
	}

	/// <inheritdoc />
	public SaveBoundary? GetLastSaveBoundary() => _saveBoundaries.LastOrDefault();

	/// <inheritdoc />
	public IEnumerable<ICommand> GetCommandsToUndo(SaveBoundary saveBoundary, int currentPosition, IReadOnlyList<ICommand> commands)
	{
		Ensure.NotNull(saveBoundary);
		Ensure.NotNull(commands);

		return currentPosition <= saveBoundary.Position
			? []
			: commands.Skip(saveBoundary.Position + 1).Take(currentPosition - saveBoundary.Position);
	}

	/// <inheritdoc />
	public void Clear()
	{
		_saveBoundaries.Clear();
		_initialStateIsClean = true;
	}
}
