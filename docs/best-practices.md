# Best Practices

This document outlines recommended patterns, common pitfalls to avoid, and best practices for using the UndoRedo library effectively.

## Command Design Best Practices

### 1. Keep Commands Focused and Atomic

Commands should represent a single, atomic operation that can be cleanly reversed.

#### ✅ Good: Atomic Command

```csharp
public class SetTextCommand : BaseCommand
{
    private readonly ITextControl _control;
    private readonly string _oldText;
    private readonly string _newText;

    public override string Description => "Change text";

    public SetTextCommand(ITextControl control, string newText)
        : base(ChangeType.Modify, new[] { control.Name })
    {
        _control = control;
        _oldText = control.Text;
        _newText = newText;
    }

    public override void Execute() => _control.Text = _newText;
    public override void Undo() => _control.Text = _oldText;
}
```

#### ❌ Bad: Multiple Unrelated Operations

```csharp
// Don't combine unrelated operations in a single command
public class BadCommand : BaseCommand
{
    public override void Execute()
    {
        // Multiple unrelated operations
        UpdateText();
        ChangeColor();
        SaveToDatabase();
        SendNotification();
    }
}
```

**Solution**: Use `CompositeCommand` for related operations:

```csharp
var commands = new List<ICommand>
{
    new SetTextCommand(control, newText),
    new SetColorCommand(control, newColor)
};
var compositeCommand = new CompositeCommand("Update control", commands);
```

### 2. Capture State Before Execution

Always capture the "old" state in the constructor, before executing the command.

#### ✅ Good: Capture in Constructor

```csharp
public class MoveItemCommand : BaseCommand
{
    private readonly IList<Item> _list;
    private readonly int _oldIndex;
    private readonly int _newIndex;
    private readonly Item _item;

    public MoveItemCommand(IList<Item> list, int oldIndex, int newIndex)
        : base(ChangeType.Move, new[] { $"Item_{oldIndex}" })
    {
        _list = list;
        _oldIndex = oldIndex;
        _newIndex = newIndex;
        _item = list[oldIndex]; // Capture before execution
    }

    public override void Execute()
    {
        _list.RemoveAt(_oldIndex);
        _list.Insert(_newIndex, _item);
    }

    public override void Undo()
    {
        _list.RemoveAt(_newIndex);
        _list.Insert(_oldIndex, _item);
    }
}
```

#### ❌ Bad: Capture in Execute

```csharp
public class BadMoveCommand : BaseCommand
{
    private Item _item; // Not captured yet

    public override void Execute()
    {
        _item = _list[_oldIndex]; // Too late - state might have changed
        _list.RemoveAt(_oldIndex);
        _list.Insert(_newIndex, _item);
    }
}
```

### 3. Handle Edge Cases and Validation

Validate parameters and handle edge cases gracefully.

```csharp
public class DeleteItemCommand : BaseCommand
{
    private readonly IList<Item> _list;
    private readonly int _index;
    private readonly Item _item;

    public DeleteItemCommand(IList<Item> list, int index)
        : base(ChangeType.Delete, new[] { $"Item_{index}" })
    {
        ArgumentNullException.ThrowIfNull(list);

        if (index < 0 || index >= list.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _list = list;
        _index = index;
        _item = list[index];
    }

    public override void Execute()
    {
        if (_index < _list.Count && _list[_index] == _item)
            _list.RemoveAt(_index);
    }

    public override void Undo()
    {
        if (_index <= _list.Count)
            _list.Insert(_index, _item);
    }
}
```

### 4. Implement Command Merging Thoughtfully

Only merge commands when it makes semantic sense and improves user experience.

#### ✅ Good: Text Editing Merging

```csharp
public class TypeTextCommand : BaseCommand
{
    private readonly ITextControl _control;
    private readonly int _position;
    private readonly string _text;
    private readonly DateTime _timestamp;

    public override bool CanMergeWith(ICommand other)
    {
        return other is TypeTextCommand otherType &&
               otherType._control == _control &&
               otherType._position == _position + _text.Length &&
               (otherType._timestamp - _timestamp).TotalSeconds < 2.0; // Merge within 2 seconds
    }

    public override ICommand MergeWith(ICommand other)
    {
        var otherType = (TypeTextCommand)other;
        return new TypeTextCommand(_control, _position, _text + otherType._text);
    }
}
```

#### ❌ Bad: Overly Aggressive Merging

```csharp
// Don't merge semantically different operations
public override bool CanMergeWith(ICommand other)
{
    return other.GetType() == GetType(); // Too broad!
}
```

## Service Usage Best Practices

### 1. Use Dependency Injection

Always use dependency injection rather than creating services manually.

#### ✅ Good: Constructor Injection

```csharp
public class DocumentEditor
{
    private readonly IUndoRedoService _undoRedoService;

    public DocumentEditor(IUndoRedoService undoRedoService)
    {
        _undoRedoService = undoRedoService ?? throw new ArgumentNullException(nameof(undoRedoService));
    }
}
```

#### ❌ Bad: Manual Creation

```csharp
public class DocumentEditor
{
    private readonly IUndoRedoService _undoRedoService;

    public DocumentEditor()
    {
        // Don't create services manually
        _undoRedoService = new UndoRedoService(
            new StackManager(),
            new SaveBoundaryManager(),
            new CommandMerger());
    }
}
```

### 2. Handle Events Properly

Subscribe to events for UI updates and unsubscribe to prevent memory leaks.

#### ✅ Good: Proper Event Handling

```csharp
public class DocumentView : IDisposable
{
    private readonly IUndoRedoService _undoRedoService;

    public DocumentView(IUndoRedoService undoRedoService)
    {
        _undoRedoService = undoRedoService;

        // Subscribe to events
        _undoRedoService.CommandExecuted += OnCommandExecuted;
        _undoRedoService.CommandUndone += OnCommandUndone;
        _undoRedoService.CommandRedone += OnCommandRedone;
    }

    private void OnCommandExecuted(object? sender, CommandExecutedEventArgs e)
    {
        UpdateUI();
        ShowStatusMessage($"Executed: {e.Command.Description}");
    }

    public void Dispose()
    {
        // Unsubscribe to prevent memory leaks
        _undoRedoService.CommandExecuted -= OnCommandExecuted;
        _undoRedoService.CommandUndone -= OnCommandUndone;
        _undoRedoService.CommandRedone -= OnCommandRedone;
    }
}
```

### 3. Use Async Methods Appropriately

Use async methods when navigation is involved or when calling from async contexts.

```csharp
// In event handlers or UI code
private async void OnUndoButtonClick(object sender, EventArgs e)
{
    if (_undoRedoService.CanUndo)
    {
        // Use async version for navigation
        await _undoRedoService.UndoAsync(navigateToChange: true);
        UpdateUI();
    }
}

// In synchronous contexts where navigation isn't needed
private void UndoWithoutNavigation()
{
    if (_undoRedoService.CanUndo)
    {
        // Use sync version when navigation isn't needed
        _undoRedoService.Undo();
        UpdateUI();
    }
}
```

## Performance Best Practices

### 1. Configure Stack Size Appropriately

Set reasonable limits to prevent memory issues.

```csharp
// Configure based on your application's needs
services.AddUndoRedo(options => options
    .WithMaxStackSize(1000) // Adjust based on memory constraints
    .WithAutoMerge(true));   // Enable merging to reduce stack size
```

### 2. Optimize Command Memory Usage

Keep command memory footprint small, especially for frequently created commands.

#### ✅ Good: Minimal Memory Usage

```csharp
public class SetPropertyCommand : BaseCommand
{
    private readonly WeakReference _targetRef; // Use weak reference for large objects
    private readonly string _propertyName;
    private readonly object _oldValue;
    private readonly object _newValue;

    public SetPropertyCommand(object target, string propertyName, object oldValue, object newValue)
        : base(ChangeType.Modify, new[] { $"{target.GetType().Name}.{propertyName}" })
    {
        _targetRef = new WeakReference(target);
        _propertyName = propertyName;
        _oldValue = oldValue;
        _newValue = newValue;
    }
}
```

#### ❌ Bad: Holding Large Objects

```csharp
public class BadCommand : BaseCommand
{
    private readonly LargeObject _largeObject; // Don't hold unnecessary references
    private readonly byte[] _hugeData; // Avoid large data in commands
}
```

### 3. Batch Related Operations

Use composite commands for related operations to reduce stack size.

```csharp
public void ApplyMultipleChanges(IEnumerable<Change> changes)
{
    var commands = changes.Select(change => CreateCommandForChange(change)).ToList();

    if (commands.Count == 1)
    {
        _undoRedoService.Execute(commands[0]);
    }
    else if (commands.Count > 1)
    {
        var composite = new CompositeCommand("Apply multiple changes", commands);
        _undoRedoService.Execute(composite);
    }
}
```

## Error Handling Best Practices

### 1. Handle Command Execution Failures

Provide graceful error handling for command execution failures.

```csharp
public class SafeExecuteHelper
{
    private readonly IUndoRedoService _undoRedoService;
    private readonly ILogger _logger;

    public async Task<bool> TryExecuteAsync(ICommand command)
    {
        try
        {
            _undoRedoService.Execute(command);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute command: {Description}", command.Description);

            // Show user-friendly error message
            ShowErrorMessage($"Failed to {command.Description.ToLower()}: {ex.Message}");
            return false;
        }
    }
}
```

### 2. Validate Commands Before Execution

Implement validation to catch issues early.

```csharp
public interface ICommandValidator
{
    ValidationResult Validate(ICommand command);
}

public class DocumentCommandValidator : ICommandValidator
{
    public ValidationResult Validate(ICommand command)
    {
        if (command is IDocumentCommand docCommand)
        {
            if (docCommand.Document.IsReadOnly)
                return ValidationResult.Failure("Document is read-only");

            if (!docCommand.Document.IsLoaded)
                return ValidationResult.Failure("Document is not loaded");
        }

        return ValidationResult.Success();
    }
}
```

## UI Integration Best Practices

### 1. Update UI State Reactively

Keep UI in sync with undo/redo state.

```csharp
public class UndoRedoToolbar : UserControl
{
    private readonly IUndoRedoService _undoRedoService;
    private readonly Button _undoButton;
    private readonly Button _redoButton;
    private readonly Label _statusLabel;

    public UndoRedoToolbar(IUndoRedoService undoRedoService)
    {
        _undoRedoService = undoRedoService;
        InitializeComponent();

        // Subscribe to events for reactive UI updates
        _undoRedoService.CommandExecuted += (s, e) => UpdateButtons();
        _undoRedoService.CommandUndone += (s, e) => UpdateButtons();
        _undoRedoService.CommandRedone += (s, e) => UpdateButtons();

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        _undoButton.Enabled = _undoRedoService.CanUndo;
        _redoButton.Enabled = _undoRedoService.CanRedo;

        // Update tooltips with command descriptions
        _undoButton.ToolTipText = _undoRedoService.CanUndo
            ? $"Undo {GetCurrentCommand()?.Description}"
            : "Nothing to undo";

        _redoButton.ToolTipText = _undoRedoService.CanRedo
            ? $"Redo {GetNextCommand()?.Description}"
            : "Nothing to redo";

        _statusLabel.Text = $"Commands: {_undoRedoService.CommandCount}";
    }
}
```

### 2. Provide Visual Feedback

Give users clear feedback about undo/redo operations.

```csharp
public class UndoRedoVisualFeedback
{
    public async Task ShowUndoFeedback(ICommand command)
    {
        // Show temporary notification
        var notification = new ToastNotification($"Undone: {command.Description}");
        await notification.ShowAsync(TimeSpan.FromSeconds(2));
    }

    public void HighlightAffectedElements(ICommand command)
    {
        foreach (var item in command.Metadata.AffectedItems)
        {
            if (FindElementByName(item) is UIElement element)
            {
                HighlightElement(element);
            }
        }
    }
}
```

## Testing Best Practices

### 1. Test Command Behavior Thoroughly

Write comprehensive tests for command execute/undo behavior.

```csharp
[TestFixture]
public class SetTextCommandTests
{
    [Test]
    public void Execute_SetsTextCorrectly()
    {
        // Arrange
        var textBox = new TextBox { Text = "original" };
        var command = new SetTextCommand(textBox, "new text");

        // Act
        command.Execute();

        // Assert
        Assert.That(textBox.Text, Is.EqualTo("new text"));
    }

    [Test]
    public void Undo_RestoresOriginalText()
    {
        // Arrange
        var textBox = new TextBox { Text = "original" };
        var command = new SetTextCommand(textBox, "new text");
        command.Execute();

        // Act
        command.Undo();

        // Assert
        Assert.That(textBox.Text, Is.EqualTo("original"));
    }

    [Test]
    public void ExecuteUndo_IsIdempotent()
    {
        // Arrange
        var textBox = new TextBox { Text = "original" };
        var command = new SetTextCommand(textBox, "new text");

        // Act - multiple execute/undo cycles
        command.Execute();
        command.Undo();
        command.Execute();
        command.Undo();

        // Assert
        Assert.That(textBox.Text, Is.EqualTo("original"));
    }
}
```

### 2. Test Service Integration

Test the full workflow with real services.

```csharp
[TestFixture]
public class UndoRedoIntegrationTests
{
    private IUndoRedoService _service;
    private ServiceProvider _serviceProvider;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddUndoRedo();
        _serviceProvider = services.BuildServiceProvider();
        _service = _serviceProvider.GetRequiredService<IUndoRedoService>();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
    }

    [Test]
    public void CompleteWorkflow_WorksCorrectly()
    {
        // Arrange
        var textBox = new TextBox { Text = "start" };
        var command1 = new SetTextCommand(textBox, "middle");
        var command2 = new SetTextCommand(textBox, "end");

        // Act & Assert
        _service.Execute(command1);
        Assert.That(textBox.Text, Is.EqualTo("middle"));
        Assert.That(_service.CanUndo, Is.True);

        _service.Execute(command2);
        Assert.That(textBox.Text, Is.EqualTo("end"));

        _service.Undo();
        Assert.That(textBox.Text, Is.EqualTo("middle"));

        _service.Undo();
        Assert.That(textBox.Text, Is.EqualTo("start"));
        Assert.That(_service.CanUndo, Is.False);

        _service.Redo();
        Assert.That(textBox.Text, Is.EqualTo("middle"));
        Assert.That(_service.CanRedo, Is.True);
    }
}
```

## Common Anti-Patterns to Avoid

### 1. Don't Modify State in Constructors

```csharp
// ❌ Bad: Modifying state in constructor
public class BadCommand : BaseCommand
{
    public BadCommand(ITextControl control, string newText)
    {
        control.Text = newText; // Don't do this!
    }
}

// ✅ Good: Modify state in Execute
public class GoodCommand : BaseCommand
{
    private readonly ITextControl _control;
    private readonly string _newText;

    public GoodCommand(ITextControl control, string newText)
    {
        _control = control;
        _newText = newText;
        // Don't modify state here
    }

    public override void Execute()
    {
        _control.Text = _newText; // Modify state here
    }
}
```

### 2. Don't Create Commands for Every Property Change

```csharp
// ❌ Bad: Creating commands for trivial changes
private void OnMouseMove(MouseEventArgs e)
{
    var command = new SetMousePositionCommand(e.X, e.Y);
    _undoRedoService.Execute(command); // Too granular!
}

// ✅ Good: Create commands for meaningful user actions
private void OnShapeMove(Point oldPosition, Point newPosition)
{
    var command = new MoveShapeCommand(_selectedShape, oldPosition, newPosition);
    _undoRedoService.Execute(command);
}
```

### 3. Don't Ignore Command Execution Context

```csharp
// ❌ Bad: Executing commands during undo/redo
private void OnTextChanged(object sender, EventArgs e)
{
    // This will execute even during undo/redo!
    var command = new TextChangedCommand(...);
    _undoRedoService.Execute(command);
}

// ✅ Good: Track execution context
private bool _isExecutingCommand = false;

private void OnTextChanged(object sender, EventArgs e)
{
    if (_isExecutingCommand) return; // Skip during undo/redo

    var command = new TextChangedCommand(...);
    _undoRedoService.Execute(command);
}
```

## Configuration Best Practices

### 1. Environment-Specific Configuration

Adjust configuration based on environment and usage patterns.

```csharp
// Development: Smaller stack for faster testing
if (isDevelopment)
{
    services.AddUndoRedo(options => options
        .WithMaxStackSize(50)
        .WithAutoMerge(false)); // Disable for testing individual commands
}
// Production: Optimized for user experience
else
{
    services.AddUndoRedo(options => options
        .WithMaxStackSize(1000)
        .WithAutoMerge(true)
        .WithNavigation(true));
}
```

### 2. User Preference Integration

Allow users to configure behavior.

```csharp
public class UserPreferencesService
{
    public UndoRedoOptions GetUndoRedoOptions()
    {
        var userPrefs = LoadUserPreferences();

        return new UndoRedoOptions(
            MaxStackSize: userPrefs.UndoLevels,
            AutoMergeCommands: userPrefs.AutoMergeEnabled,
            EnableNavigation: userPrefs.NavigationEnabled,
            DefaultNavigationTimeout: TimeSpan.FromSeconds(userPrefs.NavigationTimeoutSeconds));
    }
}
```

By following these best practices, you'll create a robust, performant, and user-friendly undo/redo system that integrates well with your application architecture.
