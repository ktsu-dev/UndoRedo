// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

using ktsu.UndoRedo.Core.Contracts;

namespace ktsu.UndoRedo.Core.Services;

/// <summary>
/// Service for managing the command stack
/// </summary>
public sealed class StackManager : IStackManager
{
	private readonly List<ICommand> _commands = [];
	private int _currentPosition = -1;

	/// <inheritdoc />
	public IReadOnlyList<ICommand> Commands => _commands.AsReadOnly();

	/// <inheritdoc />
	public int CurrentPosition => _currentPosition;

	/// <inheritdoc />
	public bool CanUndo => _currentPosition >= 0;

	/// <inheritdoc />
	public bool CanRedo => _currentPosition < _commands.Count - 1;

	/// <inheritdoc />
	public void AddCommand(ICommand command)
	{
		ArgumentNullException.ThrowIfNull(command);

		// Clear any commands after the current position (we're branching)
		if (_currentPosition < _commands.Count - 1)
		{
			_commands.RemoveRange(_currentPosition + 1, _commands.Count - _currentPosition - 1);
		}

		_commands.Add(command);
		_currentPosition++;
	}

	/// <inheritdoc />
	public ICommand? MovePrevious()
	{
		if (!CanUndo)
		{
			return null;
		}

		var command = _commands[_currentPosition];
		_currentPosition--;
		return command;
	}

	/// <inheritdoc />
	public ICommand? MoveNext()
	{
		if (!CanRedo)
		{
			return null;
		}

		_currentPosition++;
		return _commands[_currentPosition];
	}

	/// <inheritdoc />
	public void ClearForward()
	{
		if (_currentPosition < _commands.Count - 1)
		{
			_commands.RemoveRange(_currentPosition + 1, _commands.Count - _currentPosition - 1);
		}
	}

	/// <inheritdoc />
	public void Clear()
	{
		_commands.Clear();
		_currentPosition = -1;
	}

	/// <inheritdoc />
	public int TrimToSize(int maxSize)
	{
		if (maxSize <= 0 || _commands.Count <= maxSize)
		{
			return 0;
		}

		var removeCount = _commands.Count - maxSize;
		_commands.RemoveRange(0, removeCount);
		_currentPosition -= removeCount;

		// Ensure position doesn't go negative
		if (_currentPosition < -1)
		{
			_currentPosition = -1;
		}

		return removeCount;
	}

	/// <inheritdoc />
	public ICommand? GetCurrentCommand() => CanUndo ? _commands[_currentPosition] : null;

	/// <inheritdoc />
	public IEnumerable<ICommand> GetCommandsInRange(int startIndex, int count)
	{
		if (startIndex < 0 || startIndex >= _commands.Count || count <= 0)
		{
			return [];
		}

		var endIndex = Math.Min(startIndex + count, _commands.Count);
		return _commands.GetRange(startIndex, endIndex - startIndex);
	}
}
