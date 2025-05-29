// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.UndoRedo.Core;

/// <summary>
/// Metadata about a change for visualization and navigation purposes
/// </summary>
/// <param name="ChangeType">The type of change (Insert, Delete, Modify, etc.)</param>
/// <param name="AffectedItems">Items that were affected by this change</param>
/// <param name="Timestamp">When the change was made</param>
/// <param name="Size">Estimated size/magnitude of the change</param>
/// <param name="CustomData">Additional custom data for specific use cases</param>
public record ChangeMetadata(
	ChangeType ChangeType,
	IReadOnlyList<string> AffectedItems,
	DateTimeOffset Timestamp,
	int Size = 1,
	IReadOnlyDictionary<string, object>? CustomData = null
);

/// <summary>
/// Types of changes that can be made
/// </summary>
public enum ChangeType
{
	/// <summary>
	/// Something was inserted or added
	/// </summary>
	Insert,

	/// <summary>
	/// Something was deleted or removed
	/// </summary>
	Delete,

	/// <summary>
	/// Something was modified or changed
	/// </summary>
	Modify,

	/// <summary>
	/// Something was moved or repositioned
	/// </summary>
	Move,

	/// <summary>
	/// A composite change involving multiple operations
	/// </summary>
	Composite,

	/// <summary>
	/// A custom change type
	/// </summary>
	Custom
}
