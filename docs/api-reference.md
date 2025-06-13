---
title: "API Reference"
description: "Complete API reference for ktsu.UndoRedo library"
status: draft
---

# API Reference

Complete reference for all public APIs in the ktsu.UndoRedo library.

## Core Interfaces

### IUndoRedoService

Main service interface for undo/redo operations.

```csharp
public interface IUndoRedoService
{
    // Properties
    bool CanUndo { get; }
    bool CanRedo { get; }
    int CurrentPosition { get; }
    int CommandCount { get; }
    bool HasUnsavedChanges { get; }
    IReadOnlyList<SaveBoundary> SaveBoundaries { get; }
    IReadOnlyList<ICommand> Commands { get; }
    
    // Events
    event EventHandler<CommandExecutedEventArgs>? CommandExecuted;
    event EventHandler<CommandUndoneEventArgs>? CommandUndone;
    event EventHandler<CommandRedoneEventArgs>? CommandRedone;
    event EventHandler<SaveBoundaryCreatedEventArgs>? SaveBoundaryCreated;
    
    // Methods
    void Execute(ICommand command);
    Task<bool> UndoAsync(bool navigateToChange = true, CancellationToken cancellationToken = default);
    bool Undo();
    Task<bool> RedoAsync(bool navigateToChange = true, CancellationToken cancellationToken = default);
    bool Redo();
    void MarkAsSaved(string? description = null);
    void Clear();
    IEnumerable<ICommand> GetCommandsToUndo(SaveBoundary saveBoundary);
    Task<bool> UndoToSaveBoundaryAsync(SaveBoundary saveBoundary, bool navigateToLastChange = true, CancellationToken cancellationToken = default);
    IEnumerable<ChangeVisualization> GetChangeVisualizations(int maxItems = 50);
    
    // Serialization Methods
    void SetSerializer(IUndoRedoSerializer? serializer);
    Task<byte[]> SaveStateAsync(CancellationToken cancellationToken = default);
    Task<bool> LoadStateAsync(byte[] data, CancellationToken cancellationToken = default);
    UndoRedoStackState GetCurrentState();
    bool RestoreFromState(UndoRedoStackState state);
}
```

#### Properties

##### CanUndo
```csharp
bool CanUndo { get; }
```
Gets whether there are commands available to undo.

**Returns:** `true` if undo is possible, `false` otherwise.

**Example:**
```csharp
if (undoRedoStack.CanUndo)
{
    await undoRedoStack.UndoAsync();
}
```

##### CanRedo
```csharp
bool CanRedo { get; }
```
Gets whether there are commands available to redo.

**Returns:** `true` if redo is possible, `false` otherwise.

##### CurrentPosition
```csharp
int CurrentPosition { get; }
```
Gets the current position in the stack (0-based index).

**Returns:** The current position, where 0 means no commands have been executed.

##### CommandCount
```csharp
int CommandCount { get; }
```
Gets the total number of commands in the stack.

**Returns:** The total command count.

##### HasUnsavedChanges
```csharp
bool HasUnsavedChanges { get; }
```
Gets whether there are unsaved changes since the last save boundary.

**Returns:** `true` if there are unsaved changes, `false` otherwise.

**Example:**
```csharp
if (undoRedoStack.HasUnsavedChanges)
{
    var result = MessageBox.Show("Save changes?", "Unsaved Changes", MessageBoxButtons.YesNo);
    if (result == DialogResult.Yes)
    {
        SaveDocument();
        undoRedoStack.MarkAsSaved();
    }
}
```

##### SaveBoundaries
```csharp
IReadOnlyList<SaveBoundary> SaveBoundaries { get; }
```
Gets all save boundaries in the stack.

**Returns:** Read-only list of save boundaries.

##### Commands
```csharp
IReadOnlyList<ICommand> Commands { get; }
```
Gets all commands in the stack.

**Returns:** Read-only list of commands.

#### Events

##### CommandExecuted
```csharp
event EventHandler<CommandExecutedEventArgs>? CommandExecuted;
```
Fired when a command is executed.

**Event Args:**
- `Command`: The command that was executed
- `Position`: The position in the stack

**Example:**
```csharp
undoRedoStack.CommandExecuted += (sender, e) =>
{
    Console.WriteLine($"Executed: {e.Command.Description}");
    UpdateUndoRedoButtons();
};
```

##### CommandUndone
```csharp
event EventHandler<CommandUndoneEventArgs>? CommandUndone;
```
Fired when a command is undone.

##### CommandRedone
```csharp
event EventHandler<CommandRedoneEventArgs>? CommandRedone;
```
Fired when a command is redone.

##### SaveBoundaryCreated
```csharp
event EventHandler<SaveBoundaryCreatedEventArgs>? SaveBoundaryCreated;
```
Fired when a save boundary is created.

#### Methods

##### Execute
```csharp
void Execute(ICommand command);
```
Executes a command and adds it to the stack.

**Parameters:**
- `command`: The command to execute

**Throws:**
- `ArgumentNullException`: When command is null
- `InvalidOperationException`: When command execution fails

**Example:**
```csharp
var command = new DelegateCommand(
    "Set value",
    () => obj.Value = newValue,
    () => obj.Value = oldValue);
    
undoRedoStack.Execute(command);
```

##### UndoAsync
```csharp
Task<bool> UndoAsync(bool navigateToChange = true, CancellationToken cancellationToken = default);
```
Undoes the last command asynchronously.

**Parameters:**
- `navigateToChange`: Whether to navigate to where the change was made
- `cancellationToken`: Cancellation token for navigation

**Returns:** `true` if undo was successful, `false` otherwise.

**Example:**
```csharp
bool success = await undoRedoStack.UndoAsync(navigateToChange: true);
if (!success)
{
    MessageBox.Show("Undo failed");
}
```

##### Undo
```csharp
bool Undo();
```
Undoes the last command synchronously.

**Returns:** `true` if undo was successful, `false` otherwise.

##### RedoAsync
```csharp
Task<bool> RedoAsync(bool navigateToChange = true, CancellationToken cancellationToken = default);
```
Redoes the next command asynchronously.

**Parameters:**
- `navigateToChange`: Whether to navigate to where the change was made
- `cancellationToken`: Cancellation token for navigation

**Returns:** `true` if redo was successful, `false` otherwise.

##### Redo
```csharp
bool Redo();
```
Redoes the next command synchronously.

**Returns:** `true` if redo was successful, `false` otherwise.

##### MarkAsSaved
```csharp
void MarkAsSaved(string? description = null);
```
Creates a save boundary at the current position.

**Parameters:**
- `description`: Optional description of what was saved

**Example:**
```csharp
// After saving document
SaveDocumentToFile();
undoRedoStack.MarkAsSaved($"Saved to {fileName}");
```

##### Clear
```csharp
void Clear();
```
Clears the entire stack and all save boundaries.

**Example:**
```csharp
// When opening a new document
undoRedoStack.Clear();
LoadNewDocument();
```

##### GetCommandsToUndo
```csharp
IEnumerable<ICommand> GetCommandsToUndo(SaveBoundary saveBoundary);
```
Gets commands that would be undone to reach the specified save boundary.

**Parameters:**
- `saveBoundary`: The target save boundary

**Returns:** Commands that would be undone

**Throws:**
- `ArgumentException`: When save boundary is not in the stack

##### UndoToSaveBoundaryAsync
```csharp
Task<bool> UndoToSaveBoundaryAsync(SaveBoundary saveBoundary, bool navigateToLastChange = true, CancellationToken cancellationToken = default);
```
Undoes commands until reaching the specified save boundary.

**Parameters:**
- `saveBoundary`: The target save boundary
- `navigateToLastChange`: Whether to navigate to the last change
- `cancellationToken`: Cancellation token

**Returns:** `true` if successful, `false` otherwise.

**Example:**
```csharp
var lastSave = undoRedoStack.SaveBoundaries.LastOrDefault();
if (lastSave != null)
{
    await undoRedoStack.UndoToSaveBoundaryAsync(lastSave);
}
```

##### GetChangeVisualizations
```csharp
IEnumerable<ChangeVisualization> GetChangeVisualizations(int maxItems = 50);
```
Gets change visualization data for the commands in the stack.

**Parameters:**
- `maxItems`: Maximum number of items to return (default: 50)

**Returns:** Visualization data for changes

**Example:**
```csharp
var visualizations = undoRedoStack.GetChangeVisualizations(20);
foreach (var viz in visualizations)
{
    var status = viz.IsExecuted ? "✓" : "○";
    var saveIndicator = viz.HasSaveBoundary ? " 💾" : "";
    Console.WriteLine($"{status} {viz.Command.Description}{saveIndicator}");
}
```

##### SetSerializer
```csharp
void SetSerializer(IUndoRedoSerializer? serializer);
```
Sets the serializer to use for persistent stack state.

**Parameters:**
- `serializer`: The serializer to use, or null to disable serialization

**Example:**
```csharp
var serializer = new JsonUndoRedoSerializer();
undoRedoStack.SetSerializer(serializer);
```

##### SaveStateAsync
```csharp
Task<byte[]> SaveStateAsync(CancellationToken cancellationToken = default);
```
Saves the current stack state using the configured serializer.

**Parameters:**
- `cancellationToken`: Cancellation token

**Returns:** Serialized stack state as byte array

**Throws:**
- `InvalidOperationException`: When no serializer is configured

**Example:**
```csharp
try
{
    var data = await undoRedoStack.SaveStateAsync();
    await File.WriteAllBytesAsync("undo_stack.dat", data);
}
catch (InvalidOperationException)
{
    Console.WriteLine("No serializer configured");
}
```

##### LoadStateAsync
```csharp
Task<bool> LoadStateAsync(byte[] data, CancellationToken cancellationToken = default);
```
Loads stack state from serialized data using the configured serializer.

**Parameters:**
- `data`: The serialized stack state
- `cancellationToken`: Cancellation token

**Returns:** `true` if state was loaded successfully, `false` otherwise

**Throws:**
- `InvalidOperationException`: When no serializer is configured

##### GetCurrentState
```csharp
UndoRedoStackState GetCurrentState();
```
Gets the current stack state for serialization.

**Returns:** The current stack state

##### RestoreFromState
```csharp
bool RestoreFromState(UndoRedoStackState state);
```
Restores the stack from a given state.

**Parameters:**
- `state`: The state to restore

**Returns:** `true` if restoration was successful, `false` otherwise

### ICommand

Interface for all commands in the undo/redo system.

```csharp
public interface ICommand
{
    string Description { get; }
    ChangeMetadata Metadata { get; }
    
    void Execute();
    void Undo();
    bool CanMergeWith(ICommand other);
    ICommand MergeWith(ICommand other);
}
```

#### Properties

##### Description
```csharp
string Description { get; }
```
Gets a human-readable description of what this command does.

##### Metadata
```csharp
ChangeMetadata Metadata { get; }
```
Gets metadata about this command including change type, affected items, and navigation context.

#### Methods

##### Execute
```csharp
void Execute();
```
Executes the command's action.

##### Undo
```csharp
void Undo();
```
Undoes the command's action.

##### CanMergeWith
```csharp
bool CanMergeWith(ICommand other);
```
Determines if this command can be merged with another command.

**Parameters:**
- `other`: The other command to check

**Returns:** `true` if commands can be merged, `false` otherwise

##### MergeWith
```csharp
ICommand MergeWith(ICommand other);
```
Merges this command with another command.

**Parameters:**
- `other`: The other command to merge with

**Returns:** A new command representing the merged operation

**Throws:**
- `NotSupportedException`: When commands cannot be merged

### INavigationProvider

Interface for providing navigation functionality during undo/redo operations.

```csharp
public interface INavigationProvider
{
    Task<bool> NavigateToAsync(string context, CancellationToken cancellationToken = default);
    bool IsValidContext(string context);
}
```

#### Methods

##### NavigateToAsync
```csharp
Task<bool> NavigateToAsync(string context, CancellationToken cancellationToken = default);
```
Navigates to the specified context.

**Parameters:**
- `context`: The navigation context (e.g., "file:line:column")
- `cancellationToken`: Cancellation token

**Returns:** `true` if navigation was successful, `false` otherwise

##### IsValidContext
```csharp
bool IsValidContext(string context);
```
Checks if the given context is valid for navigation.

**Parameters:**
- `context`: The context to validate

**Returns:** `true` if context is valid, `false` otherwise

## Command Implementations

### DelegateCommand

Simple command implementation using delegates.

```csharp
public class DelegateCommand : BaseCommand
{
    public DelegateCommand(
        string description,
        Action executeAction,
        Action undoAction,
        ChangeType changeType = ChangeType.Modify,
        IEnumerable<string>? affectedItems = null,
        string? navigationContext = null);
}
```

**Example:**
```csharp
var command = new DelegateCommand(
    description: "Change color to red",
    executeAction: () => shape.Color = Color.Red,
    undoAction: () => shape.Color = originalColor,
    changeType: ChangeType.Modify,
    affectedItems: new[] { $"shape-{shape.Id}" },
    navigationContext: $"canvas:{shape.Position.X}:{shape.Position.Y}"
);
```

### CompositeCommand

Command that groups multiple commands into a single operation.

```csharp
public class CompositeCommand : BaseCommand
{
    public CompositeCommand(
        string description,
        IEnumerable<ICommand> commands,
        string? navigationContext = null);
}
```

**Example:**
```csharp
var moveCommand = new DelegateCommand("Move", () => obj.Move(dx, dy), () => obj.Move(-dx, -dy));
var resizeCommand = new DelegateCommand("Resize", () => obj.Resize(dw, dh), () => obj.Resize(-dw, -dh));

var composite = new CompositeCommand("Move and Resize", new[] { moveCommand, resizeCommand });
undoRedoStack.Execute(composite);
```

### BaseCommand

Abstract base class for implementing custom commands.

```csharp
public abstract class BaseCommand : ICommand
{
    protected BaseCommand(
        ChangeType changeType,
        IEnumerable<string>? affectedItems = null,
        string? navigationContext = null);
        
    public abstract string Description { get; }
    public ChangeMetadata Metadata { get; }
    
    public abstract void Execute();
    public abstract void Undo();
    
    public virtual bool CanMergeWith(ICommand other) => false;
    public virtual ICommand MergeWith(ICommand other) => throw new NotSupportedException();
}
```

**Example:**
```csharp
public class TextInsertCommand : BaseCommand
{
    private readonly ITextBuffer _buffer;
    private readonly int _position;
    private readonly string _text;
    
    public TextInsertCommand(ITextBuffer buffer, int position, string text)
        : base(ChangeType.Insert, new[] { $"text:{position}" }, $"editor:{GetLineColumn(position)}")
    {
        _buffer = buffer;
        _position = position;
        _text = text;
    }
    
    public override string Description => $"Insert '{_text}' at position {_position}";
    
    public override void Execute()
    {
        _buffer.Insert(_position, _text);
    }
    
    public override void Undo()
    {
        _buffer.Delete(_position, _text.Length);
    }
    
    public override bool CanMergeWith(ICommand other)
    {
        return other is TextInsertCommand insertCmd && 
               insertCmd._position == _position + _text.Length;
    }
    
    public override ICommand MergeWith(ICommand other)
    {
        var insertCmd = (TextInsertCommand)other;
        return new TextInsertCommand(_buffer, _position, _text + insertCmd._text);
    }
}
```

## Models

### UndoRedoOptions

Configuration options for the undo/redo system.

```csharp
public record UndoRedoOptions(
    int MaxStackSize = 1000,
    bool AutoMergeCommands = true,
    bool EnableNavigation = true,
    TimeSpan DefaultNavigationTimeout = default);
```

**Properties:**
- `MaxStackSize`: Maximum number of commands to keep (0 for unlimited)
- `AutoMergeCommands`: Whether to automatically merge compatible commands
- `EnableNavigation`: Whether navigation is enabled by default
- `DefaultNavigationTimeout`: Default timeout for navigation operations

**Example:**
```csharp
var options = UndoRedoOptions.Create(
    maxStackSize: 500,
    autoMerge: false,
    enableNavigation: true,
    navigationTimeout: TimeSpan.FromSeconds(10)
);

var undoRedoStack = new UndoRedoStack(options);
```

### ChangeMetadata

Metadata describing a command's impact.

```csharp
public record ChangeMetadata(
    ChangeType ChangeType,
    IReadOnlyList<string> AffectedItems,
    string? NavigationContext = null,
    DateTime Timestamp = default);
```

**Properties:**
- `ChangeType`: The type of change (Insert, Delete, Modify, Mixed)
- `AffectedItems`: Items affected by this command
- `NavigationContext`: Context for navigation (optional)
- `Timestamp`: When the command was created

### SaveBoundary

Represents a save point in the command history.

```csharp
public record SaveBoundary(
    int Position,
    DateTime Timestamp,
    string? Description = null);
```

**Properties:**
- `Position`: Position in the command stack
- `Timestamp`: When the save boundary was created
- `Description`: Optional description

### ChangeVisualization

Data for visualizing changes in UI.

```csharp
public record ChangeVisualization(
    ICommand Command,
    bool IsExecuted,
    bool HasSaveBoundary);
```

**Properties:**
- `Command`: The command being visualized
- `IsExecuted`: Whether the command is currently executed
- `HasSaveBoundary`: Whether there's a save boundary at this position

## Event Args

### CommandExecutedEventArgs
```csharp
public class CommandExecutedEventArgs : EventArgs
{
    public ICommand Command { get; }
    public int Position { get; }
}
```

### CommandUndoneEventArgs
```csharp
public class CommandUndoneEventArgs : EventArgs
{
    public ICommand Command { get; }
    public int Position { get; }
}
```

### CommandRedoneEventArgs
```csharp
public class CommandRedoneEventArgs : EventArgs
{
    public ICommand Command { get; }
    public int Position { get; }
}
```

### SaveBoundaryCreatedEventArgs
```csharp
public class SaveBoundaryCreatedEventArgs : EventArgs
{
    public SaveBoundary SaveBoundary { get; }
}
```

## Enums

### ChangeType

Describes the type of change a command makes.

```csharp
public enum ChangeType
{
    Insert,   // Adding new content
    Delete,   // Removing content
    Modify,   // Changing existing content
    Mixed     // Multiple types (used for composite commands)
}
```

## Extension Methods

### ServiceCollectionExtensions

Extensions for dependency injection setup.

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUndoRedo(this IServiceCollection services);
    public static IServiceCollection AddUndoRedo(this IServiceCollection services, UndoRedoOptions options);
    public static IServiceCollection AddUndoRedo(this IServiceCollection services, Action<UndoRedoOptions> configure);
}
```

**Example:**
```csharp
services.AddUndoRedo(options =>
{
    options.MaxStackSize = 1000;
    options.AutoMergeCommands = true;
});
``` 
