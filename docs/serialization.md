---
title: "Serialization and Persistence"
description: "Guide to serializing and persisting undo/redo stack state"
status: draft
---

# Serialization and Persistence

The ktsu.UndoRedo library supports serializing and persisting the undo/redo stack state, allowing you to save and restore command history across application sessions.

## Overview

Serialization enables you to:
- Save undo/redo state to disk for crash recovery
- Implement session persistence across application restarts
- Create checkpoints for complex operations
- Implement distributed undo/redo across multiple instances

## Core Concepts

### IUndoRedoSerializer Interface

The serialization system is built around the `IUndoRedoSerializer` interface:

```csharp
public interface IUndoRedoSerializer
{
    string FormatVersion { get; }
    bool SupportsVersion(string version);
    
    Task<byte[]> SerializeAsync(
        IReadOnlyList<ICommand> commands,
        int currentPosition,
        IReadOnlyList<SaveBoundary> saveBoundaries,
        CancellationToken cancellationToken = default);
        
    Task<UndoRedoStackState> DeserializeAsync(
        byte[] data,
        CancellationToken cancellationToken = default);
}
```

### UndoRedoStackState

The `UndoRedoStackState` record represents the complete state of an undo/redo stack:

```csharp
public record UndoRedoStackState(
    IReadOnlyList<ICommand> Commands,
    int CurrentPosition,
    IReadOnlyList<SaveBoundary> SaveBoundaries,
    string FormatVersion,
    DateTime Timestamp);
```

## Basic Usage

### Setting Up Serialization

```csharp
// Create the undo/redo stack
var undoRedoStack = new UndoRedoStack();

// Configure JSON serializer
var serializer = new JsonUndoRedoSerializer();
undoRedoStack.SetSerializer(serializer);

// Execute some commands
undoRedoStack.Execute(new DelegateCommand("Operation 1", () => DoSomething(), () => UndoSomething()));
undoRedoStack.Execute(new DelegateCommand("Operation 2", () => DoMore(), () => UndoMore()));
```

### Saving State

```csharp
// Save the current state
byte[] serializedState = await undoRedoStack.SaveStateAsync();

// Save to file
await File.WriteAllBytesAsync("undo_stack.json", serializedState);
```

### Loading State

```csharp
// Load from file
byte[] data = await File.ReadAllBytesAsync("undo_stack.json");

// Restore the state
bool success = await undoRedoStack.LoadStateAsync(data);
if (success)
{
    Console.WriteLine($"Restored {undoRedoStack.CommandCount} commands");
}
```

## Advanced Scenarios

### Implementing Custom Serializers

Create a custom serializer for specific formats:

```csharp
public class BinaryUndoRedoSerializer : IUndoRedoSerializer
{
    public string FormatVersion => "binary-v1.0";
    
    public bool SupportsVersion(string version) => version.StartsWith("binary-v1.");
    
    public async Task<byte[]> SerializeAsync(
        IReadOnlyList<ICommand> commands,
        int currentPosition,
        IReadOnlyList<SaveBoundary> saveBoundaries,
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        
        // Write format version
        writer.Write(FormatVersion);
        writer.Write(DateTime.UtcNow.ToBinary());
        
        // Write commands
        writer.Write(commands.Count);
        writer.Write(currentPosition);
        
        foreach (var command in commands)
        {
            await SerializeCommand(writer, command);
        }
        
        // Write save boundaries
        writer.Write(saveBoundaries.Count);
        foreach (var boundary in saveBoundaries)
        {
            writer.Write(boundary.Position);
            writer.Write(boundary.Timestamp.ToBinary());
            writer.Write(boundary.Description ?? string.Empty);
        }
        
        return stream.ToArray();
    }
    
    public async Task<UndoRedoStackState> DeserializeAsync(
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);
        
        // Read format version
        string version = reader.ReadString();
        if (!SupportsVersion(version))
        {
            throw new NotSupportedException($"Unsupported version: {version}");
        }
        
        DateTime timestamp = DateTime.FromBinary(reader.ReadInt64());
        
        // Read commands
        int commandCount = reader.ReadInt32();
        int currentPosition = reader.ReadInt32();
        
        var commands = new List<ICommand>();
        for (int i = 0; i < commandCount; i++)
        {
            commands.Add(await DeserializeCommand(reader));
        }
        
        // Read save boundaries
        int boundaryCount = reader.ReadInt32();
        var boundaries = new List<SaveBoundary>();
        for (int i = 0; i < boundaryCount; i++)
        {
            int position = reader.ReadInt32();
            DateTime boundaryTime = DateTime.FromBinary(reader.ReadInt64());
            string description = reader.ReadString();
            boundaries.Add(new SaveBoundary(position, boundaryTime, description));
        }
        
        return new UndoRedoStackState(commands, currentPosition, boundaries, version, timestamp);
    }
    
    private async Task SerializeCommand(BinaryWriter writer, ICommand command)
    {
        // Implement command serialization logic
        // This is application-specific
    }
    
    private async Task<ICommand> DeserializeCommand(BinaryReader reader)
    {
        // Implement command deserialization logic
        // This is application-specific
        throw new NotImplementedException();
    }
}
```

### Serializable Commands

For commands to be properly serialized and restored, implement `ISerializableCommand`:

```csharp
public class TextEditCommand : BaseCommand, ISerializableCommand
{
    public int Position { get; private set; }
    public string OldText { get; private set; } = string.Empty;
    public string NewText { get; private set; } = string.Empty;
    
    private ITextEditor? _editor;
    
    public TextEditCommand(ITextEditor editor, int position, string oldText, string newText)
        : base(ChangeType.Modify, new[] { $"text:{position}" }, $"editor:{position}")
    {
        _editor = editor;
        Position = position;
        OldText = oldText;
        NewText = newText;
    }
    
    // Parameterless constructor for deserialization
    public TextEditCommand() : base(ChangeType.Modify, Array.Empty<string>()) { }
    
    public override string Description => $"Replace '{OldText}' with '{NewText}' at {Position}";
    
    public override void Execute()
    {
        _editor?.ReplaceText(Position, OldText.Length, NewText);
    }
    
    public override void Undo()
    {
        _editor?.ReplaceText(Position, NewText.Length, OldText);
    }
    
    public string SerializeData()
    {
        return JsonSerializer.Serialize(new
        {
            Position,
            OldText,
            NewText
        });
    }
    
    public void DeserializeData(string data)
    {
        var obj = JsonSerializer.Deserialize<JsonElement>(data);
        Position = obj.GetProperty("Position").GetInt32();
        OldText = obj.GetProperty("OldText").GetString() ?? string.Empty;
        NewText = obj.GetProperty("NewText").GetString() ?? string.Empty;
        
        // Note: _editor needs to be set externally after deserialization
        // This is typically done through dependency injection or a factory
    }
    
    public void SetEditor(ITextEditor editor)
    {
        _editor = editor;
    }
}
```

### Handling Non-Serializable Commands

Some commands may not be serializable. The library handles this gracefully:

```csharp
// Commands that can't be serialized become placeholder commands
var nonSerializableCommand = new DelegateCommand("Complex operation", 
    () => DoComplexWork(), () => UndoComplexWork());
    
undoRedoStack.Execute(nonSerializableCommand);

// After serialization/deserialization, this becomes a PlaceholderCommand
// that preserves metadata but can't be executed
```

### Session Persistence

Implement automatic session persistence:

```csharp
public class PersistentUndoRedoStack : IDisposable
{
    private readonly UndoRedoStack _undoRedoStack;
    private readonly string _persistenceFile;
    private readonly Timer _autoSaveTimer;
    
    public PersistentUndoRedoStack(string persistenceFile)
    {
        _persistenceFile = persistenceFile;
        _undoRedoStack = new UndoRedoStack();
        _undoRedoStack.SetSerializer(new JsonUndoRedoSerializer());
        
        // Auto-save every 30 seconds
        _autoSaveTimer = new Timer(AutoSave, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        
        // Load existing state
        _ = LoadStateAsync();
    }
    
    public UndoRedoStack UndoRedoStack => _undoRedoStack;
    
    private async void AutoSave(object? state)
    {
        try
        {
            await SaveStateAsync();
        }
        catch (Exception ex)
        {
            // Log error but don't crash the application
            Console.WriteLine($"Auto-save failed: {ex.Message}");
        }
    }
    
    public async Task SaveStateAsync()
    {
        if (_undoRedoStack.CommandCount == 0) return;
        
        var data = await _undoRedoStack.SaveStateAsync();
        await File.WriteAllBytesAsync(_persistenceFile, data);
    }
    
    public async Task LoadStateAsync()
    {
        if (!File.Exists(_persistenceFile)) return;
        
        try
        {
            var data = await File.ReadAllBytesAsync(_persistenceFile);
            await _undoRedoStack.LoadStateAsync(data);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load state: {ex.Message}");
        }
    }
    
    public void Dispose()
    {
        _autoSaveTimer?.Dispose();
        // Save state one final time
        SaveStateAsync().Wait(TimeSpan.FromSeconds(5));
    }
}
```

## Best Practices

### 1. Design for Serializability

When creating custom commands, consider serialization from the start:

```csharp
// Good: Serializable command
public class MoveCommand : BaseCommand, ISerializableCommand
{
    public int ItemId { get; private set; }
    public Point OldPosition { get; private set; }
    public Point NewPosition { get; private set; }
    
    // Implementation with serialization support
}

// Avoid: Commands with complex dependencies
public class ComplexCommand : BaseCommand
{
    private readonly DatabaseConnection _db;
    private readonly NetworkService _network;
    private readonly UIComponent _ui;
    
    // This would be difficult to serialize
}
```

### 2. Version Your Serialization Format

Always version your serialization format:

```csharp
public class MyCustomSerializer : IUndoRedoSerializer
{
    public string FormatVersion => "myapp-v1.2";
    
    public bool SupportsVersion(string version)
    {
        return version switch
        {
            "myapp-v1.0" => true,
            "myapp-v1.1" => true,
            "myapp-v1.2" => true,
            _ => false
        };
    }
    
    // Handle version differences in serialization/deserialization
}
```

### 3. Handle Serialization Errors Gracefully

```csharp
public async Task<bool> TryLoadStateAsync(string filePath)
{
    try
    {
        var data = await File.ReadAllBytesAsync(filePath);
        return await _undoRedoStack.LoadStateAsync(data);
    }
    catch (FileNotFoundException)
    {
        // No saved state exists - this is normal
        return false;
    }
    catch (NotSupportedException ex)
    {
        // Version mismatch - could prompt user or use migration
        Logger.Warning($"Unsupported format: {ex.Message}");
        return false;
    }
    catch (Exception ex)
    {
        // Corruption or other error - could backup and start fresh
        Logger.Error($"Failed to load state: {ex.Message}");
        await CreateBackupAsync(filePath);
        return false;
    }
}
```

### 4. Consider Memory Usage

For large stacks, consider implementing compression:

```csharp
public class CompressedJsonSerializer : IUndoRedoSerializer
{
    private readonly JsonUndoRedoSerializer _jsonSerializer = new();
    
    public string FormatVersion => "compressed-json-v1.0";
    
    public async Task<byte[]> SerializeAsync(
        IReadOnlyList<ICommand> commands,
        int currentPosition,
        IReadOnlyList<SaveBoundary> saveBoundaries,
        CancellationToken cancellationToken = default)
    {
        var jsonData = await _jsonSerializer.SerializeAsync(commands, currentPosition, saveBoundaries, cancellationToken);
        
        using var output = new MemoryStream();
        using var gzip = new GZipStream(output, CompressionLevel.Optimal);
        await gzip.WriteAsync(jsonData, cancellationToken);
        await gzip.FlushAsync(cancellationToken);
        
        return output.ToArray();
    }
    
    public async Task<UndoRedoStackState> DeserializeAsync(
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        
        await gzip.CopyToAsync(output, cancellationToken);
        var jsonData = output.ToArray();
        
        return await _jsonSerializer.DeserializeAsync(jsonData, cancellationToken);
    }
    
    public bool SupportsVersion(string version) => version == FormatVersion;
}
```

## Integration Examples

### WPF Application with Persistence

```csharp
public partial class MainWindow : Window
{
    private readonly PersistentUndoRedoStack _undoRedoStack;
    
    public MainWindow()
    {
        InitializeComponent();
        
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MyApp");
        Directory.CreateDirectory(appDataPath);
        
        _undoRedoStack = new PersistentUndoRedoStack(
            Path.Combine(appDataPath, "undo_stack.json"));
        
        // Set up command factory for deserialization
        SetupCommandFactory();
    }
    
    private void SetupCommandFactory()
    {
        // Register command types for deserialization
        CommandFactory.Register<TextEditCommand>(() => new TextEditCommand());
        CommandFactory.Register<MoveCommand>(() => new MoveCommand());
    }
    
    protected override void OnClosed(EventArgs e)
    {
        _undoRedoStack.Dispose();
        base.OnClosed(e);
    }
}
```

## Limitations and Considerations

1. **Action Serialization**: Delegate-based commands (using `Action` for execute/undo) cannot be serialized. Use custom command classes instead.

2. **Dependency Injection**: Deserialized commands may need dependencies injected after restoration.

3. **Format Evolution**: Plan for format changes and provide migration paths.

4. **Security**: Serialized data may contain sensitive information. Consider encryption for sensitive applications.

5. **Performance**: Serialization can be expensive for large stacks. Consider implementing periodic cleanup or compression.

6. **Cross-Platform**: Ensure serialization format works across different platforms and .NET versions.

## Error Handling

The serialization system provides comprehensive error handling:

```csharp
try
{
    var data = await undoRedoStack.SaveStateAsync();
    await File.WriteAllBytesAsync("backup.json", data);
}
catch (InvalidOperationException ex) when (ex.Message.Contains("No serializer"))
{
    // Handle missing serializer
    undoRedoStack.SetSerializer(new JsonUndoRedoSerializer());
}
catch (NotSupportedException ex)
{
    // Handle unsupported format
    Console.WriteLine($"Format not supported: {ex.Message}");
}
catch (Exception ex)
{
    // Handle other serialization errors
    Console.WriteLine($"Serialization failed: {ex.Message}");
}
``` 
