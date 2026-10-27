// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo;

/// <summary>
/// A command loaded from saved history that could not be rebuilt, such as a <see cref="DelegateCommand"/>
/// or a command whose type is no longer available
/// </summary>
/// <remarks>
/// A placeholder keeps the saved description, navigation context and metadata, so it can be shown in the
/// history and written back on the next save, but it can be neither undone nor redone. The service reports
/// <c>CanUndo</c> as <see langword="false"/> while the current command is a placeholder, and
/// <c>CanRedo</c> as <see langword="false"/> while the next one is, so history on the far side of a
/// placeholder can't be reached by stepping through it.
/// </remarks>
public interface IPlaceholderCommand : ICommand
{
	/// <summary>
	/// The command type name as it was saved
	/// </summary>
	public string OriginalType { get; }

	/// <summary>
	/// The description as it was saved, without the placeholder prefix
	/// </summary>
	public string OriginalDescription { get; }
}
