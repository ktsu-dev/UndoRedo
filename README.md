# ktsu.UndoRedo

A comprehensive .NET library for implementing undo/redo functionality with advanced features including save boundaries, change visualization, and external navigation integration.

## Overview

ktsu.UndoRedo provides a robust and flexible undo/redo stack implementation that goes beyond basic command pattern implementations. It's designed for applications that need sophisticated change tracking, visual feedback, and integration with navigation systems.

## Features

-   **Command Pattern Implementation**: Clean, extensible command interface
-   **Save Boundaries**: Track which changes have been saved and identify unsaved work
-   **Change Visualization**: Rich metadata for displaying change history in UI
-   **Navigation Integration**: Automatically navigate to where changes were made during undo/redo
-   **Command Merging**: Intelligent merging of related commands (e.g., typing)
-   **Composite Commands**: Group multiple operations into atomic units
-   **Events**: Comprehensive event system for UI synchronization
-   **Stack Management**: Configurable stack size limits and automatic cleanup
-   **Async Support**: Full async/await support for navigation operations

## Installation

Add the NuGet package:

```bash
dotnet add package ktsu.UndoRedo
```

## Quick Start

### Basic Usage

```csharp
using ktsu.UndoRedo.Core;

// Create an undo/redo stack
var undoRedoStack = new UndoRedoStack();

// Create a simple command using delegates
var command = new DelegateCommand(
    description: "Set value to 42",
    executeAction: () => myObject.Value = 42,
    undoAction: () => myObject.Value = oldValue,
    changeType: ChangeType.Modify,
    affectedItems: new[] { "myObject.Value" }
);

// Execute the command
undoRedoStack.Execute(command);

// Undo and redo
if (undoRedoStack.CanUndo)
    undoRedoStack.Undo();

if (undoRedoStack.CanRedo)
    undoRedoStack.Redo();
```

### Save Boundaries

```csharp
// Mark the current state as saved
undoRedoStack.MarkAsSaved("Auto-save checkpoint");

// Check if there are unsaved changes
if (undoRedoStack.HasUnsavedChanges)
{
    // Prompt user to save or undo to last save point
    var lastSave = undoRedoStack.SaveBoundaries.LastOrDefault();
    if (lastSave != null)
    {
        await undoRedoStack.UndoToSaveBoundaryAsync(lastSave);
    }
}
```

### Navigation Integration

```csharp
// Implement navigation provider
public class MyNavigationProvider : INavigationProvider
{
    public async Task<bool> NavigateToAsync(string context, CancellationToken cancellationToken = default)
    {
        // Navigate to the location where the change was made
        // context might be something like "file:line:column" or "elementId"
        return await NavigateToLocation(context);
    }

    public bool IsValidContext(string context) => !string.IsNullOrEmpty(context);
}

// Set up navigation
var navigationProvider = new MyNavigationProvider();
undoRedoStack.SetNavigationProvider(navigationProvider);

// Commands with navigation context will automatically navigate on undo/redo
var command = new DelegateCommand(
    "Edit text",
    executeAction,
    undoAction,
    navigationContext: "editor:45:12" // Line 45, column 12
);
```

### Custom Commands

```csharp
public class TextEditCommand : BaseCommand
{
    private readonly ITextEditor _editor;
    private readonly int _position;
    private readonly string _oldText;
    private readonly string _newText;

    public override string Description => $"Replace '{_oldText}' with '{_newText}'";

    public TextEditCommand(ITextEditor editor, int position, string oldText, string newText)
        : base(ChangeType.Modify, new[] { $"text:{position}" }, $"editor:{GetLineColumn(position)}")
    {
        _editor = editor;
        _position = position;
        _oldText = oldText;
        _newText = newText;
    }

    public override void Execute()
    {
        _editor.ReplaceText(_position, _oldText.Length, _newText);
    }

    public override void Undo()
    {
        _editor.ReplaceText(_position, _newText.Length, _oldText);
    }

    public override bool CanMergeWith(ICommand other)
    {
        // Allow merging consecutive character insertions
        return other is TextEditCommand textCmd &&
               textCmd._position == _position + _newText.Length &&
               _newText.Length == 1 && textCmd._newText.Length == 1;
    }

    public override ICommand MergeWith(ICommand other)
    {
        var textCmd = (TextEditCommand)other;
        return new TextEditCommand(_editor, _position, _oldText, _newText + textCmd._newText);
    }
}
```

### Composite Commands

```csharp
// Group multiple operations into a single undoable action
var commands = new[]
{
    new DelegateCommand("Move item", () => item.Position = newPos, () => item.Position = oldPos),
    new DelegateCommand("Resize item", () => item.Size = newSize, () => item.Size = oldSize),
    new DelegateCommand("Change color", () => item.Color = newColor, () => item.Color = oldColor)
};

var composite = new CompositeCommand("Transform item", commands, "item:" + item.Id);
undoRedoStack.Execute(composite);
```

### Change Visualization

```csharp
// Get visualization data for UI display
var visualizations = undoRedoStack.GetChangeVisualizations(maxItems: 20);

foreach (var viz in visualizations)
{
    Console.WriteLine($"{(viz.IsExecuted ? "✓" : "○")} {viz.Command.Description}");

    if (viz.HasSaveBoundary)
        Console.WriteLine("  📁 Save point");

    Console.WriteLine($"  📊 {viz.Command.Metadata.ChangeType} affecting {viz.Command.Metadata.AffectedItems.Count} items");
    Console.WriteLine($"  🕒 {viz.Command.Metadata.Timestamp:HH:mm:ss}");
}
```

### Events

```csharp
// Subscribe to events for UI updates
undoRedoStack.CommandExecuted += (sender, e) =>
{
    UpdateUI();
    LogAction($"Executed: {e.Command.Description}");
};

undoRedoStack.CommandUndone += (sender, e) =>
{
    UpdateUI();
    LogAction($"Undone: {e.Command.Description}");
};

undoRedoStack.SaveBoundaryCreated += (sender, e) =>
{
    UpdateSaveIndicator(saved: true);
};
```

### Serialization and Persistence

```csharp
// Configure JSON serializer for persistence
var serializer = new JsonUndoRedoSerializer();
undoRedoStack.SetSerializer(serializer);

// Save stack state to byte array
byte[] data = await undoRedoStack.SaveStateAsync();
await File.WriteAllBytesAsync("undo_stack.json", data);

// Load stack state from byte array
byte[] loadedData = await File.ReadAllBytesAsync("undo_stack.json");
bool success = await undoRedoStack.LoadStateAsync(loadedData);

// For commands that need custom serialization, implement ISerializableCommand
public class MyCommand : BaseCommand, ISerializableCommand
{
    public string SerializeData() => JsonSerializer.Serialize(myData);
    public void DeserializeData(string data) => myData = JsonSerializer.Deserialize<MyData>(data);
}
```

## Advanced Configuration

```csharp
// Configure stack behavior
var undoRedoStack = new UndoRedoStack(
    maxStackSize: 500,        // Limit to 500 commands
    autoMergeCommands: true   // Automatically merge compatible commands
);

// Set up navigation with custom behavior
undoRedoStack.SetNavigationProvider(navigationProvider);

// Use async operations for better responsiveness
await undoRedoStack.UndoAsync(navigateToChange: true);
await undoRedoStack.RedoAsync(navigateToChange: true);
```

## Integration Examples

### Text Editor Integration

```csharp
public class TextEditorUndoRedo
{
    private readonly UndoRedoStack _undoRedo = new();
    private readonly ITextEditor _editor;

    public void OnTextChanged(TextChangeEventArgs e)
    {
        var command = new TextEditCommand(_editor, e.Position, e.OldText, e.NewText);
        _undoRedo.Execute(command);
    }

    public void OnSave()
    {
        _undoRedo.MarkAsSaved($"Saved {DateTime.Now:HH:mm:ss}");
    }
}
```

### WPF Integration

```csharp
public class DocumentViewModel : INotifyPropertyChanged
{
    private readonly UndoRedoStack _undoRedo = new();

    public ICommand UndoCommand => new RelayCommand(
        execute: () => _undoRedo.Undo(),
        canExecute: () => _undoRedo.CanUndo
    );

    public ICommand RedoCommand => new RelayCommand(
        execute: () => _undoRedo.Redo(),
        canExecute: () => _undoRedo.CanRedo
    );

    public bool HasUnsavedChanges => _undoRedo.HasUnsavedChanges;
}
```

## API Reference

### Core Classes

-   **`UndoRedoStack`**: Main class managing the undo/redo operations
-   **`ICommand`**: Interface for implementing undoable commands
-   **`BaseCommand`**: Base class with common command functionality
-   **`DelegateCommand`**: Simple command using delegates
-   **`CompositeCommand`**: Command containing multiple sub-commands
-   **`SaveBoundary`**: Represents a save point in the stack

### Key Interfaces

-   **`INavigationProvider`**: Interface for implementing navigation to changes
-   **`ChangeMetadata`**: Rich metadata about changes for visualization
-   **`ChangeVisualization`**: Data structure for displaying change history

## License

MIT License. Copyright (c) ktsu.dev
