// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.UndoRedo.Test;

using System;
using System.Collections.Generic;
using System.Linq;
using ktsu.UndoRedo.Core;

[TestClass]
public class CompositeCommandTests
{
	[TestMethod]
	public void CompositeCommand_EmptyCommandList_ThrowsException()
	{
		// Act & Assert
		Assert.ThrowsException<ArgumentException>(() =>
			new CompositeCommand("Empty", []));
	}

	[TestMethod]
	public void CompositeCommand_NullCommandList_ThrowsException()
	{
		// Act & Assert
		Assert.ThrowsException<ArgumentNullException>(() =>
			new CompositeCommand("Null", null!));
	}

	[TestMethod]
	public void CompositeCommand_WithFailingCommand_RollsBackSuccessfulCommands()
	{
		// Arrange
		List<string> values = [];
		ICommand[] commands =
		[
			new DelegateCommand("Add A", () => values.Add("A"), () => values.RemoveAt(values.Count - 1)),
			new DelegateCommand("Add B", () => values.Add("B"), () => values.RemoveAt(values.Count - 1)),
			new DelegateCommand("Fail", () => throw new InvalidOperationException("Test failure"), () => { }),
			new DelegateCommand("Add C", () => values.Add("C"), () => values.RemoveAt(values.Count - 1))
		];

		CompositeCommand composite = new("Test Composite", commands);

		// Act & Assert
		Assert.ThrowsException<InvalidOperationException>(composite.Execute);

		// All successful commands should have been rolled back
		Assert.AreEqual(0, values.Count);
	}

	[TestMethod]
	public void CompositeCommand_NestedFailure_RollsBackCompleteHierarchy()
	{
		// Arrange
		List<string> values = [];

		CompositeCommand innerComposite = new("Inner",
		[
			new DelegateCommand("Add X", () => values.Add("X"), () => values.RemoveAt(values.Count - 1)),
			new DelegateCommand("Fail", () => throw new InvalidOperationException("Inner failure"), () => { })
		]);

		CompositeCommand outerComposite = new("Outer",
		[
			new DelegateCommand("Add A", () => values.Add("A"), () => values.RemoveAt(values.Count - 1)),
			innerComposite,
			new DelegateCommand("Add B", () => values.Add("B"), () => values.RemoveAt(values.Count - 1))
		]);

		// Act & Assert
		Assert.ThrowsException<InvalidOperationException>(outerComposite.Execute);
		Assert.AreEqual(0, values.Count); // Everything should be rolled back
	}
	private static readonly string[] expected = ["A", "B", "C"];

	[TestMethod]
	public void CompositeCommand_UndoFailure_DoesNotAffectOtherCommands()
	{
		// Arrange
		List<string> values = [];
		bool shouldFailUndo = false;

		ICommand[] commands =
		[
			new DelegateCommand("Add A", () => values.Add("A"), () => values.RemoveAt(values.Count - 1)),
			new DelegateCommand("Add B", () => values.Add("B"), () =>
			{
				if (shouldFailUndo)
				{
					throw new InvalidOperationException("Undo failure");
				}
				values.RemoveAt(values.Count - 1);
			}),
			new DelegateCommand("Add C", () => values.Add("C"), () => values.RemoveAt(values.Count - 1))
		];

		CompositeCommand composite = new("Test", commands);

		// Act
		composite.Execute();
		Assert.AreEqual(3, values.Count);
		CollectionAssert.AreEqual(expected, values);

		// Make undo fail for middle command
		shouldFailUndo = true;

		// Assert - Undo should throw but still attempt to undo all commands
		Assert.ThrowsException<InvalidOperationException>(composite.Undo);

		// Commands should still be partially undone (C and A undone from end, B failed)
		Assert.AreEqual(1, values.Count);
		Assert.AreEqual("A", values[0]);  // A remains because B's undo failed, C was undone, A tried to undo but only removes from end
	}

	[TestMethod]
	public void CompositeCommand_DeepNesting_ExecutesInCorrectOrder()
	{
		// Arrange
		List<string> executionOrder = [];

		CompositeCommand level3 = new("Level3",
		[
			new DelegateCommand("L3-1", () => executionOrder.Add("L3-1"), () => executionOrder.Add("U-L3-1")),
			new DelegateCommand("L3-2", () => executionOrder.Add("L3-2"), () => executionOrder.Add("U-L3-2"))
		]);

		CompositeCommand level2 = new("Level2",
		[
			new DelegateCommand("L2-1", () => executionOrder.Add("L2-1"), () => executionOrder.Add("U-L2-1")),
			level3,
			new DelegateCommand("L2-2", () => executionOrder.Add("L2-2"), () => executionOrder.Add("U-L2-2"))
		]);

		CompositeCommand level1 = new("Level1",
		[
			new DelegateCommand("L1-1", () => executionOrder.Add("L1-1"), () => executionOrder.Add("U-L1-1")),
			level2,
			new DelegateCommand("L1-2", () => executionOrder.Add("L1-2"), () => executionOrder.Add("U-L1-2"))
		]);

		// Act
		level1.Execute();

		// Assert execution order
		string[] expectedExecution = ["L1-1", "L2-1", "L3-1", "L3-2", "L2-2", "L1-2"];
		CollectionAssert.AreEqual(expectedExecution, executionOrder.Take(6).ToArray());

		// Act - Undo
		level1.Undo();

		// Assert undo order (reverse)
		string[] expectedUndo = ["U-L1-2", "U-L2-2", "U-L3-2", "U-L3-1", "U-L2-1", "U-L1-1"];
		CollectionAssert.AreEqual(expectedUndo, executionOrder.Skip(6).ToArray());
	}

	[TestMethod]
	public void CompositeCommand_Metadata_AggregatesFromChildCommands()
	{
		// Arrange
		ICommand[] commands =
		[
			new DelegateCommand("Cmd1", () => { }, () => { }, ChangeType.Insert, ["item1", "item2"]),
			new DelegateCommand("Cmd2", () => { }, () => { }, ChangeType.Modify, ["item2", "item3"]),
			new DelegateCommand("Cmd3", () => { }, () => { }, ChangeType.Delete, ["item4"])
		];

		CompositeCommand composite = new("Multi-op", commands, "context1");

		// Act & Assert
		Assert.AreEqual("Multi-op", composite.Description);
		Assert.AreEqual("context1", composite.NavigationContext);

		// Should aggregate all affected items
		string[] expectedItems = ["item1", "item2", "item3", "item4"];
		CollectionAssert.AreEquivalent(expectedItems, composite.Metadata.AffectedItems.ToArray());

		// Change type should be composite for composite
		Assert.AreEqual(ChangeType.Composite, composite.Metadata.ChangeType);
	}

	[TestMethod]
	public void CompositeCommand_CanMergeWith_ReturnsFalse()
	{
		// Arrange
		ICommand[] commands =
		[
			new DelegateCommand("Test", () => { }, () => { })
		];

		CompositeCommand composite1 = new("Composite1", commands);
		CompositeCommand composite2 = new("Composite2", commands);

		// Act & Assert
		Assert.IsFalse(composite1.CanMergeWith(composite2));
		Assert.ThrowsException<NotSupportedException>(() => composite1.MergeWith(composite2));
	}

	[TestMethod]
	public void CompositeCommand_LargeNumberOfCommands_PerformanceTest()
	{
		// Arrange
		List<ICommand> commands = [];
		List<int> values = [];

		// Create 1000 commands
		for (int i = 0; i < 1000; i++)
		{
			int localI = i;
			commands.Add(new DelegateCommand($"Add {localI}",
				() => values.Add(localI),
				() => values.RemoveAt(values.Count - 1)));
		}

		CompositeCommand composite = new("Large Composite", commands);
		System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

		// Act
		composite.Execute();
		stopwatch.Stop();

		// Assert
		Assert.AreEqual(1000, values.Count);
		Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1000, "Execution should be fast");

		// Test undo performance
		stopwatch.Restart();
		composite.Undo();
		stopwatch.Stop();

		Assert.AreEqual(0, values.Count);
		Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1000, "Undo should be fast");
	}
}
