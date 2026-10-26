// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Core.Services;

using ktsu.UndoRedo.Contracts;

/// <summary>
/// Service for managing save boundaries
/// </summary>
public sealed class SaveBoundaryManager : ISaveBoundaryManager
{
	private readonly List<SaveBoundary> _saveBoundaries = [];

	// The boundary the most recent save made: the only one whose position matches what is on disk.
	// Older boundaries stay in the list for UndoToSaveBoundaryAsync and the visualization, but a later
	// save replaced their content, so they no longer count as clean. Null when nothing has been saved,
	// or when that boundary was removed, in which case no position matches disk.
	private SaveBoundary? _latestSaveBoundary;

	/// <inheritdoc />
	public IReadOnlyList<SaveBoundary> SaveBoundaries => _saveBoundaries.AsReadOnly();

	// Whether position -1 still holds the untouched initial state, which is clean without a boundary.
	// It stops being true once anything is saved, since the saved state replaces it, and once trimming
	// shifts later commands' results down to -1.
	/// <inheritdoc />
	public bool InitialStateIsClean { get; private set; } = true;

	/// <inheritdoc />
	public void SetInitialStateClean(bool isClean) => InitialStateIsClean = isClean;

	/// <inheritdoc />
	public bool HasUnsavedChanges(int currentPosition)
	{
		if (currentPosition == -1 && InitialStateIsClean)
		{
			return false;
		}

		// No unsaved changes only at the latest save; an older boundary's content is no longer on disk
		return _latestSaveBoundary is null || _latestSaveBoundary.Position != currentPosition;
	}

	/// <inheritdoc />
	public SaveBoundary CreateSaveBoundary(int position, string? description = null)
	{
		// Saving again where a boundary already is re-saves that save point rather than adding another,
		// so repeated saves with no edit in between do not grow the list
		int existing = _saveBoundaries.FindIndex(boundary => boundary.Position == position);
		SaveBoundary saveBoundary;
		if (existing >= 0)
		{
			// Keep the list in save order, so the re-saved boundary moves to the end as the latest
			saveBoundary = new(_saveBoundaries[existing], description);
			_saveBoundaries.RemoveAt(existing);
		}
		else
		{
			saveBoundary = new(position, description);
		}

		_saveBoundaries.Add(saveBoundary);
		_latestSaveBoundary = saveBoundary;
		InitialStateIsClean = false;
		return saveBoundary;
	}

	/// <summary>
	/// Adds a save boundary recreated from saved state, keeping the time it was originally created
	/// </summary>
	/// <remarks>
	/// Boundaries are restored in the order they were created, so the last one restored becomes the latest.
	/// </remarks>
	internal void RestoreSaveBoundary(SaveBoundary saveBoundary)
	{
		// Keep the boundary's identity as well as its timestamp, so a SaveBoundary a caller already holds,
		// from the restored state or from SaveBoundaryCreated, still resolves after the restore
		SaveBoundary restored = new(saveBoundary, saveBoundary.Position);
		_saveBoundaries.Add(restored);
		_latestSaveBoundary = restored;
		InitialStateIsClean = false;
	}

	/// <summary>
	/// The index in <see cref="SaveBoundaries"/> of the boundary the most recent save made, or -1 when
	/// that boundary is gone and no position matches what is on disk
	/// </summary>
	internal int LatestSaveBoundaryIndex => _latestSaveBoundary is null ? -1 : _saveBoundaries.IndexOf(_latestSaveBoundary);

	/// <summary>
	/// Sets which restored boundary the most recent save made, by its index in <see cref="SaveBoundaries"/>,
	/// or none for -1
	/// </summary>
	internal void RestoreLatestSaveBoundary(int index) =>
		_latestSaveBoundary = index >= 0 && index < _saveBoundaries.Count ? _saveBoundaries[index] : null;

	/// <inheritdoc />
	public int CleanupInvalidBoundaries(int maxValidPosition)
	{
		int removed = 0;
		for (int i = _saveBoundaries.Count - 1; i >= 0; i--)
		{
			if (_saveBoundaries[i].Position > maxValidPosition)
			{
				ForgetIfLatest(_saveBoundaries[i]);
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
			InitialStateIsClean = false;
		}

		for (int i = _saveBoundaries.Count - 1; i >= 0; i--)
		{
			SaveBoundary boundary = _saveBoundaries[i];
			int newPosition = boundary.Position + adjustment;

			// -1 is a reachable position, so a boundary shifted exactly there is still a valid save point
			if (newPosition < -1)
			{
				ForgetIfLatest(boundary);
				_saveBoundaries.RemoveAt(i);
			}
			else
			{
				// Create a new boundary with adjusted position that is still the same save point, so a
				// boundary a caller already holds can be resolved to it
				SaveBoundary adjusted = new(boundary, newPosition);
				_saveBoundaries[i] = adjusted;
				if (ReferenceEquals(boundary, _latestSaveBoundary))
				{
					_latestSaveBoundary = adjusted;
				}
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

		// Materialize so the result is a snapshot, not a view that changes with the live stack
		return currentPosition <= saveBoundary.Position
			? []
			: [.. commands.Skip(saveBoundary.Position + 1).Take(currentPosition - saveBoundary.Position)];
	}

	/// <inheritdoc />
	public void Clear()
	{
		_saveBoundaries.Clear();
		_latestSaveBoundary = null;
		InitialStateIsClean = true;
	}

	// Once the latest save's boundary is gone no position matches disk, so do not fall back to an older one
	private void ForgetIfLatest(SaveBoundary boundary)
	{
		if (ReferenceEquals(boundary, _latestSaveBoundary))
		{
			_latestSaveBoundary = null;
		}
	}
}
