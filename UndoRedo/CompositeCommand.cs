// Copyright (c) 2023-2026 ktsu-dev contributors

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
		List<ICommand> undoneCommands = [];

		try
		{
			// Undo in reverse order
			for (int i = _commands.Count - 1; i >= 0; i--)
			{
				_commands[i].Undo();
				undoneCommands.Add(_commands[i]);
			}
		}
#pragma warning disable CA1031 // Do not catch general exception types
		catch (Exception)
		{
			// Re-apply what this call already undid, in forward order, so a failed Undo leaves the
			// composite fully applied. The stack keeps its position when Undo throws, so it must be
			// able to rely on nothing having been undone. This mirrors the rollback in Execute().
			for (int i = undoneCommands.Count - 1; i >= 0; i--)
			{
				try
				{
					undoneCommands[i].Execute();
				}
				catch (Exception)
				{
					// Continue restoring even if an individual re-execute fails
				}
			}
			throw; // Re-throw the original exception
		}
#pragma warning restore CA1031 // Do not catch general exception types
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
