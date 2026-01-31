// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.UndoRedo.Contracts;

/// <summary>
/// Service for merging compatible commands
/// </summary>
public interface ICommandMerger
{
	/// <summary>
	/// Determines if two commands can be merged
	/// </summary>
	/// <param name="first">The first command</param>
	/// <param name="second">The second command</param>
	/// <returns>True if the commands can be merged</returns>
	public bool CanMerge(ICommand first, ICommand second);

	/// <summary>
	/// Merges two compatible commands into one
	/// </summary>
	/// <param name="first">The first command</param>
	/// <param name="second">The second command</param>
	/// <returns>A new merged command</returns>
	public ICommand Merge(ICommand first, ICommand second);
}
