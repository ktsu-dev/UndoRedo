// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo;

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
	/// <remarks>
	/// The service asks once per <c>Execute</c>, while this command is still applied, and does not ask
	/// again before calling <see cref="MergeWith"/>.
	/// </remarks>
	/// <param name="other">The other command to potentially merge with</param>
	/// <returns>True if the commands can be merged</returns>
	public bool CanMergeWith(ICommand other);

	/// <summary>
	/// Merge this command with another command
	/// </summary>
	/// <remarks>
	/// The service calls this after <see cref="CanMergeWith"/> said yes and after undoing this command,
	/// then executes the command returned. A merge may return this command changed in place. If that
	/// command's Execute then throws, the original edit can't be applied again, so it leaves the history
	/// with <c>CommandDiscardReason.MergeFailed</c>. A merge that throws must do so before changing
	/// anything, so the original can be applied again.
	/// </remarks>
	/// <param name="other">The command to merge with</param>
	/// <returns>A new command representing the merged operations</returns>
	public ICommand MergeWith(ICommand other);
}
