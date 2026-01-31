// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.UndoRedo;

/// <summary>
/// A command that contains multiple sub-commands executed as a single operation
/// </summary>
public sealed class CompositeCommand : BaseCommand
{
	private readonly List<ICommand> _commands;

	/// <inheritdoc />
	public override string Description { get; }

	/// <summary>
	/// Gets the sub-commands in this composite
	/// </summary>
	public IReadOnlyList<ICommand> Commands => _commands.AsReadOnly();

	/// <summary>
	/// Creates a new composite command
	/// </summary>
	/// <param name="description">Description of the composite operation</param>
	/// <param name="commands">Commands to execute as a group</param>
	/// <param name="navigationContext">Optional navigation context</param>
	public CompositeCommand(string description, IEnumerable<ICommand> commands, string? navigationContext = null)
		: base(
			ChangeType.Composite,
			GetAffectedItems(commands),
			navigationContext,
			GetTotalSize(commands))
	{
		Description = description;
		_commands = [.. commands];

		if (_commands.Count == 0)
		{
			throw new ArgumentException("Composite command must contain at least one command", nameof(commands));
		}
	}

	/// <inheritdoc />
	public override void Execute()
	{
		List<ICommand> executedCommands = [];

		try
		{
			foreach (ICommand command in _commands)
			{
				command.Execute();
				executedCommands.Add(command);
			}
		}
#pragma warning disable CA1031 // Do not catch general exception types
		catch (Exception)
		{
			// Rollback all successfully executed commands in reverse order
			for (int i = executedCommands.Count - 1; i >= 0; i--)
			{
				try
				{
					executedCommands[i].Undo();
				}
				catch (Exception)
				{
					// Continue with rollback even if individual undo fails
				}
			}
			throw; // Re-throw the original exception
		}
#pragma warning restore CA1031 // Do not catch general exception types
	}

	/// <inheritdoc />
	public override void Undo()
	{
		List<Exception> undoExceptions = [];

		// Undo in reverse order, collecting any exceptions
		for (int i = _commands.Count - 1; i >= 0; i--)
		{
#pragma warning disable CA1031 // Do not catch general exception types
			try
			{
				_commands[i].Undo();
			}
			catch (Exception ex)
			{
				undoExceptions.Add(ex);
			}
#pragma warning restore CA1031 // Do not catch general exception types
		}

		// If any undo operations failed, throw the first exception
		if (undoExceptions.Count > 0)
		{
			throw undoExceptions[0];
		}
	}

	/// <inheritdoc />
	public override bool CanMergeWith(ICommand other) => false;

	/// <inheritdoc />
	public override ICommand MergeWith(ICommand other) => throw new NotSupportedException("Composite commands cannot be merged");

	private static IReadOnlyList<string> GetAffectedItems(IEnumerable<ICommand> commands)
	{
		List<ICommand> commandList = [.. commands];
		return [.. commandList.SelectMany(c => c.Metadata.AffectedItems).Distinct()];
	}

	private static int GetTotalSize(IEnumerable<ICommand> commands)
	{
		List<ICommand> commandList = [.. commands];
		return commandList.Sum(c => c.Metadata.Size);
	}
}
