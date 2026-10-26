// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Test;

using ktsu.UndoRedo.Contracts;
using ktsu.UndoRedo.Core.Services;
using ktsu.UndoRedo.Models;

/// <summary>
/// The examples README.md and docs/serialization.md give, compiled as written apart from what they leave
/// to the reader, so they can't drift from what the library does
/// </summary>
[TestClass]
public sealed class DocumentedExampleTests
{
	internal interface ITextEditor
	{
		public void ReplaceText(int position, int length, string text);
	}

	private sealed class Buffer : ITextEditor
	{
		public string Text { get; private set; } = string.Empty;

		public void ReplaceText(int position, int length, string text) => Text = Text.Remove(position, length).Insert(position, text);
	}

	/// <summary>
	/// README.md, "Custom Commands"
	/// </summary>
	private sealed class TextEditCommand(ITextEditor editor, int position, string oldText, string newText)
		: BaseCommand(ChangeType.Modify, [$"text:{position}"], $"editor:{position}")
	{
		private readonly ITextEditor _editor = editor;
		private readonly int _position = position;
		private readonly string _oldText = oldText;
		private readonly string _newText = newText;

		public override string Description => $"Replace '{_oldText}' with '{_newText}'";

		public override void Execute() => _editor.ReplaceText(_position, _oldText.Length, _newText);

		public override void Undo() => _editor.ReplaceText(_position, _newText.Length, _oldText);

		public override bool CanMergeWith(ICommand other) =>
			other is TextEditCommand textCmd &&
			textCmd._oldText.Length == 0 &&
			textCmd._newText.Length == 1 &&
			textCmd._position == _position + _newText.Length;

		public override ICommand MergeWith(ICommand other)
		{
			TextEditCommand textCmd = (TextEditCommand)other;
			return new TextEditCommand(_editor, _position, _oldText, _newText + textCmd._newText);
		}
	}

	[TestMethod]
	public void TheReadmeTypingCommand_MergesAWholeWordIntoOneStep()
	{
		Buffer buffer = new();
		UndoRedoService undoRedoStack = UndoRedoService.Create();

		string word = "hello";
		for (int i = 0; i < word.Length; i++)
		{
			undoRedoStack.Execute(new TextEditCommand(buffer, i, string.Empty, word[i].ToString()));
		}

		Assert.AreEqual("hello", buffer.Text);
		Assert.AreEqual(1, undoRedoStack.CommandCount);
		undoRedoStack.Undo();
		Assert.AreEqual(string.Empty, buffer.Text);
	}

	/// <summary>
	/// docs/serialization.md, "Custom Serializers", with the command stubs filled in
	/// </summary>
	private sealed class BinaryUndoRedoSerializer : IUndoRedoSerializer
	{
		public string FormatVersion => "binary-v1.0";

		public bool SupportsVersion(string version) => version.StartsWith("binary-v1.", StringComparison.Ordinal);

		public Task<byte[]> SerializeAsync(
			IReadOnlyList<ICommand> commands,
			int currentPosition,
			IReadOnlyList<SaveBoundary> saveBoundaries,
			bool initialStateIsClean,
			CancellationToken cancellationToken = default)
		{
			using MemoryStream stream = new();
			using BinaryWriter writer = new(stream);

			writer.Write(FormatVersion);
			writer.Write(DateTime.UtcNow.ToBinary());

			writer.Write(commands.Count);
			writer.Write(currentPosition);
			writer.Write(initialStateIsClean);

			foreach (ICommand command in commands)
			{
				writer.Write(command.Description);
			}

			writer.Write(saveBoundaries.Count);
			foreach (SaveBoundary boundary in saveBoundaries)
			{
				writer.Write(boundary.Position);
				writer.Write(boundary.Timestamp.UtcTicks);
				writer.Write(boundary.Description ?? string.Empty);
			}

			writer.Flush();
			return Task.FromResult(stream.ToArray());
		}

		public Task<UndoRedoStackState> DeserializeAsync(byte[] data, CancellationToken cancellationToken = default)
		{
			using MemoryStream stream = new(data);
			using BinaryReader reader = new(stream);

			string version = reader.ReadString();
			if (!SupportsVersion(version))
			{
				throw new NotSupportedException($"Unsupported version: {version}");
			}

			DateTime timestamp = DateTime.FromBinary(reader.ReadInt64());

			int commandCount = reader.ReadInt32();
			int currentPosition = reader.ReadInt32();
			bool initialStateIsClean = reader.ReadBoolean();

			List<ICommand> commands = [];
			for (int i = 0; i < commandCount; i++)
			{
				commands.Add(new DelegateCommand(reader.ReadString(), () => { }, () => { }));
			}

			int boundaryCount = reader.ReadInt32();
			List<SaveBoundary> boundaries = [];
			for (int i = 0; i < boundaryCount; i++)
			{
				int position = reader.ReadInt32();
				DateTimeOffset boundaryTime = new(reader.ReadInt64(), TimeSpan.Zero);
				string description = reader.ReadString();
				boundaries.Add(new SaveBoundary(position, description, boundaryTime));
			}

			return Task.FromResult(new UndoRedoStackState(commands, currentPosition, boundaries, version, timestamp)
			{
				InitialStateIsClean = initialStateIsClean,
			});
		}
	}

	[TestMethod]
	public async Task TheDocumentedBinarySerializer_KeepsATrimmedHistoryUnsaved()
	{
		UndoRedoService service = UndoRedoService.Create(UndoRedoOptions.Create(maxStackSize: 1));
		service.SetSerializer(new BinaryUndoRedoSerializer());
		service.Execute(new DelegateCommand("a", () => { }, () => { }));
		service.Execute(new DelegateCommand("b", () => { }, () => { }));
		await service.UndoAsync().ConfigureAwait(false);
		Assert.IsTrue(service.HasUnsavedChanges);

		byte[] data = await service.SaveStateAsync().ConfigureAwait(false);
		UndoRedoService loaded = UndoRedoService.Create();
		loaded.SetSerializer(new BinaryUndoRedoSerializer());
		Assert.IsTrue(await loaded.LoadStateAsync(data).ConfigureAwait(false));

		Assert.AreEqual(-1, loaded.CurrentPosition);
		Assert.IsTrue(loaded.HasUnsavedChanges);
	}
}
