// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.UndoRedo.Core.Services;
using ktsu.UndoRedo.Core.Contracts;

/// <summary>
/// Service for managing save boundaries
/// </summary>
public sealed class SaveBoundaryManager : ISaveBoundaryManager
{
	private readonly List<SaveBoundary> _saveBoundaries = [];

	/// <inheritdoc />
	public IReadOnlyList<SaveBoundary> SaveBoundaries => _saveBoundaries.AsReadOnly();

	/// <inheritdoc />
	public bool HasUnsavedChanges(int currentPosition)
	{
		// If no save boundaries exist, we have unsaved changes unless at initial position
		if (_saveBoundaries.Count == 0)
		{
			return currentPosition >= 0;
		}

		// No unsaved changes if we're exactly at a save boundary position
		return !_saveBoundaries.Any(boundary => boundary.Position == currentPosition);
	}

	/// <inheritdoc />
	public SaveBoundary CreateSaveBoundary(int position, string? description = null)
	{
		SaveBoundary saveBoundary = new(position, description);
		_saveBoundaries.Add(saveBoundary);
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

		for (int i = _saveBoundaries.Count - 1; i >= 0; i--)
		{
			SaveBoundary boundary = _saveBoundaries[i];
			int newPosition = boundary.Position + adjustment;

			if (newPosition < 0)
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
		ArgumentNullException.ThrowIfNull(saveBoundary);
		ArgumentNullException.ThrowIfNull(commands);

		return currentPosition <= saveBoundary.Position
			? []
			: commands.Skip(saveBoundary.Position + 1).Take(currentPosition - saveBoundary.Position);
	}

	/// <inheritdoc />
	public void Clear() => _saveBoundaries.Clear();
}
