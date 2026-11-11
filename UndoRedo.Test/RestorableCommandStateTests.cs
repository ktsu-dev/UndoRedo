// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Test;

using System.Globalization;
using ktsu.UndoRedo.Core.Services;
using static HistoryTestSupport;

/// <summary>
/// A command that implements the interfaces directly, rather than deriving from BaseCommand, gets its
/// navigation context and metadata back after a save and load (#164)
/// </summary>
[TestClass]
public sealed class RestorableCommandStateTests
{
	[TestMethod]
	public async Task DirectCommand_ImplementingIRestorableCommandState_RoundTripsNavigationContextAndMetadata()
	{
		DateTimeOffset when = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
		UndoRedoService saved = JsonService();
		saved.Execute(new SetValue
		{
			Value = 5,
			NavigationContext = "line:42",
			Metadata = new ChangeMetadata(ChangeType.Modify, ["field"], when, 7),
		});
		byte[] data = await saved.SaveStateAsync().ConfigureAwait(false);

		UndoRedoService loaded = JsonService();
		Assert.IsTrue(await loaded.LoadStateAsync(data).ConfigureAwait(false));

		SetValue reloaded = (SetValue)loaded.Commands[0];
		Assert.AreEqual(5, reloaded.Value);
		Assert.AreEqual("line:42", reloaded.NavigationContext);
		Assert.AreEqual(ChangeType.Modify, reloaded.Metadata.ChangeType);
		Assert.HasCount(1, reloaded.Metadata.AffectedItems);
		Assert.AreEqual("field", reloaded.Metadata.AffectedItems[0]);
		Assert.AreEqual(when, reloaded.Metadata.Timestamp);
		Assert.AreEqual(7, reloaded.Metadata.Size);
	}

	[TestMethod]
	public void BaseCommand_ImplementsIRestorableCommandState() =>
		Assert.IsInstanceOfType<IRestorableCommandState>(new AddToRegister("r", 1));

#pragma warning disable CA1812 // Instantiated by reflection during deserialization
	private sealed class SetValue : ICommand, ISerializableCommand, IRestorableCommandState
#pragma warning restore CA1812
	{
		public int Value { get; set; }

		public string Description => $"Set {Value}";

		public string? NavigationContext { get; set; }

		public ChangeMetadata Metadata { get; set; } = new(ChangeType.Modify, [], DateTimeOffset.Now);

		public void Execute() { }

		public void Undo() { }

		public bool CanMergeWith(ICommand other) => false;

		public ICommand MergeWith(ICommand other) => throw new InvalidOperationException();

		public string SerializeData() => Value.ToString(CultureInfo.InvariantCulture);

		public void DeserializeData(string data) => Value = int.Parse(data, CultureInfo.InvariantCulture);

		public void RestoreSerializedState(string? navigationContext, ChangeMetadata metadata)
		{
			NavigationContext = navigationContext;
			Metadata = metadata;
		}
	}
}
