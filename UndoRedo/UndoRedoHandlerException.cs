// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo;

/// <summary>
/// Thrown after an undo/redo operation has completed when one or more of its event handlers threw
/// </summary>
/// <remarks>
/// The operation itself succeeded: the command was executed, undone or redone, and the history reflects
/// it. Don't retry the operation, since that would apply it a second time. Every handler of every event
/// still ran, and <see cref="AggregateException.InnerExceptions"/> holds what each failing one threw.
/// </remarks>
public sealed class UndoRedoHandlerException : AggregateException
{
	private const string DefaultMessage = "The operation completed, but one or more event handlers threw.";

	/// <summary>
	/// Creates the exception with the default message
	/// </summary>
	public UndoRedoHandlerException()
		: base(DefaultMessage)
	{
	}

	/// <summary>
	/// Creates the exception with a message
	/// </summary>
	/// <param name="message">The message</param>
	public UndoRedoHandlerException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Creates the exception with a message and one handler's exception
	/// </summary>
	/// <param name="message">The message</param>
	/// <param name="innerException">What the handler threw</param>
	public UndoRedoHandlerException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Creates the exception from what each failing handler threw
	/// </summary>
	/// <param name="innerExceptions">What each failing handler threw</param>
	public UndoRedoHandlerException(IEnumerable<Exception> innerExceptions)
		: base(DefaultMessage, innerExceptions)
	{
	}
}
