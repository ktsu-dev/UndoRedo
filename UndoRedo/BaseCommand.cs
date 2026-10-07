// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo;

/// <summary>
/// Base implementation of ICommand with common functionality
/// </summary>
/// <param name="changeType">The type of change</param>
/// <param name="affectedItems">Items affected by this change</param>
/// <param name="navigationContext">Optional navigation context</param>
/// <param name="size">Estimated size of the change</param>
/// <param name="customData">Additional custom data</param>
public abstract class BaseCommand(
	ChangeType changeType,
	IReadOnlyList<string> affectedItems,
	string? navigationContext = null,
	int size = 1,
	IReadOnlyDictionary<string, object>? customData = null) : ICommand
{
	/// <inheritdoc />
	public abstract string Description { get; }

	/// <inheritdoc />
	public virtual string? NavigationContext { get; protected set; } = navigationContext;

	/// <inheritdoc />
	public virtual ChangeMetadata Metadata { get; protected set; } = new(changeType, affectedItems, DateTimeOffset.Now, size, customData);

	/// <inheritdoc />
	public abstract void Execute();

	/// <inheritdoc />
	public abstract void Undo();

	/// <inheritdoc />
	public virtual bool CanMergeWith(ICommand other) => false;

	/// <inheritdoc />
	public virtual ICommand MergeWith(ICommand other) => throw new InvalidOperationException("This command does not support merging");

	/// <summary>
	/// Puts back the navigation context and metadata a command was saved with. A reloaded
	/// <see cref="Core.Services.ISerializableCommand"/> is built with its parameterless constructor, which knows
	/// neither, and <see cref="Core.Services.ISerializableCommand.DeserializeData"/> only restores the command's own data.
	/// </summary>
	/// <param name="navigationContext">The navigation context the command was saved with</param>
	/// <param name="metadata">The metadata the command was saved with</param>
	internal void RestoreSerializedState(string? navigationContext, ChangeMetadata metadata)
	{
		NavigationContext = navigationContext;
		Metadata = metadata;
	}
}
