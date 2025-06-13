---
title: "Advanced Scenarios Tutorial"
description: "Tutorial covering advanced undo/redo scenarios with real-world examples"
status: draft
---

# Advanced Scenarios Tutorial

This tutorial covers advanced usage patterns and real-world scenarios for the ktsu.UndoRedo library, including serialization, complex command patterns, and integration examples.

## Table of Contents

1. [Building a Text Editor with Persistent Undo](#text-editor)
2. [Graphics Editor with Composite Operations](#graphics-editor)
3. [Document Management with Save Boundaries](#document-management)
4. [Distributed Undo/Redo with Serialization](#distributed-undo)
5. [Performance Optimization Techniques](#performance)
6. [Error Handling and Recovery](#error-handling)

## Building a Text Editor with Persistent Undo {#text-editor}

Let's build a complete text editor with undo/redo functionality that persists across sessions.

### Step 1: Define the Text Editor Interface

```csharp
public interface ITextEditor
{
    string Text { get; set; }
    int CaretPosition { get; set; }
    
    void Insert(int position, string text);
    void Delete(int position, int length);
    void Replace(int position, int length, string newText);
    
    event EventHandler<TextChangedEventArgs> TextChanged;
}

public class TextChangedEventArgs : EventArgs
{
    public int Position { get; set; }
    public string? OldText { get; set; }
    public string? NewText { get; set; }
    public TextChangeType ChangeType { get; set; }
}

public enum TextChangeType
{
    Insert,
    Delete,
    Replace
}
```

### Step 2: Create Serializable Text Commands

```csharp
public class TextCommand : BaseCommand, ISerializableCommand
{
    private ITextEditor? _editor;
    
    public int Position { get; private set; }
    public string OldText { get; private set; } = string.Empty;
    public string NewText { get; private set; } = string.Empty;
    public TextChangeType ChangeType { get; private set; }
    
    // Constructor for creating new commands
    public TextCommand(ITextEditor editor, int position, string oldText, string newText, TextChangeType changeType)
        : base(MapChangeType(changeType), new[] { $"text:{position}-{position + Math.Max(oldText.Length, newText.Length)}" }, 
               $"editor:{GetLineColumn(editor, position)}")
    {
        _editor = editor;
        Position = position;
        OldText = oldText;
        NewText = newText;
        ChangeType = changeType;
    }
    
    // Parameterless constructor for deserialization
    public TextCommand() : base(Core.ChangeType.Modify, Array.Empty<string>())
    {
    }
    
    public override string Description => ChangeType switch
    {
        TextChangeType.Insert => $"Insert '{NewText}' at line {GetLineNumber()}",
        TextChangeType.Delete => $"Delete '{OldText}' at line {GetLineNumber()}",
        TextChangeType.Replace => $"Replace '{OldText}' with '{NewText}' at line {GetLineNumber()}",
        _ => "Text change"
    };
    
    public override void Execute()
    {
        switch (ChangeType)
        {
            case TextChangeType.Insert:
                _editor?.Insert(Position, NewText);
                break;
            case TextChangeType.Delete:
                _editor?.Delete(Position, OldText.Length);
                break;
            case TextChangeType.Replace:
                _editor?.Replace(Position, OldText.Length, NewText);
                break;
        }
    }
    
    public override void Undo()
    {
        switch (ChangeType)
        {
            case TextChangeType.Insert:
                _editor?.Delete(Position, NewText.Length);
                break;
            case TextChangeType.Delete:
                _editor?.Insert(Position, OldText);
                break;
            case TextChangeType.Replace:
                _editor?.Replace(Position, NewText.Length, OldText);
                break;
        }
    }
    
    public override bool CanMergeWith(ICommand other)
    {
        if (other is not TextCommand textCmd) return false;
        
        // Merge consecutive character insertions
        if (ChangeType == TextChangeType.Insert && 
            textCmd.ChangeType == TextChangeType.Insert &&
            textCmd.Position == Position + NewText.Length &&
            NewText.Length == 1 && textCmd.NewText.Length == 1)
        {
            return true;
        }
        
        // Merge consecutive character deletions
        if (ChangeType == TextChangeType.Delete && 
            textCmd.ChangeType == TextChangeType.Delete &&
            textCmd.Position + textCmd.OldText.Length == Position &&
            OldText.Length == 1 && textCmd.OldText.Length == 1)
        {
            return true;
        }
        
        return false;
    }
    
    public override ICommand MergeWith(ICommand other)
    {
        var textCmd = (TextCommand)other;
        
        if (ChangeType == TextChangeType.Insert && textCmd.ChangeType == TextChangeType.Insert)
        {
            return new TextCommand(_editor!, Position, "", NewText + textCmd.NewText, TextChangeType.Insert);
        }
        
        if (ChangeType == TextChangeType.Delete && textCmd.ChangeType == TextChangeType.Delete)
        {
            return new TextCommand(_editor!, textCmd.Position, textCmd.OldText + OldText, "", TextChangeType.Delete);
        }
        
        throw new NotSupportedException("Cannot merge these commands");
    }
    
    public string SerializeData()
    {
        return JsonSerializer.Serialize(new
        {
            Position,
            OldText,
            NewText,
            ChangeType = ChangeType.ToString()
        });
    }
    
    public void DeserializeData(string data)
    {
        var obj = JsonSerializer.Deserialize<JsonElement>(data);
        Position = obj.GetProperty("Position").GetInt32();
        OldText = obj.GetProperty("OldText").GetString() ?? string.Empty;
        NewText = obj.GetProperty("NewText").GetString() ?? string.Empty;
        ChangeType = Enum.Parse<TextChangeType>(obj.GetProperty("ChangeType").GetString()!);
    }
    
    public void SetEditor(ITextEditor editor)
    {
        _editor = editor;
    }
    
    private static Core.ChangeType MapChangeType(TextChangeType changeType) => changeType switch
    {
        TextChangeType.Insert => Core.ChangeType.Insert,
        TextChangeType.Delete => Core.ChangeType.Delete,
        TextChangeType.Replace => Core.ChangeType.Modify,
        _ => Core.ChangeType.Modify
    };
    
    private int GetLineNumber()
    {
        if (_editor == null) return 1;
        return _editor.Text[..Position].Count(c => c == '\n') + 1;
    }
    
    private static string GetLineColumn(ITextEditor editor, int position)
    {
        var textUpToPosition = editor.Text[..position];
        var lineNumber = textUpToPosition.Count(c => c == '\n') + 1;
        var columnNumber = position - textUpToPosition.LastIndexOf('\n');
        return $"{lineNumber}:{columnNumber}";
    }
}
```

### Step 3: Text Editor with Undo/Redo Integration

```csharp
public class TextEditorWithUndo : ITextEditor, IDisposable
{
    private readonly UndoRedoStack _undoRedoStack;
    private readonly string _persistenceFile;
    private string _text = "";
    private int _caretPosition = 0;
    private bool _isUpdatingFromCommand = false;
    
    public TextEditorWithUndo(string? persistenceFile = null)
    {
        _undoRedoStack = new UndoRedoStack();
        _undoRedoStack.SetSerializer(new JsonUndoRedoSerializer());
        
        if (!string.IsNullOrEmpty(persistenceFile))
        {
            _persistenceFile = persistenceFile;
            _ = LoadStateAsync();
        }
        
        // Set up auto-save
        var timer = new System.Timers.Timer(30000); // 30 seconds
        timer.Elapsed += (_, _) => _ = SaveStateAsync();
        timer.Start();
    }
    
    public string Text
    {
        get => _text;
        set
        {
            if (_text != value && !_isUpdatingFromCommand)
            {
                var oldText = _text;
                _text = value;
                
                // Create command for text replacement
                var command = new TextCommand(this, 0, oldText, value, TextChangeType.Replace);
                _undoRedoStack.Execute(command);
                
                TextChanged?.Invoke(this, new TextChangedEventArgs
                {
                    Position = 0,
                    OldText = oldText,
                    NewText = value,
                    ChangeType = TextChangeType.Replace
                });
            }
            else
            {
                _text = value;
            }
        }
    }
    
    public int CaretPosition
    {
        get => _caretPosition;
        set => _caretPosition = Math.Max(0, Math.Min(value, _text.Length));
    }
    
    public event EventHandler<TextChangedEventArgs>? TextChanged;
    
    public void Insert(int position, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        
        position = Math.Max(0, Math.Min(position, _text.Length));
        
        if (!_isUpdatingFromCommand)
        {
            var command = new TextCommand(this, position, "", text, TextChangeType.Insert);
            _undoRedoStack.Execute(command);
        }
        else
        {
            _text = _text.Insert(position, text);
            TextChanged?.Invoke(this, new TextChangedEventArgs
            {
                Position = position,
                OldText = "",
                NewText = text,
                ChangeType = TextChangeType.Insert
            });
        }
    }
    
    public void Delete(int position, int length)
    {
        if (length <= 0) return;
        
        position = Math.Max(0, Math.Min(position, _text.Length));
        length = Math.Min(length, _text.Length - position);
        
        if (length == 0) return;
        
        var deletedText = _text.Substring(position, length);
        
        if (!_isUpdatingFromCommand)
        {
            var command = new TextCommand(this, position, deletedText, "", TextChangeType.Delete);
            _undoRedoStack.Execute(command);
        }
        else
        {
            _text = _text.Remove(position, length);
            TextChanged?.Invoke(this, new TextChangedEventArgs
            {
                Position = position,
                OldText = deletedText,
                NewText = "",
                ChangeType = TextChangeType.Delete
            });
        }
    }
    
    public void Replace(int position, int length, string newText)
    {
        position = Math.Max(0, Math.Min(position, _text.Length));
        length = Math.Min(length, _text.Length - position);
        
        var oldText = length > 0 ? _text.Substring(position, length) : "";
        
        if (!_isUpdatingFromCommand)
        {
            var command = new TextCommand(this, position, oldText, newText ?? "", TextChangeType.Replace);
            _undoRedoStack.Execute(command);
        }
        else
        {
            _text = _text.Remove(position, length).Insert(position, newText ?? "");
            TextChanged?.Invoke(this, new TextChangedEventArgs
            {
                Position = position,
                OldText = oldText,
                NewText = newText,
                ChangeType = TextChangeType.Replace
            });
        }
    }
    
    public bool CanUndo => _undoRedoStack.CanUndo;
    public bool CanRedo => _undoRedoStack.CanRedo;
    
    public async Task<bool> UndoAsync()
    {
        _isUpdatingFromCommand = true;
        try
        {
            return await _undoRedoStack.UndoAsync();
        }
        finally
        {
            _isUpdatingFromCommand = false;
        }
    }
    
    public async Task<bool> RedoAsync()
    {
        _isUpdatingFromCommand = true;
        try
        {
            return await _undoRedoStack.RedoAsync();
        }
        finally
        {
            _isUpdatingFromCommand = false;
        }
    }
    
    public void MarkAsSaved()
    {
        _undoRedoStack.MarkAsSaved($"Saved at {DateTime.Now:HH:mm:ss}");
    }
    
    public bool HasUnsavedChanges => _undoRedoStack.HasUnsavedChanges;
    
    private async Task SaveStateAsync()
    {
        if (string.IsNullOrEmpty(_persistenceFile)) return;
        
        try
        {
            var data = await _undoRedoStack.SaveStateAsync();
            await File.WriteAllBytesAsync(_persistenceFile, data);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save undo state: {ex.Message}");
        }
    }
    
    private async Task LoadStateAsync()
    {
        if (string.IsNullOrEmpty(_persistenceFile) || !File.Exists(_persistenceFile)) return;
        
        try
        {
            var data = await File.ReadAllBytesAsync(_persistenceFile);
            await _undoRedoStack.LoadStateAsync(data);
            
            // Restore commands need to have their editor reference set
            foreach (var command in _undoRedoStack.Commands.OfType<TextCommand>())
            {
                command.SetEditor(this);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load undo state: {ex.Message}");
        }
    }
    
    public void Dispose()
    {
        _ = SaveStateAsync();
    }
}
```

### Step 4: Usage Example

```csharp
class Program
{
    static async Task Main(string[] args)
    {
        using var editor = new TextEditorWithUndo("editor_undo.json");
        
        // Simulate user typing
        editor.Text = "Hello";
        editor.Insert(5, " World");
        editor.Insert(11, "!");
        
        Console.WriteLine($"Text: '{editor.Text}'"); // "Hello World!"
        
        // Save the document
        editor.MarkAsSaved();
        
        // Make more changes
        editor.Replace(6, 5, "Universe");
        Console.WriteLine($"Text: '{editor.Text}'"); // "Hello Universe!"
        
        // Undo the replacement
        await editor.UndoAsync();
        Console.WriteLine($"Text: '{editor.Text}'"); // "Hello World!"
        
        // Check if we have unsaved changes
        Console.WriteLine($"Has unsaved changes: {editor.HasUnsavedChanges}"); // False (back to save point)
    }
}
```

## Graphics Editor with Composite Operations {#graphics-editor}

Let's build a graphics editor that supports complex composite operations.

### Step 1: Define Graphics Primitives

```csharp
public interface IShape
{
    string Id { get; }
    Point Position { get; set; }
    Size Size { get; set; }
    Color Color { get; set; }
    bool IsVisible { get; set; }
}

public class Rectangle : IShape
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public Point Position { get; set; }
    public Size Size { get; set; }
    public Color Color { get; set; } = Color.Black;
    public bool IsVisible { get; set; } = true;
}

public interface ICanvas
{
    IReadOnlyList<IShape> Shapes { get; }
    void AddShape(IShape shape);
    void RemoveShape(string shapeId);
    IShape? GetShape(string shapeId);
    void UpdateShape(string shapeId, Action<IShape> update);
}
```

### Step 2: Create Graphics Commands

```csharp
public class AddShapeCommand : BaseCommand, ISerializableCommand
{
    private ICanvas? _canvas;
    private IShape? _shape;
    private string _shapeId = string.Empty;
    private string _shapeData = string.Empty;
    
    public AddShapeCommand(ICanvas canvas, IShape shape)
        : base(ChangeType.Insert, new[] { $"shape:{shape.Id}" }, $"canvas:{shape.Position.X},{shape.Position.Y}")
    {
        _canvas = canvas;
        _shape = shape;
        _shapeId = shape.Id;
    }
    
    public AddShapeCommand() : base(ChangeType.Insert, Array.Empty<string>()) { }
    
    public override string Description => $"Add {_shape?.GetType().Name ?? "Shape"}";
    
    public override void Execute()
    {
        if (_canvas != null && _shape != null)
        {
            _canvas.AddShape(_shape);
        }
    }
    
    public override void Undo()
    {
        if (_canvas != null && !string.IsNullOrEmpty(_shapeId))
        {
            _canvas.RemoveShape(_shapeId);
        }
    }
    
    public string SerializeData()
    {
        return JsonSerializer.Serialize(new
        {
            ShapeId = _shapeId,
            ShapeType = _shape?.GetType().AssemblyQualifiedName,
            ShapeData = JsonSerializer.Serialize(_shape)
        });
    }
    
    public void DeserializeData(string data)
    {
        var obj = JsonSerializer.Deserialize<JsonElement>(data);
        _shapeId = obj.GetProperty("ShapeId").GetString()!;
        _shapeData = obj.GetProperty("ShapeData").GetString()!;
        
        // Shape reconstruction would need a factory pattern in real implementation
        // For simplicity, we'll create a placeholder
    }
    
    public void SetCanvas(ICanvas canvas)
    {
        _canvas = canvas;
    }
}

public class TransformShapeCommand : BaseCommand, ISerializableCommand
{
    private ICanvas? _canvas;
    private string _shapeId;
    private Point _oldPosition;
    private Point _newPosition;
    private Size _oldSize;
    private Size _newSize;
    
    public TransformShapeCommand(ICanvas canvas, string shapeId, Point newPosition, Size newSize)
        : base(ChangeType.Modify, new[] { $"shape:{shapeId}" }, $"canvas:{newPosition.X},{newPosition.Y}")
    {
        _canvas = canvas;
        _shapeId = shapeId;
        _newPosition = newPosition;
        _newSize = newSize;
        
        // Store current state
        var shape = canvas.GetShape(shapeId);
        if (shape != null)
        {
            _oldPosition = shape.Position;
            _oldSize = shape.Size;
        }
    }
    
    public TransformShapeCommand() : base(ChangeType.Modify, Array.Empty<string>())
    {
        _shapeId = string.Empty;
    }
    
    public override string Description => $"Transform shape {_shapeId}";
    
    public override void Execute()
    {
        var shape = _canvas?.GetShape(_shapeId);
        if (shape != null)
        {
            shape.Position = _newPosition;
            shape.Size = _newSize;
        }
    }
    
    public override void Undo()
    {
        var shape = _canvas?.GetShape(_shapeId);
        if (shape != null)
        {
            shape.Position = _oldPosition;
            shape.Size = _oldSize;
        }
    }
    
    public string SerializeData()
    {
        return JsonSerializer.Serialize(new
        {
            ShapeId = _shapeId,
            OldPosition = _oldPosition,
            NewPosition = _newPosition,
            OldSize = _oldSize,
            NewSize = _newSize
        });
    }
    
    public void DeserializeData(string data)
    {
        var obj = JsonSerializer.Deserialize<JsonElement>(data);
        _shapeId = obj.GetProperty("ShapeId").GetString()!;
        
        var oldPos = obj.GetProperty("OldPosition");
        _oldPosition = new Point(oldPos.GetProperty("X").GetInt32(), oldPos.GetProperty("Y").GetInt32());
        
        var newPos = obj.GetProperty("NewPosition");
        _newPosition = new Point(newPos.GetProperty("X").GetInt32(), newPos.GetProperty("Y").GetInt32());
        
        var oldSize = obj.GetProperty("OldSize");
        _oldSize = new Size(oldSize.GetProperty("Width").GetInt32(), oldSize.GetProperty("Height").GetInt32());
        
        var newSize = obj.GetProperty("NewSize");
        _newSize = new Size(newSize.GetProperty("Width").GetInt32(), newSize.GetProperty("Height").GetInt32());
    }
    
    public void SetCanvas(ICanvas canvas)
    {
        _canvas = canvas;
    }
}
```

### Step 3: Complex Composite Operations

```csharp
public static class GraphicsOperations
{
    public static ICommand CreateAlignmentOperation(ICanvas canvas, IReadOnlyList<string> shapeIds, AlignmentType alignment)
    {
        var commands = new List<ICommand>();
        
        foreach (var shapeId in shapeIds)
        {
            var shape = canvas.GetShape(shapeId);
            if (shape == null) continue;
            
            var newPosition = CalculateAlignedPosition(canvas, shapeIds, shape, alignment);
            if (newPosition != shape.Position)
            {
                commands.Add(new TransformShapeCommand(canvas, shapeId, newPosition, shape.Size));
            }
        }
        
        return new CompositeCommand($"Align {shapeIds.Count} shapes {alignment}", commands);
    }
    
    public static ICommand CreateDistributeOperation(ICanvas canvas, IReadOnlyList<string> shapeIds, DistributionType distribution)
    {
        var commands = new List<ICommand>();
        var shapes = shapeIds.Select(id => canvas.GetShape(id)).Where(s => s != null).ToList();
        
        if (shapes.Count < 3) return new CompositeCommand("Distribute (insufficient shapes)", commands);
        
        var sortedShapes = distribution == DistributionType.Horizontal
            ? shapes.OrderBy(s => s!.Position.X).ToList()
            : shapes.OrderBy(s => s!.Position.Y).ToList();
        
        var totalSpacing = distribution == DistributionType.Horizontal
            ? sortedShapes.Last()!.Position.X - sortedShapes.First()!.Position.X
            : sortedShapes.Last()!.Position.Y - sortedShapes.First()!.Position.Y;
        
        var spacing = totalSpacing / (sortedShapes.Count - 1);
        
        for (int i = 1; i < sortedShapes.Count - 1; i++)
        {
            var shape = sortedShapes[i]!;
            var newPosition = distribution == DistributionType.Horizontal
                ? new Point(sortedShapes.First()!.Position.X + (i * spacing), shape.Position.Y)
                : new Point(shape.Position.X, sortedShapes.First()!.Position.Y + (i * spacing));
            
            if (newPosition != shape.Position)
            {
                commands.Add(new TransformShapeCommand(canvas, shape.Id, newPosition, shape.Size));
            }
        }
        
        return new CompositeCommand($"Distribute {shapeIds.Count} shapes {distribution}ly", commands);
    }
    
    public static ICommand CreateGroupOperation(ICanvas canvas, IReadOnlyList<string> shapeIds)
    {
        // This would create a group shape containing the selected shapes
        var commands = new List<ICommand>();
        
        // Calculate bounding box
        var shapes = shapeIds.Select(id => canvas.GetShape(id)).Where(s => s != null).ToList();
        if (!shapes.Any()) return new CompositeCommand("Group (no shapes)", commands);
        
        var bounds = CalculateBoundingBox(shapes);
        var groupShape = new GroupShape(shapeIds.ToList())
        {
            Position = bounds.Location,
            Size = bounds.Size
        };
        
        // Add the group
        commands.Add(new AddShapeCommand(canvas, groupShape));
        
        // Hide the individual shapes (they're now part of the group)
        foreach (var shapeId in shapeIds)
        {
            commands.Add(new SetVisibilityCommand(canvas, shapeId, false));
        }
        
        return new CompositeCommand($"Group {shapeIds.Count} shapes", commands);
    }
    
    private static Point CalculateAlignedPosition(ICanvas canvas, IReadOnlyList<string> shapeIds, IShape shape, AlignmentType alignment)
    {
        var shapes = shapeIds.Select(id => canvas.GetShape(id)).Where(s => s != null).ToList();
        if (!shapes.Any()) return shape.Position;
        
        return alignment switch
        {
            AlignmentType.Left => new Point(shapes.Min(s => s!.Position.X), shape.Position.Y),
            AlignmentType.Right => new Point(shapes.Max(s => s!.Position.X + s.Size.Width) - shape.Size.Width, shape.Position.Y),
            AlignmentType.Top => new Point(shape.Position.X, shapes.Min(s => s!.Position.Y)),
            AlignmentType.Bottom => new Point(shape.Position.X, shapes.Max(s => s!.Position.Y + s.Size.Height) - shape.Size.Height),
            AlignmentType.CenterHorizontal => new Point((shapes.Min(s => s!.Position.X) + shapes.Max(s => s!.Position.X + s.Size.Width)) / 2 - shape.Size.Width / 2, shape.Position.Y),
            AlignmentType.CenterVertical => new Point(shape.Position.X, (shapes.Min(s => s!.Position.Y) + shapes.Max(s => s!.Position.Y + s.Size.Height)) / 2 - shape.Size.Height / 2),
            _ => shape.Position
        };
    }
    
    private static System.Drawing.Rectangle CalculateBoundingBox(IList<IShape> shapes)
    {
        if (!shapes.Any()) return System.Drawing.Rectangle.Empty;
        
        var left = shapes.Min(s => s.Position.X);
        var top = shapes.Min(s => s.Position.Y);
        var right = shapes.Max(s => s.Position.X + s.Size.Width);
        var bottom = shapes.Max(s => s.Position.Y + s.Size.Height);
        
        return new System.Drawing.Rectangle(left, top, right - left, bottom - top);
    }
}

public enum AlignmentType
{
    Left, Right, Top, Bottom, CenterHorizontal, CenterVertical
}

public enum DistributionType
{
    Horizontal, Vertical
}

public class GroupShape : IShape
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public Point Position { get; set; }
    public Size Size { get; set; }
    public Color Color { get; set; } = Color.Transparent;
    public bool IsVisible { get; set; } = true;
    public List<string> ChildShapeIds { get; set; }
    
    public GroupShape(List<string> childShapeIds)
    {
        ChildShapeIds = childShapeIds;
    }
}

public class SetVisibilityCommand : BaseCommand
{
    private readonly ICanvas _canvas;
    private readonly string _shapeId;
    private readonly bool _newVisibility;
    private bool _oldVisibility;
    
    public SetVisibilityCommand(ICanvas canvas, string shapeId, bool visibility)
        : base(ChangeType.Modify, new[] { $"shape:{shapeId}" })
    {
        _canvas = canvas;
        _shapeId = shapeId;
        _newVisibility = visibility;
        
        var shape = canvas.GetShape(shapeId);
        if (shape != null)
        {
            _oldVisibility = shape.IsVisible;
        }
    }
    
    public override string Description => $"{(_newVisibility ? "Show" : "Hide")} shape {_shapeId}";
    
    public override void Execute()
    {
        var shape = _canvas.GetShape(_shapeId);
        if (shape != null)
        {
            shape.IsVisible = _newVisibility;
        }
    }
    
    public override void Undo()
    {
        var shape = _canvas.GetShape(_shapeId);
        if (shape != null)
        {
            shape.IsVisible = _oldVisibility;
        }
    }
}
```

### Step 4: Graphics Editor with Undo/Redo

```csharp
public class GraphicsEditor
{
    private readonly UndoRedoStack _undoRedoStack;
    private readonly ICanvas _canvas;
    private readonly List<string> _selectedShapeIds = new();
    
    public GraphicsEditor(ICanvas canvas)
    {
        _canvas = canvas;
        _undoRedoStack = new UndoRedoStack();
        _undoRedoStack.SetSerializer(new JsonUndoRedoSerializer());
    }
    
    public IReadOnlyList<string> SelectedShapeIds => _selectedShapeIds.AsReadOnly();
    
    public void AddRectangle(Point position, Size size, Color color)
    {
        var rectangle = new Rectangle
        {
            Position = position,
            Size = size,
            Color = color
        };
        
        var command = new AddShapeCommand(_canvas, rectangle);
        _undoRedoStack.Execute(command);
    }
    
    public void TransformShape(string shapeId, Point newPosition, Size newSize)
    {
        var command = new TransformShapeCommand(_canvas, shapeId, newPosition, newSize);
        _undoRedoStack.Execute(command);
    }
    
    public void AlignSelectedShapes(AlignmentType alignment)
    {
        if (_selectedShapeIds.Count < 2) return;
        
        var command = GraphicsOperations.CreateAlignmentOperation(_canvas, _selectedShapeIds, alignment);
        _undoRedoStack.Execute(command);
    }
    
    public void DistributeSelectedShapes(DistributionType distribution)
    {
        if (_selectedShapeIds.Count < 3) return;
        
        var command = GraphicsOperations.CreateDistributeOperation(_canvas, _selectedShapeIds, distribution);
        _undoRedoStack.Execute(command);
    }
    
    public void GroupSelectedShapes()
    {
        if (_selectedShapeIds.Count < 2) return;
        
        var command = GraphicsOperations.CreateGroupOperation(_canvas, _selectedShapeIds);
        _undoRedoStack.Execute(command);
        
        // Clear selection after grouping
        _selectedShapeIds.Clear();
    }
    
    public void SelectShape(string shapeId)
    {
        if (!_selectedShapeIds.Contains(shapeId))
        {
            _selectedShapeIds.Add(shapeId);
        }
    }
    
    public void DeselectShape(string shapeId)
    {
        _selectedShapeIds.Remove(shapeId);
    }
    
    public void ClearSelection()
    {
        _selectedShapeIds.Clear();
    }
    
    public async Task<bool> UndoAsync() => await _undoRedoStack.UndoAsync();
    public async Task<bool> RedoAsync() => await _undoRedoStack.RedoAsync();
    
    public bool CanUndo => _undoRedoStack.CanUndo;
    public bool CanRedo => _undoRedoStack.CanRedo;
    
    public void MarkAsSaved() => _undoRedoStack.MarkAsSaved();
    public bool HasUnsavedChanges => _undoRedoStack.HasUnsavedChanges;
}
```

This tutorial demonstrates advanced patterns including:

1. **Serializable Commands**: Commands that can be persisted and restored
2. **Composite Operations**: Complex operations built from simpler commands
3. **Command Merging**: Automatically combining related operations
4. **Real-world Integration**: Practical examples with UI concerns
5. **State Management**: Proper handling of editor state during undo/redo
6. **Performance Considerations**: Efficient command design and execution

The patterns shown here can be adapted for many different types of applications, from text editors to CAD software to data manipulation tools. 
