# Simple Text Editor Example

This console application demonstrates the core features of the ktsu.UndoRedo library, including:

- ✅ **Basic Undo/Redo Operations**
- 💾 **Persistent State with Serialization**
- 📜 **Command History Visualization**
- 🔄 **Save Boundaries and Session Management**
- 📊 **Real-time Status Feedback**

## Features Demonstrated

### Core Undo/Redo Functionality
- Insert, delete, and replace text operations
- Unlimited undo/redo with command history
- Command descriptions and metadata
- Event notifications for all operations

### Serialization and Persistence
- **Session Persistence**: Automatically saves and restores your editing session
- **State Serialization**: Complete undo/redo stack is preserved between runs
- **Save Boundaries**: Mark save points to track unsaved changes
- **Error Handling**: Graceful handling of serialization errors

### User Interface
- **Interactive Console**: Easy-to-use command interface
- **Real-time Feedback**: Shows current document state after each operation
- **Command History**: View complete history of all operations
- **Visual Indicators**: Clear markers for executed/undone commands and save points

## How to Run

```bash
cd Examples/SimpleTextEditor
dotnet run
```

## Usage Examples

### Basic Text Operations
```
> insert 0 Hello
✅ Executed: Insert 'Hello' at position 0
📄 Current Document:
Content: "Hello"

> insert 5 World
✅ Executed: Insert 'World' at position 5
📄 Current Document:
Content: "HelloWorld"

> insert 5 " "
✅ Executed: Insert ' ' at position 5
📄 Current Document:
Content: "Hello World"
```

### Undo/Redo Operations
```
> undo
↩️  Undone: Insert ' ' at position 5
📄 Current Document:
Content: "HelloWorld"

> redo
↪️  Redone: Insert ' ' at position 5
📄 Current Document:
Content: "Hello World"
```

### Save and Session Management
```
> save
💾 Saved: Manual save at 14:30:15
Document and session saved!

> history
📜 Command History:
  ✓ Insert 'Hello' at position 0 💾
  ✓ Insert 'World' at position 5
  ✓ Insert ' ' at position 5

💾 Save Points:
  Position 1: Manual save at 14:30:15 (14:30:15)
```

### Advanced Operations
```
> replace 0 5 Hi
✅ Executed: Replace 'Hello' with 'Hi' at position 0
📄 Current Document:
Content: "Hi World"

> delete 2 6
✅ Executed: Delete ' World' at position 2
📄 Current Document:
Content: "Hi"
```

## Commands Reference

| Command | Syntax | Description |
|---------|--------|-------------|
| `help` | `help` or `h` | Show available commands |
| `show` | `show` or `s` | Display current document state |
| `insert` | `insert <pos> <text>` | Insert text at position |
| `delete` | `delete <pos> <length>` | Delete text from position |
| `replace` | `replace <pos> <len> <text>` | Replace text at position |
| `undo` | `undo` or `u` | Undo last command |
| `redo` | `redo` or `y` | Redo next command |
| `save` | `save` | Save document and session |
| `history` | `history` | Show command history |
| `clear` | `clear` | Clear entire document |
| `exit` | `exit`, `quit`, or `q` | Exit the editor |

## Technical Implementation

### Command Pattern
Each text operation is implemented as a command with:
- **Execute**: Applies the change
- **Undo**: Reverts the change
- **Metadata**: Includes change type, affected items, and description

### Serialization
- Uses `JsonUndoRedoSerializer` for human-readable persistence
- Saves complete stack state including commands and save boundaries
- Automatic session restoration on startup
- Graceful error handling for corrupted or missing files

### Event System
Real-time feedback through event handlers:
```csharp
_undoRedoStack.CommandExecuted += (_, e) => Console.WriteLine($"✅ Executed: {e.Command.Description}");
_undoRedoStack.CommandUndone += (_, e) => Console.WriteLine($"↩️  Undone: {e.Command.Description}");
_undoRedoStack.SaveBoundaryCreated += (_, e) => Console.WriteLine($"💾 Saved: {e.SaveBoundary.Description}");
```

## Session Persistence

The editor automatically:
1. **Saves session state** to `editor_state.json` on exit
2. **Restores previous session** on startup
3. **Preserves command history** across runs
4. **Maintains save boundaries** and unsaved change tracking

## Learning Opportunities

This example demonstrates:
- How to integrate undo/redo into existing applications
- Best practices for command design and implementation
- Effective use of serialization for session persistence
- Event-driven UI updates and user feedback
- Error handling and graceful degradation

## Extending the Example

You can enhance this example by:
- Adding more complex text operations (find/replace, formatting)
- Implementing command merging for typing operations
- Adding file I/O operations with save boundaries
- Creating a GUI version using WPF or WinUI
- Adding collaborative editing features

## Related Documentation

- [API Reference](../../docs/api-reference.md)
- [Serialization Guide](../../docs/serialization.md)
- [Advanced Scenarios Tutorial](../../docs/tutorial-advanced-scenarios.md)
- [Best Practices](../../docs/best-practices.md) 
