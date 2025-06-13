using ktsu.UndoRedo.Core;
using ktsu.UndoRedo.Core.Services;

namespace StandaloneDemo;

/// <summary>
/// Standalone demo showcasing ktsu.UndoRedo features including serialization
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🔄 ktsu.UndoRedo Feature Demo");
        Console.WriteLine("=============================");
        Console.WriteLine();

        await BasicUndoRedoDemo();
        await SerializationDemo();
        await CompositeCommandDemo();
        await SaveBoundaryDemo();

        Console.WriteLine("✅ All demos completed successfully!");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    /// <summary>
    /// Demonstrates basic undo/redo functionality
    /// </summary>
    static async Task BasicUndoRedoDemo()
    {
        Console.WriteLine("📝 Demo 1: Basic Undo/Redo Operations");
        Console.WriteLine("-------------------------------------");

        var undoRedoStack = new UndoRedoStack();
        var value = 0;

        // Set up event handlers for feedback
        undoRedoStack.CommandExecuted += (_, e) => Console.WriteLine($"  ✅ Executed: {e.Command.Description}");
        undoRedoStack.CommandUndone += (_, e) => Console.WriteLine($"  ↩️  Undone: {e.Command.Description}");
        undoRedoStack.CommandRedone += (_, e) => Console.WriteLine($"  ↪️  Redone: {e.Command.Description}");

        // Execute some commands
        undoRedoStack.Execute(new DelegateCommand("Set value to 10", () => value = 10, () => value = 0));
        undoRedoStack.Execute(new DelegateCommand("Add 5", () => value += 5, () => value -= 5));
        undoRedoStack.Execute(new DelegateCommand("Multiply by 2", () => value *= 2, () => value /= 2));

        Console.WriteLine($"  Current value: {value}"); // Should be 30

        // Undo operations
        await undoRedoStack.UndoAsync(navigateToChange: false);
        Console.WriteLine($"  After undo: {value}"); // Should be 15

        await undoRedoStack.UndoAsync(navigateToChange: false);
        Console.WriteLine($"  After undo: {value}"); // Should be 10

        // Redo operation
        await undoRedoStack.RedoAsync(navigateToChange: false);
        Console.WriteLine($"  After redo: {value}"); // Should be 15

        Console.WriteLine($"  Stack state: {undoRedoStack.CommandCount} commands, position {undoRedoStack.CurrentPosition}");
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates serialization and persistence
    /// </summary>
    static async Task SerializationDemo()
    {
        Console.WriteLine("💾 Demo 2: Serialization and Persistence");
        Console.WriteLine("----------------------------------------");

        // Create first stack with serialization
        var stack1 = new UndoRedoStack();
        stack1.SetSerializer(new JsonUndoRedoSerializer());

        var text = "";

        // Execute commands
        stack1.Execute(new DelegateCommand("Add 'Hello'", () => text += "Hello", () => text = text[..^5]));
        stack1.Execute(new DelegateCommand("Add ' World'", () => text += " World", () => text = text[..^6]));
        stack1.Execute(new DelegateCommand("Add '!'", () => text += "!", () => text = text[..^1]));

        Console.WriteLine($"  Original stack text: '{text}'");
        Console.WriteLine($"  Commands: {stack1.CommandCount}, Position: {stack1.CurrentPosition}");

        // Serialize the stack
        var serializedData = await stack1.SaveStateAsync();
        Console.WriteLine($"  ✅ Serialized stack to {serializedData.Length} bytes");

        // Create new stack and deserialize
        var stack2 = new UndoRedoStack();
        stack2.SetSerializer(new JsonUndoRedoSerializer());

        bool loadSuccess = await stack2.LoadStateAsync(serializedData);
        Console.WriteLine($"  ✅ Deserialization success: {loadSuccess}");
        Console.WriteLine($"  Restored commands: {stack2.CommandCount}, Position: {stack2.CurrentPosition}");

        // Verify state preservation
        var state = stack2.GetCurrentState();
        Console.WriteLine($"  State timestamp: {state.Timestamp:HH:mm:ss}");
        Console.WriteLine($"  Format version: {state.FormatVersion}");
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates composite commands
    /// </summary>
    static async Task CompositeCommandDemo()
    {
        Console.WriteLine("🔗 Demo 3: Composite Commands");
        Console.WriteLine("-----------------------------");

        var undoRedoStack = new UndoRedoStack();
        var values = new List<string>();

        // Create individual commands
        var commands = new[]
        {
            new DelegateCommand("Add A", () => values.Add("A"), () => values.RemoveAt(values.Count - 1)),
            new DelegateCommand("Add B", () => values.Add("B"), () => values.RemoveAt(values.Count - 1)),
            new DelegateCommand("Add C", () => values.Add("C"), () => values.RemoveAt(values.Count - 1))
        };

        // Create composite command
        var composite = new CompositeCommand("Add A, B, C", commands);

        // Execute composite
        undoRedoStack.Execute(composite);
        Console.WriteLine($"  After composite execute: [{string.Join(", ", values)}]");
        Console.WriteLine($"  Commands in stack: {undoRedoStack.CommandCount} (should be 1 composite)");

        // Undo composite (undoes all sub-commands at once)
        await undoRedoStack.UndoAsync(navigateToChange: false);
        Console.WriteLine($"  After composite undo: [{string.Join(", ", values)}]");

        // Redo composite
        await undoRedoStack.RedoAsync(navigateToChange: false);
        Console.WriteLine($"  After composite redo: [{string.Join(", ", values)}]");

        // Show visualization
        var visualizations = undoRedoStack.GetChangeVisualizations().ToList();
        Console.WriteLine("  Command visualization:");
        foreach (var viz in visualizations)
        {
            var marker = viz.IsExecuted ? "✓" : "○";
            Console.WriteLine($"    {marker} {viz.Command.Description}");
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates save boundaries
    /// </summary>
    static async Task SaveBoundaryDemo()
    {
        Console.WriteLine("💾 Demo 4: Save Boundaries");
        Console.WriteLine("--------------------------");

        var undoRedoStack = new UndoRedoStack();
        var document = "";

        // Set up save boundary event
        undoRedoStack.SaveBoundaryCreated += (_, e) =>
            Console.WriteLine($"  💾 Save boundary: {e.SaveBoundary.Description}");

        // Execute some commands
        undoRedoStack.Execute(new DelegateCommand("Type 'Hello'", () => document += "Hello", () => document = document[..^5]));
        undoRedoStack.Execute(new DelegateCommand("Type ' World'", () => document += " World", () => document = document[..^6]));

        // Mark as saved
        undoRedoStack.MarkAsSaved("First save");
        Console.WriteLine($"  Document after save: '{document}'");
        Console.WriteLine($"  Has unsaved changes: {undoRedoStack.HasUnsavedChanges}");

        // Make more changes
        undoRedoStack.Execute(new DelegateCommand("Add '!'", () => document += "!", () => document = document[..^1]));
        undoRedoStack.Execute(new DelegateCommand("Add '?'", () => document += "?", () => document = document[..^1]));

        Console.WriteLine($"  After more changes: '{document}'");
        Console.WriteLine($"  Has unsaved changes: {undoRedoStack.HasUnsavedChanges}");

        // Show save boundaries
        Console.WriteLine("  Save boundaries:");
        foreach (var boundary in undoRedoStack.SaveBoundaries)
        {
            Console.WriteLine($"    Position {boundary.Position}: {boundary.Description} ({boundary.Timestamp:HH:mm:ss})");
        }

        // Undo to save boundary
        var saveBoundary = undoRedoStack.SaveBoundaries.FirstOrDefault();
        if (saveBoundary != null)
        {
            var commandsToUndo = undoRedoStack.GetCommandsToUndo(saveBoundary).ToList();
            Console.WriteLine($"  Commands to undo to reach save: {commandsToUndo.Count}");

            await undoRedoStack.UndoToSaveBoundaryAsync(saveBoundary, navigateToLastChange: false);
            Console.WriteLine($"  After undo to save: '{document}'");
            Console.WriteLine($"  Has unsaved changes: {undoRedoStack.HasUnsavedChanges}");
        }

        // Show final visualization
        var visualizations = undoRedoStack.GetChangeVisualizations().ToList();
        Console.WriteLine("  Final command history:");
        foreach (var viz in visualizations)
        {
            var marker = viz.IsExecuted ? "✓" : "○";
            var saveMarker = viz.HasSaveBoundary ? " 💾" : "";
            Console.WriteLine($"    {marker} {viz.Command.Description}{saveMarker}");
        }
        Console.WriteLine();
    }
}
