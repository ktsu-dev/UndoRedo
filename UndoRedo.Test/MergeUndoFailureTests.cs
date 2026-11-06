// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Test;

using ktsu.UndoRedo.Models;
using ktsu.UndoRedo.Core.Services;
using static ktsu.UndoRedo.Test.HistoryTestSupport;

/// <summary>
/// With auto-merge on, a previous command whose Undo throws must not stop a new mergeable edit
/// from being applied: merging is only an optimisation
/// </summary>
[TestClass]
public sealed class MergeUndoFailureTests
{
	[TestMethod]
	public void APreviousCommandWhoseUndoThrows_StillLetsAMergeableEditApply()
	{
		StrongBox<string> buffer = new(string.Empty);
		UndoRedoService service = Service(UndoRedoOptions.Create(autoMerge: true));
		List<ICommand> executed = [];
		List<ICommand> discarded = [];
		service.CommandExecuted += (_, e) => executed.Add(e.Command);
		service.CommandsDiscarded += (_, e) => discarded.AddRange(e.Commands);
		Typing a = new(buffer, "abc") { UndoThrows = true };
		Typing b = new(buffer, "d");

		service.Execute(a);
		service.Execute(b);

		Assert.AreEqual("abcd", buffer.Value);
		CollectionAssert.AreEqual(new ICommand[] { a, b }, executed);
		CollectionAssert.AreEqual(new ICommand[] { a, b }, service.Commands.ToArray());
		Assert.AreEqual(1, service.CurrentPosition);
		Assert.AreEqual(0, discarded.Count);
	}

	[TestMethod]
	public void AfterTheFallback_TheNextEditMergesIntoTheNewCommand()
	{
		StrongBox<string> buffer = new(string.Empty);
		UndoRedoService service = Service(UndoRedoOptions.Create(autoMerge: true));
		Typing a = new(buffer, "abc") { UndoThrows = true };

		service.Execute(a);
		service.Execute(new Typing(buffer, "d"));
		service.Execute(new Typing(buffer, "e"));

		Assert.AreEqual("abcde", buffer.Value);
		Assert.AreEqual(2, service.Commands.Count);
		Assert.AreSame(a, service.Commands[0]);
		Assert.AreEqual("Type 'de'", service.Commands[1].Description);
		service.Undo();
		Assert.AreEqual("abc", buffer.Value);
	}

	/// <summary>
	/// Typing appended to a buffer, which merges with the next keystroke
	/// </summary>
	private sealed class Typing(StrongBox<string> buffer, string text) : BaseCommand(ChangeType.Insert, ["text"])
	{
		public string Text { get; } = text;

		public bool UndoThrows { get; init; }

		public override string Description => $"Type '{Text}'";

		public override void Execute() => buffer.Value += Text;

		public override void Undo()
		{
			if (UndoThrows)
			{
				throw new InvalidOperationException("The target buffer was replaced");
			}

			buffer.Value = buffer.Value[..^Text.Length];
		}

		public override bool CanMergeWith(ICommand other) => other is Typing;

		public override ICommand MergeWith(ICommand other) => new Typing(buffer, Text + ((Typing)other).Text);
	}
}
