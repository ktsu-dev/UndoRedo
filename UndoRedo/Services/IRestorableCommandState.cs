// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Core.Services;

/// <summary>
/// A command that can take back the navigation context and metadata it was saved with
/// </summary>
/// <remarks>
/// A reloaded <see cref="ISerializableCommand"/> is built with its parameterless constructor, which knows
/// neither, and <see cref="ISerializableCommand.DeserializeData"/> only restores the command's own data.
/// After that, <see cref="JsonUndoRedoSerializer"/> calls <see cref="RestoreSerializedState"/> on any command
/// that implements this, so undo and redo keep navigating to the change and the change history keeps its
/// timestamps and sizes. <see cref="BaseCommand"/> already implements it; implement it yourself on a command
/// that implements <see cref="ICommand"/> directly.
/// </remarks>
public interface IRestorableCommandState
{
	/// <summary>
	/// Puts back the navigation context and metadata the command was saved with
	/// </summary>
	/// <param name="navigationContext">The navigation context the command was saved with</param>
	/// <param name="metadata">The metadata the command was saved with</param>
	public void RestoreSerializedState(string? navigationContext, ChangeMetadata metadata);
}
