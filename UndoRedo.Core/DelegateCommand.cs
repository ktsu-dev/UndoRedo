// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.UndoRedo;

/// <summary>
/// A simple command implementation using delegates for execute and undo actions
/// </summary>
/// <param name="description">Description of the command</param>
/// <param name="executeAction">Action to perform when executing</param>
/// <param name="undoAction">Action to perform when undoing</param>
/// <param name="changeType">Type of change</param>
/// <param name="affectedItems">Items affected by this change</param>
/// <param name="navigationContext">Optional navigation context</param>
/// <param name="size">Estimated size of the change</param>
/// <param name="customData">Additional custom data</param>
public sealed class DelegateCommand(
	string description,
	Action executeAction,
	Action undoAction,
	ChangeType changeType = ChangeType.Modify,
	IReadOnlyList<string>? affectedItems = null,
	string? navigationContext = null,
	int size = 1,
	IReadOnlyDictionary<string, object>? customData = null)
	: BaseCommand(changeType, affectedItems ?? [], navigationContext, size, customData)
{
	private readonly Action _executeAction = executeAction ?? throw new ArgumentNullException(nameof(executeAction));
	private readonly Action _undoAction = undoAction ?? throw new ArgumentNullException(nameof(undoAction));

	/// <inheritdoc />
	public override string Description { get; } = description;

	/// <inheritdoc />
	public override void Execute() => _executeAction();

	/// <inheritdoc />
	public override void Undo() => _undoAction();
}
