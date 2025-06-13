// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.UndoRedo.Core.Services;
using ktsu.UndoRedo.Core.Contracts;

/// <summary>
/// Service for managing the command stack
/// </summary>
public sealed class StackManager : IStackManager
{
	private readonly List<ICommand> _commands = [];

	/// <inheritdoc />
	public IReadOnlyList<ICommand> Commands => _commands.AsReadOnly();

	/// <inheritdoc />
	public int CurrentPosition { get; private set; } = -1;

	/// <inheritdoc />
	public bool CanUndo => CurrentPosition >= 0;

	/// <inheritdoc />
	public bool CanRedo => CurrentPosition < _commands.Count - 1;

	/// <inheritdoc />
	public void AddCommand(ICommand command)
	{
		ArgumentNullException.ThrowIfNull(command);

		// Clear any commands after the current position (we're branching)
		if (CurrentPosition < _commands.Count - 1)
		{
			_commands.RemoveRange(CurrentPosition + 1, _commands.Count - CurrentPosition - 1);
		}

		_commands.Add(command);
		CurrentPosition++;
	}

	/// <inheritdoc />
	public ICommand? MovePrevious()
	{
		if (!CanUndo)
		{
			return null;
		}

		ICommand command = _commands[CurrentPosition];
		CurrentPosition--;
		return command;
	}

	/// <inheritdoc />
	public ICommand? MoveNext()
	{
		if (!CanRedo)
		{
			return null;
		}

		CurrentPosition++;
		return _commands[CurrentPosition];
	}

	/// <inheritdoc />
	public void ClearForward()
	{
		if (CurrentPosition < _commands.Count - 1)
		{
			_commands.RemoveRange(CurrentPosition + 1, _commands.Count - CurrentPosition - 1);
		}
	}

	/// <inheritdoc />
	public void Clear()
	{
		_commands.Clear();
		CurrentPosition = -1;
	}

	/// <inheritdoc />
	public int TrimToSize(int maxSize)
	{
		if (maxSize <= 0 || _commands.Count <= maxSize)
		{
			return 0;
		}

		int removeCount = _commands.Count - maxSize;
		_commands.RemoveRange(0, removeCount);
		CurrentPosition -= removeCount;

		// Ensure position doesn't go negative
		if (CurrentPosition < -1)
		{
			CurrentPosition = -1;
		}

		return removeCount;
	}

	/// <inheritdoc />
	public ICommand? GetCurrentCommand() => CanUndo ? _commands[CurrentPosition] : null;

	/// <inheritdoc />
	public IEnumerable<ICommand> GetCommandsInRange(int startIndex, int count)
	{
		if (startIndex < 0 || startIndex >= _commands.Count || count <= 0)
		{
			return [];
		}

		int endIndex = Math.Min(startIndex + count, _commands.Count);
		return _commands.GetRange(startIndex, endIndex - startIndex);
	}
}
