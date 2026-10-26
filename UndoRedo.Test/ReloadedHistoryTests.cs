// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Test;

using ktsu.UndoRedo.Core.Services;
using static ktsu.UndoRedo.Test.HistoryTestSupport;

/// <summary>
/// What a history loaded from saved state holds: rebuilt composites, placeholders that say so, and custom
/// data with its values' types
/// </summary>
[TestClass]
public sealed class ReloadedHistoryTests
{
	private static async Task<UndoRedoService> ReloadAsync(UndoRedoService service)
	{
		byte[] data = await service.SaveStateAsync().ConfigureAwait(false);
		UndoRedoService loaded = JsonService();
		Assert.IsTrue(await loaded.LoadStateAsync(data).ConfigureAwait(false));
		return loaded;
	}

	[TestMethod]
	public async Task ACompositeOfSerializableCommands_IsRebuilt()
	{
		string register = AddToRegister.NewRegister();
		UndoRedoService service = JsonService();
		service.Execute(new AddToRegister(register, 1));
		service.Execute(new CompositeCommand("group", [new AddToRegister(register, 10), new AddToRegister(register, 100)], "file:2:3"));
		Assert.AreEqual(111, AddToRegister.Read(register));

		UndoRedoService loaded = await ReloadAsync(service).ConfigureAwait(false);

		CompositeCommand composite = (CompositeCommand)loaded.Commands[1];
		Assert.AreEqual("group", composite.Description);
		Assert.AreEqual("file:2:3", composite.NavigationContext);
		Assert.AreEqual(service.Commands[1].Metadata.Timestamp, composite.Metadata.Timestamp);
		Assert.HasCount(2, composite.Commands);
		Assert.AreEqual(10, ((AddToRegister)composite.Commands[0]).Amount);
		Assert.AreEqual(100, ((AddToRegister)composite.Commands[1]).Amount);

		Assert.IsTrue(await loaded.UndoAsync().ConfigureAwait(false));
		Assert.AreEqual(1, AddToRegister.Read(register));
		Assert.IsTrue(await loaded.UndoAsync().ConfigureAwait(false));
		Assert.AreEqual(0, AddToRegister.Read(register));
		Assert.IsTrue(await loaded.RedoAsync().ConfigureAwait(false));
		Assert.IsTrue(await loaded.RedoAsync().ConfigureAwait(false));
		Assert.AreEqual(111, AddToRegister.Read(register));
	}

	[TestMethod]
	public async Task ACompositeHoldingADelegateCommand_LoadsAsAPlaceholderAndSavesBackWhole()
	{
		string register = AddToRegister.NewRegister();
		StrongBox<int> value = new(0);
		UndoRedoService service = JsonService();
		service.Execute(new CompositeCommand("group", [new AddToRegister(register, 10), Increment(value)]));

		UndoRedoService loaded = await ReloadAsync(service).ConfigureAwait(false);
		Assert.IsInstanceOfType<IPlaceholderCommand>(loaded.Commands[0]);
		Assert.AreEqual("group", ((IPlaceholderCommand)loaded.Commands[0]).OriginalDescription);

		// Saving the placeholder again keeps the sub-commands, so nothing is lost if a later load can rebuild them
		UndoRedoService again = await ReloadAsync(loaded).ConfigureAwait(false);
		Assert.IsInstanceOfType<IPlaceholderCommand>(again.Commands[0]);
		byte[] first = await service.SaveStateAsync().ConfigureAwait(false);
		byte[] second = await again.SaveStateAsync().ConfigureAwait(false);
		StringAssert.Contains(System.Text.Encoding.UTF8.GetString(second), $"{register}|10");
		Assert.IsGreaterThan(0, first.Length);
	}

	[TestMethod]
	public async Task APlaceholder_CannotBeUndoneAndSaysSo()
	{
		StrongBox<int> value = new(0);
		UndoRedoService service = JsonService();
		service.Execute(Increment(value));

		UndoRedoService loaded = await ReloadAsync(service).ConfigureAwait(false);

		Assert.IsInstanceOfType<IPlaceholderCommand>(loaded.Commands[0]);
		Assert.IsFalse(loaded.CanUndo);
		Assert.IsFalse(await loaded.UndoAsync().ConfigureAwait(false));
		Assert.AreEqual(0, loaded.CurrentPosition);
	}

	[TestMethod]
	public async Task APlaceholder_CannotBeRedoneAndSaysSo()
	{
		string register = AddToRegister.NewRegister();
		StrongBox<int> value = new(0);
		UndoRedoService service = JsonService();
		service.Execute(new AddToRegister(register, 1));
		service.Execute(Increment(value));
		await service.UndoAsync().ConfigureAwait(false);

		UndoRedoService loaded = await ReloadAsync(service).ConfigureAwait(false);

		Assert.AreEqual(0, loaded.CurrentPosition);
		Assert.IsTrue(loaded.CanUndo, "The rebuilt command below can still be undone");
		Assert.IsFalse(loaded.CanRedo);
		Assert.IsFalse(await loaded.RedoAsync().ConfigureAwait(false));
		Assert.AreEqual(0, loaded.CurrentPosition);
	}

	[TestMethod]
	public async Task RevertingAcrossAPlaceholder_ChangesNothing()
	{
		string register = AddToRegister.NewRegister();
		StrongBox<int> value = new(0);
		UndoRedoService service = JsonService();
		service.MarkAsSaved();
		service.Execute(Increment(value));
		service.Execute(new AddToRegister(register, 1));

		UndoRedoService loaded = await ReloadAsync(service).ConfigureAwait(false);

		Assert.IsFalse(await loaded.UndoToSaveBoundaryAsync(loaded.SaveBoundaries[0]).ConfigureAwait(false));
		Assert.AreEqual(1, loaded.CurrentPosition);
		Assert.AreEqual(1, AddToRegister.Read(register), "The command above the placeholder was not undone either");
	}

	[TestMethod]
	public async Task CustomData_KeepsItsValuesTypes()
	{
		UndoRedoService service = JsonService();
		Dictionary<string, object> customData = new()
		{
			["line"] = 42,
			["name"] = "x",
			["ratio"] = 0.5,
			["flag"] = true,
			["list"] = new[] { 1, 2 },
			["nested"] = new Dictionary<string, object> { ["inner"] = "y" },
		};
		service.Execute(new DelegateCommand("a", () => { }, () => { }, customData: customData));

		UndoRedoService loaded = await ReloadAsync(service).ConfigureAwait(false);

		IReadOnlyDictionary<string, object> data = loaded.Commands[0].Metadata.CustomData!;
		Assert.AreEqual(typeof(long), data["line"].GetType(), $"A whole number loads back as a long, not {data["line"].GetType()}");
		Assert.AreEqual(42L, (long)data["line"]);
		Assert.AreEqual("x", data["name"]);
		Assert.AreEqual(0.5, data["ratio"]);
		Assert.IsTrue((bool)data["flag"]);
		CollectionAssert.AreEqual(new List<object> { 1L, 2L }, (List<object>)data["list"]);
		Assert.AreEqual("y", ((Dictionary<string, object>)data["nested"])["inner"]);
		Assert.IsTrue(data["line"] is long line && line == 42);
	}
}
