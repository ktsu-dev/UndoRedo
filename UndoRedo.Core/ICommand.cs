// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.UndoRedo.Core;

/// <summary>
/// Represents a command that can be executed, undone, and provides metadata about the change
/// </summary>
public interface ICommand
{
	/// <summary>
	/// A human-readable description of what this command does
	/// </summary>
	public string Description { get; }

	/// <summary>
	/// An optional identifier that can be used for navigation purposes
	/// </summary>
	public string? NavigationContext { get; }

	/// <summary>
	/// Metadata about the change, useful for visualization
	/// </summary>
	public ChangeMetadata Metadata { get; }

	/// <summary>
	/// Execute the command
	/// </summary>
	public void Execute();

	/// <summary>
	/// Undo the command, restoring the previous state
	/// </summary>
	public void Undo();

	/// <summary>
	/// Determines if this command can be merged with another command
	/// Useful for operations like typing where multiple character insertions can be combined
	/// </summary>
	/// <param name="other">The other command to potentially merge with</param>
	/// <returns>True if the commands can be merged</returns>
	public bool CanMergeWith(ICommand other);

	/// <summary>
	/// Merge this command with another command
	/// </summary>
	/// <param name="other">The command to merge with</param>
	/// <returns>A new command representing the merged operations</returns>
	public ICommand MergeWith(ICommand other);
}
