# Getting Started

This guide will help you get up and running with the UndoRedo library quickly.

## Installation

Add the UndoRedo library to your project:

```xml
<PackageReference Include="ktsu.UndoRedo" Version="1.0.2" />
```

Or via Package Manager Console:

```powershell
Install-Package ktsu.UndoRedo
```

## Basic Setup

### 1. Register Services (Dependency Injection)

If you're using dependency injection (recommended):

```csharp
using ktsu.UndoRedo;
using Microsoft.Extensions.DependencyInjection;

// In your startup/configuration code
var services = new ServiceCollection();

// Add undo/redo services with default configuration
services.AddUndoRedo();

// Or with custom configuration
services.AddUndoRedo(options => options
    .WithMaxStackSize(500)
    .WithAutoMerge(true)
    .WithNavigation(true)
    .WithNavigationTimeout(TimeSpan.FromSeconds(3)));

var serviceProvider = services.BuildServiceProvider();
```

### 2. Manual Setup (Without DI)

If you prefer not to use dependency injection:

```csharp
using ktsu.UndoRedo.Services;
using ktsu.UndoRedo.Models;

// Create services manually
var stackManager = new StackManager();
var saveBoundaryManager = new SaveBoundaryManager();
var commandMerger = new CommandMerger();
var options = UndoRedoOptions.Default;

var undoRedoService = new UndoRedoService(
    stackManager,
    saveBoundaryManager,
    commandMerger,
    options);
```

## Basic Usage

### 1. Get the Service

```csharp
// From DI container
var undoRedoService = serviceProvider.GetRequiredService<IUndoRedoService>();

// Or use your manually created instance
// var undoRedoService = ... (from manual setup above)
```

### 2. Create and Execute Commands

#### Using DelegateCommand (Simple)

```csharp
using ktsu.UndoRedo;

// Simple text editing example
var originalText = textBox.Text;
var newText = "Hello, World!";

var command = new DelegateCommand(
    description: "Change text",
    executeAction: () => textBox.Text = newText,
    undoAction: () => textBox.Text = originalText,
    changeType: ChangeType.Modify,
    affectedItems: new[] { "TextBox1" });

// Execute the command (this also adds it to the undo stack)
undoRedoService.Execute(command);
```

#### Using Custom Commands

```csharp
using ktsu.UndoRedo;
using ktsu.UndoRedo.Models;

public class TextEditCommand : BaseCommand
{
    private readonly TextBox _textBox;
    private readonly string _oldText;
    private readonly string _newText;

    public override string Description => "Edit text";

    public TextEditCommand(TextBox textBox, string oldText, string newText)
        : base(ChangeType.Modify, new[] { textBox.Name })
    {
        _textBox = textBox;
        _oldText = oldText;
        _newText = newText;
    }

    public override void Execute()
    {
        _textBox.Text = _newText;
    }

    public override void Undo()
    {
        _textBox.Text = _oldText;
    }
}

// Usage
var command = new TextEditCommand(textBox, textBox.Text, "New text");
undoRedoService.Execute(command);
```

### 3. Undo and Redo Operations

```csharp
// Check if operations are available
if (undoRedoService.CanUndo)
{
    // Undo the last command
    bool success = undoRedoService.Undo();
    // or with navigation
    bool success = await undoRedoService.UndoAsync(navigateToChange: true);
}

if (undoRedoService.CanRedo)
{
    // Redo the next command
    bool success = undoRedoService.Redo();
    // or with navigation
    bool success = await undoRedoService.RedoAsync(navigateToChange: true);
}
```

### 4. Save Boundaries

Track save states to know when there are unsaved changes:

```csharp
// Mark current state as saved
undoRedoService.MarkAsSaved("Document saved");

// Check if there are unsaved changes
bool hasUnsavedChanges = undoRedoService.HasUnsavedChanges;

// Get all save boundaries
var saveBoundaries = undoRedoService.SaveBoundaries;

// Undo to a specific save boundary
var lastSave = saveBoundaries.LastOrDefault();
if (lastSave != null)
{
    await undoRedoService.UndoToSaveBoundaryAsync(lastSave);
}
```

## Complete Example: Simple Text Editor

Here's a complete example showing how to integrate the undo/redo system into a simple text editor:

```csharp
using ktsu.UndoRedo;
using ktsu.UndoRedo.Contracts;
using ktsu.UndoRedo.Models;
using ktsu.UndoRedo.Services;
using Microsoft.Extensions.DependencyInjection;

public partial class SimpleTextEditor : Form
{
    private readonly IUndoRedoService _undoRedoService;
    private TextBox _textBox;
    private Button _undoButton;
    private Button _redoButton;
    private Button _saveButton;
    private Label _statusLabel;

    public SimpleTextEditor()
    {
        InitializeComponent();

        // Setup undo/redo service
        var services = new ServiceCollection();
        services.AddUndoRedo();
        var serviceProvider = services.BuildServiceProvider();
        _undoRedoService = serviceProvider.GetRequiredService<IUndoRedoService>();

        // Subscribe to events
        _undoRedoService.CommandExecuted += OnCommandExecuted;
        _undoRedoService.CommandUndone += OnCommandUndone;
        _undoRedoService.CommandRedone += OnCommandRedone;
        _undoRedoService.SaveBoundaryCreated += OnSaveBoundaryCreated;

        UpdateUI();
    }

    private void OnTextChanged(object sender, EventArgs e)
    {
        // Create command for text change
        var command = new DelegateCommand(
            description: "Text changed",
            executeAction: () => { /* Already executed by user input */ },
            undoAction: () => _textBox.Text = _previousText,
            changeType: ChangeType.Modify,
            affectedItems: new[] { "Document" });

        _undoRedoService.Execute(command);
        UpdateUI();
    }

    private void OnUndoClick(object sender, EventArgs e)
    {
        if (_undoRedoService.CanUndo)
        {
            _undoRedoService.Undo();
            UpdateUI();
        }
    }

    private void OnRedoClick(object sender, EventArgs e)
    {
        if (_undoRedoService.CanRedo)
        {
            _undoRedoService.Redo();
            UpdateUI();
        }
    }

    private void OnSaveClick(object sender, EventArgs e)
    {
        // Simulate saving
        SaveDocument();
        _undoRedoService.MarkAsSaved($"Saved at {DateTime.Now:HH:mm:ss}");
        UpdateUI();
    }

    private void UpdateUI()
    {
        _undoButton.Enabled = _undoRedoService.CanUndo;
        _redoButton.Enabled = _undoRedoService.CanRedo;

        var status = _undoRedoService.HasUnsavedChanges ? "Modified" : "Saved";
        _statusLabel.Text = $"Status: {status} | Commands: {_undoRedoService.CommandCount}";

        // Update window title
        this.Text = $"Simple Text Editor {(_undoRedoService.HasUnsavedChanges ? "*" : "")}";
    }

    private void OnCommandExecuted(object sender, CommandExecutedEventArgs e)
    {
        Console.WriteLine($"Executed: {e.Command.Description}");
    }

    private void OnCommandUndone(object sender, CommandUndoneEventArgs e)
    {
        Console.WriteLine($"Undone: {e.Command.Description}");
    }

    private void OnCommandRedone(object sender, CommandRedoneEventArgs e)
    {
        Console.WriteLine($"Redone: {e.Command.Description}");
    }

    private void OnSaveBoundaryCreated(object sender, SaveBoundaryCreatedEventArgs e)
    {
        Console.WriteLine($"Save boundary: {e.SaveBoundary.Description}");
    }
}
```

## Event Handling

The service provides events for all major operations:

```csharp
// Subscribe to events
undoRedoService.CommandExecuted += (sender, e) =>
{
    Console.WriteLine($"Executed: {e.Command.Description} at position {e.Position}");
};

undoRedoService.CommandUndone += (sender, e) =>
{
    Console.WriteLine($"Undone: {e.Command.Description} at position {e.Position}");
};

undoRedoService.CommandRedone += (sender, e) =>
{
    Console.WriteLine($"Redone: {e.Command.Description} at position {e.Position}");
};

undoRedoService.SaveBoundaryCreated += (sender, e) =>
{
    Console.WriteLine($"Save boundary created: {e.SaveBoundary.Description}");
};
```

## Configuration Options

You can customize the behavior with various options:

```csharp
services.AddUndoRedo(options => options
    .WithMaxStackSize(1000)              // Maximum commands in stack (0 = unlimited)
    .WithAutoMerge(true)                 // Automatically merge compatible commands
    .WithNavigation(true)                // Enable navigation features
    .WithNavigationTimeout(TimeSpan.FromSeconds(5))); // Navigation timeout
```

Or using the options object directly:

```csharp
var options = new UndoRedoOptions(
    MaxStackSize: 1000,
    AutoMergeCommands: true,
    EnableNavigation: true,
    DefaultNavigationTimeout: TimeSpan.FromSeconds(5));

services.AddUndoRedo(options);
```

## Next Steps

-   Learn about complex scenarios in [Tutorial: Advanced Scenarios](tutorial-advanced-scenarios.md)
-   Understand navigation integration patterns in [Tutorial: Advanced Scenarios](tutorial-advanced-scenarios.md)
-   Review [Best Practices](best-practices.md) for optimal usage
-   Explore [API Reference](api-reference.md) for detailed API documentation and custom command development

## Common Patterns

### Command Factory

Create commands consistently using a factory:

```csharp
public static class CommandFactory
{
    public static ICommand CreateTextEdit(TextBox textBox, string newText)
    {
        var oldText = textBox.Text;
        return new DelegateCommand(
            "Edit text",
            () => textBox.Text = newText,
            () => textBox.Text = oldText,
            ChangeType.Modify,
            new[] { textBox.Name });
    }

    public static ICommand CreateItemDelete(ListView listView, ListViewItem item)
    {
        var index = item.Index;
        return new DelegateCommand(
            $"Delete {item.Text}",
            () => listView.Items.Remove(item),
            () => listView.Items.Insert(index, item),
            ChangeType.Delete,
            new[] { item.Text });
    }
}
```

### Keyboard Shortcuts

Wire up standard keyboard shortcuts:

```csharp
protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
{
    switch (keyData)
    {
        case Keys.Control | Keys.Z:
            if (_undoRedoService.CanUndo)
                _undoRedoService.Undo();
            return true;

        case Keys.Control | Keys.Y:
        case Keys.Control | Keys.Shift | Keys.Z:
            if (_undoRedoService.CanRedo)
                _undoRedoService.Redo();
            return true;
    }

    return base.ProcessCmdKey(ref msg, keyData);
}
```
