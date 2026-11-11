// Copyright (c) 2023-2026 ktsu-dev contributors

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
	/// <remarks>
	/// The service calls <see cref="CanMerge"/> with <paramref name="first"/> applied, then calls this
	/// with <paramref name="first"/> undone, so an implementation that checks again can see a different
	/// document.
	/// </remarks>
	/// <param name="first">The first command</param>
	/// <param name="second">The second command</param>
	/// <returns>A new merged command</returns>
	public ICommand Merge(ICommand first, ICommand second);
}
