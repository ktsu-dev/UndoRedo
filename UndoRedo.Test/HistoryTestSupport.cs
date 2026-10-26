// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Test;

using System.Collections.Concurrent;
using System.Globalization;
using ktsu.UndoRedo.Models;
using ktsu.UndoRedo.Core.Services;

/// <summary>
/// Commands and helpers shared by the history tests
/// </summary>
internal static class HistoryTestSupport
{
	public static UndoRedoService Service(UndoRedoOptions? options = null) =>
		new(new StackManager(), new SaveBoundaryManager(), new CommandMerger(), options);

	public static UndoRedoService JsonService(UndoRedoOptions? options = null)
	{
		UndoRedoService service = Service(options);
		service.SetSerializer(new JsonUndoRedoSerializer());
		return service;
	}

	public static DelegateCommand Increment(StrongBox<int> value, string description = "inc") =>
		new(description, () => value.Value++, () => value.Value--);
}

/// <summary>
/// A boxed value a test's commands change, so tests running in parallel never share one
/// </summary>
internal sealed class StrongBox<T>(T value)
{
	public T Value { get; set; } = value;
}

/// <summary>
/// A command that survives a save and load: it adds an amount to a named register
/// </summary>
#pragma warning disable CA1812 // Instantiated by reflection during deserialization
internal sealed class AddToRegister : BaseCommand, ISerializableCommand
#pragma warning restore CA1812
{
	public static readonly ConcurrentDictionary<string, int> Registers = new();

	public string Register { get; private set; } = string.Empty;

	public int Amount { get; private set; }

	public AddToRegister()
		: base(ChangeType.Modify, ["register"])
	{
	}

	public AddToRegister(string register, int amount)
		: this()
	{
		Register = register;
		Amount = amount;
	}

	public override string Description => $"Add {Amount} to {Register}";

	public override void Execute() => Registers.AddOrUpdate(Register, Amount, (_, current) => current + Amount);

	public override void Undo() => Registers.AddOrUpdate(Register, -Amount, (_, current) => current - Amount);

	public string SerializeData() => $"{Register}|{Amount.ToString(CultureInfo.InvariantCulture)}";

	public void DeserializeData(string data)
	{
		string[] parts = data.Split('|');
		Register = parts[0];
		Amount = int.Parse(parts[1], CultureInfo.InvariantCulture);
	}

	public static string NewRegister() => Guid.NewGuid().ToString("N");

	public static int Read(string register) => Registers.TryGetValue(register, out int value) ? value : 0;
}

/// <summary>
/// An insertion into a text buffer that merges with the insertion straight after it, as typing does
/// </summary>
internal sealed class InsertText(StrongBox<string> buffer, int position, string text) : BaseCommand(ChangeType.Insert, ["text"])
{
	public int Position { get; } = position;

	public string Text { get; } = text;

	public override string Description => $"Insert '{Text}' at {Position}";

	public override void Execute() => buffer.Value = buffer.Value.Insert(Position, Text);

	public override void Undo() => buffer.Value = buffer.Value.Remove(Position, Text.Length);

	public override bool CanMergeWith(ICommand other) => other is InsertText next && next.Position == Position + Text.Length;

	public override ICommand MergeWith(ICommand other) => new InsertText(buffer, Position, Text + ((InsertText)other).Text);
}
