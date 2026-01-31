// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.UndoRedo;

/// <summary>
/// Represents a save boundary in the undo/redo stack
/// </summary>
/// <param name="position">The position in the stack</param>
/// <param name="description">Optional description</param>
public sealed class SaveBoundary(int position, string? description = null)
{
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
}
