// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo;

/// <summary>
/// Represents a save boundary in the undo/redo stack
/// </summary>
/// <param name="position">The position in the stack</param>
/// <param name="description">Optional description</param>
public sealed class SaveBoundary(int position, string? description = null)
{
	/// <summary>
	/// Creates a copy of <paramref name="original"/> at a new position that is still the same save point,
	/// so a caller holding the original can have it resolved to where the save point is now
	/// </summary>
	internal SaveBoundary(SaveBoundary original, int position)
		: this(position, original.Description)
	{
		Identity = original.Identity;
		Timestamp = original.Timestamp;
	}

	/// <summary>
	/// The position in the stack where this save boundary was created
	/// </summary>
	public int Position { get; } = position;

	/// <summary>
	/// When this save boundary was created
	/// </summary>
	public DateTimeOffset Timestamp { get; } = DateTimeOffset.Now;

	/// <summary>
	/// Optional description of what was saved
	/// </summary>
	public string? Description { get; } = description;

	/// <summary>
	/// Shared by every copy of one save point as its position is adjusted
	/// </summary>
	internal object Identity { get; } = new();

	/// <summary>
	/// Whether this boundary and <paramref name="other"/> describe the same save point
	/// </summary>
	internal bool IsSameSavePointAs(SaveBoundary other) => ReferenceEquals(Identity, other.Identity);
}
