// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

using ktsu.UndoRedo.Core.Contracts;
using ktsu.UndoRedo.Core.Models;

namespace ktsu.UndoRedo.Core.Services;

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
		var lastSave = _saveBoundaries.LastOrDefault();
		return lastSave == null || currentPosition > lastSave.Position;
	}

	/// <inheritdoc />
	public SaveBoundary CreateSaveBoundary(int position, string? description = null)
	{
		var saveBoundary = new SaveBoundary(position, description);
		_saveBoundaries.Add(saveBoundary);
		return saveBoundary;
	}

	/// <inheritdoc />
	public int CleanupInvalidBoundaries(int maxValidPosition)
	{
		var removed = 0;
		for (var i = _saveBoundaries.Count - 1; i >= 0; i--)
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

		for (var i = _saveBoundaries.Count - 1; i >= 0; i--)
		{
			var boundary = _saveBoundaries[i];
			var newPosition = boundary.Position + adjustment;

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

		if (currentPosition <= saveBoundary.Position)
		{
			return [];
		}

		return commands.Skip(saveBoundary.Position + 1).Take(currentPosition - saveBoundary.Position);
	}

	/// <inheritdoc />
	public void Clear() => _saveBoundaries.Clear();
}
