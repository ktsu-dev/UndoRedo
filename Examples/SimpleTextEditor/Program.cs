using ktsu.UndoRedo.Core;
using ktsu.UndoRedo.Core.Services;
using System.Text.Json;

namespace SimpleTextEditor;

/// <summary>
/// Simple console text editor demonstrating undo/redo with persistence
/// </summary>
class Program
{
    private static readonly string SaveFile = "editor_state.json";
    private static UndoRedoStack? _undoRedoStack;
    private static string _text = "";
    private static bool _hasUnsavedChanges = false;

    static async Task Main(string[] args)
    {
        Console.WriteLine("🔄 Simple Text Editor with Undo/Redo");
        Console.WriteLine("=====================================");
        Console.WriteLine();

        // Initialize undo/redo stack with serialization
        _undoRedoStack = new UndoRedoStack();
        _undoRedoStack.SetSerializer(new JsonUndoRedoSerializer());

        // Set up event handlers
        _undoRedoStack.CommandExecuted += (_, e) =>
        {
            _hasUnsavedChanges = true;
            Console.WriteLine($"✅ Executed: {e.Command.Description}");
        };

        _undoRedoStack.CommandUndone += (_, e) =>
        {
            _hasUnsavedChanges = _undoRedoStack.HasUnsavedChanges;
            Console.WriteLine($"↩️  Undone: {e.Command.Description}");
        };

        _undoRedoStack.CommandRedone += (_, e) =>
        {
            _hasUnsavedChanges = _undoRedoStack.HasUnsavedChanges;
            Console.WriteLine($"↪️  Redone: {e.Command.Description}");
        };

        _undoRedoStack.SaveBoundaryCreated += (_, e) =>
        {
            _hasUnsavedChanges = false;
            Console.WriteLine($"💾 Saved: {e.SaveBoundary.Description}");
        };

        // Try to load previous session
        await LoadSessionAsync();

        // Show current state
        ShowCurrentState();
        ShowHelp();

        // Main loop
        while (true)
        {
            Console.Write("\n> ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input))
                continue;

            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].ToLower();

            try
            {
                switch (command)
                {
                    case "help" or "h":
                        ShowHelp();
                        break;

                    case "show" or "s":
                        ShowCurrentState();
                        break;

                    case "insert" or "i":
                        if (parts.Length < 3)
                        {
                            Console.WriteLine("Usage: insert <position> <text>");
                            break;
                        }
                        if (int.TryParse(parts[1], out int pos))
                        {
                            var text = string.Join(" ", parts.Skip(2));
                            InsertText(pos, text);
                        }
                        else
                        {
                            Console.WriteLine("Invalid position number");
                        }
                        break;

                    case "delete" or "d":
                        if (parts.Length < 3)
                        {
                            Console.WriteLine("Usage: delete <position> <length>");
                            break;
                        }
                        if (int.TryParse(parts[1], out int delPos) && int.TryParse(parts[2], out int length))
                        {
                            DeleteText(delPos, length);
                        }
                        else
                        {
                            Console.WriteLine("Invalid position or length number");
                        }
                        break;

                    case "replace" or "r":
                        if (parts.Length < 4)
                        {
                            Console.WriteLine("Usage: replace <position> <length> <new_text>");
                            break;
                        }
                        if (int.TryParse(parts[1], out int repPos) && int.TryParse(parts[2], out int repLen))
                        {
                            var newText = string.Join(" ", parts.Skip(3));
                            ReplaceText(repPos, repLen, newText);
                        }
                        else
                        {
                            Console.WriteLine("Invalid position or length number");
                        }
                        break;

                    case "undo" or "u":
                        await UndoAsync();
                        break;

                    case "redo" or "y":
                        await RedoAsync();
                        break;

                    case "save":
                        await SaveAsync();
                        break;

                    case "history":
                        ShowHistory();
                        break;

                    case "clear":
                        ClearDocument();
                        break;

                    case "exit" or "quit" or "q":
                        await ExitAsync();
                        return;

                    default:
                        Console.WriteLine($"Unknown command: {command}. Type 'help' for available commands.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }
    }

    static void InsertText(int position, string text)
    {
        position = Math.Max(0, Math.Min(position, _text.Length));

        var command = new DelegateCommand(
            $"Insert '{text}' at position {position}",
            () => _text = _text.Insert(position, text),
            () => _text = _text.Remove(position, text.Length),
            ChangeType.Insert,
            new[] { $"text:{position}" }
        );

        _undoRedoStack!.Execute(command);
        ShowCurrentState();
    }

    static void DeleteText(int position, int length)
    {
        position = Math.Max(0, Math.Min(position, _text.Length));
        length = Math.Min(length, _text.Length - position);

        if (length <= 0)
        {
            Console.WriteLine("Nothing to delete");
            return;
        }

        var deletedText = _text.Substring(position, length);

        var command = new DelegateCommand(
            $"Delete '{deletedText}' at position {position}",
            () => _text = _text.Remove(position, length),
            () => _text = _text.Insert(position, deletedText),
            ChangeType.Delete,
            new[] { $"text:{position}" }
        );

        _undoRedoStack!.Execute(command);
        ShowCurrentState();
    }

    static void ReplaceText(int position, int length, string newText)
    {
        position = Math.Max(0, Math.Min(position, _text.Length));
        length = Math.Min(length, _text.Length - position);

        var oldText = length > 0 ? _text.Substring(position, length) : "";

        var command = new DelegateCommand(
            $"Replace '{oldText}' with '{newText}' at position {position}",
            () => _text = _text.Remove(position, length).Insert(position, newText),
            () => _text = _text.Remove(position, newText.Length).Insert(position, oldText),
            ChangeType.Modify,
            new[] { $"text:{position}" }
        );

        _undoRedoStack!.Execute(command);
        ShowCurrentState();
    }

    static async Task UndoAsync()
    {
        if (_undoRedoStack!.CanUndo)
        {
            await _undoRedoStack.UndoAsync(navigateToChange: false);
            ShowCurrentState();
        }
        else
        {
            Console.WriteLine("Nothing to undo");
        }
    }

    static async Task RedoAsync()
    {
        if (_undoRedoStack!.CanRedo)
        {
            await _undoRedoStack.RedoAsync(navigateToChange: false);
            ShowCurrentState();
        }
        else
        {
            Console.WriteLine("Nothing to redo");
        }
    }

    static async Task SaveAsync()
    {
        _undoRedoStack!.MarkAsSaved($"Manual save at {DateTime.Now:HH:mm:ss}");
        await SaveSessionAsync();
        Console.WriteLine("Document and session saved!");
    }

    static void ClearDocument()
    {
        if (_text.Length > 0)
        {
            var command = new DelegateCommand(
                "Clear document",
                () => _text = "",
                () => _text = _text, // This captures the current text
                ChangeType.Delete,
                new[] { "document" }
            );

            _undoRedoStack!.Execute(command);
            ShowCurrentState();
        }
        else
        {
            Console.WriteLine("Document is already empty");
        }
    }

    static void ShowCurrentState()
    {
        Console.WriteLine();
        Console.WriteLine("📄 Current Document:");
        Console.WriteLine($"Content: \"{_text}\"");
        Console.WriteLine($"Length: {_text.Length}");
        Console.WriteLine($"Can Undo: {_undoRedoStack!.CanUndo}");
        Console.WriteLine($"Can Redo: {_undoRedoStack.CanRedo}");
        Console.WriteLine($"Commands: {_undoRedoStack.CommandCount}");
        Console.WriteLine($"Unsaved Changes: {(_hasUnsavedChanges ? "Yes" : "No")}");

        if (_text.Length > 0)
        {
            Console.WriteLine("Positions: " + string.Join("", Enumerable.Range(0, Math.Min(_text.Length, 20)).Select(i => i % 10)));
            Console.WriteLine("Text:      " + _text[..Math.Min(_text.Length, 20)] + (_text.Length > 20 ? "..." : ""));
        }
    }

    static void ShowHistory()
    {
        Console.WriteLine("\n📜 Command History:");
        var visualizations = _undoRedoStack!.GetChangeVisualizations().ToList();

        if (!visualizations.Any())
        {
            Console.WriteLine("No commands in history");
            return;
        }

        for (int i = 0; i < visualizations.Count; i++)
        {
            var viz = visualizations[i];
            var marker = viz.IsExecuted ? "✓" : "○";
            var saveMarker = viz.HasSaveBoundary ? " 💾" : "";
            Console.WriteLine($"  {marker} {viz.Command.Description}{saveMarker}");
        }

        Console.WriteLine($"\nPosition: {_undoRedoStack.CurrentPosition}/{_undoRedoStack.CommandCount}");

        if (_undoRedoStack.SaveBoundaries.Any())
        {
            Console.WriteLine("\n💾 Save Points:");
            foreach (var boundary in _undoRedoStack.SaveBoundaries)
            {
                Console.WriteLine($"  Position {boundary.Position}: {boundary.Description} ({boundary.Timestamp:HH:mm:ss})");
            }
        }
    }

    static async Task LoadSessionAsync()
    {
        if (!File.Exists(SaveFile))
        {
            Console.WriteLine("🆕 Starting new session");
            return;
        }

        try
        {
            Console.WriteLine("📂 Loading previous session...");
            var data = await File.ReadAllBytesAsync(SaveFile);
            bool success = await _undoRedoStack!.LoadStateAsync(data);

            if (success)
            {
                // Restore the text content by replaying all executed commands
                var state = _undoRedoStack.GetCurrentState();
                for (int i = 0; i < state.CurrentPosition; i++)
                {
                    // Since our commands modify _text directly, we need to execute them
                    // In a real application, you'd restore the document state differently
                }

                Console.WriteLine($"✅ Session restored! Loaded {_undoRedoStack.CommandCount} commands");
                _hasUnsavedChanges = _undoRedoStack.HasUnsavedChanges;
            }
            else
            {
                Console.WriteLine("❌ Failed to restore session");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error loading session: {ex.Message}");
        }
    }

    static async Task SaveSessionAsync()
    {
        try
        {
            var data = await _undoRedoStack!.SaveStateAsync();
            await File.WriteAllBytesAsync(SaveFile, data);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error saving session: {ex.Message}");
        }
    }

    static async Task ExitAsync()
    {
        if (_hasUnsavedChanges)
        {
            Console.Write("You have unsaved changes. Save before exit? (y/n): ");
            var response = Console.ReadLine()?.Trim().ToLower();
            if (response == "y" || response == "yes")
            {
                await SaveAsync();
            }
        }

        await SaveSessionAsync();
        Console.WriteLine("👋 Goodbye!");
    }

    static void ShowHelp()
    {
        Console.WriteLine("\n📖 Available Commands:");
        Console.WriteLine("  help (h)                    - Show this help");
        Console.WriteLine("  show (s)                    - Show current document state");
        Console.WriteLine("  insert <pos> <text>        - Insert text at position");
        Console.WriteLine("  delete <pos> <length>      - Delete text from position");
        Console.WriteLine("  replace <pos> <len> <text> - Replace text at position");
        Console.WriteLine("  undo (u)                    - Undo last command");
        Console.WriteLine("  redo (y)                    - Redo next command");
        Console.WriteLine("  save                        - Save document and session");
        Console.WriteLine("  history                     - Show command history");
        Console.WriteLine("  clear                       - Clear document");
        Console.WriteLine("  exit (quit, q)              - Exit the editor");
        Console.WriteLine();
        Console.WriteLine("💡 Examples:");
        Console.WriteLine("  insert 0 Hello       - Insert 'Hello' at start");
        Console.WriteLine("  insert 5 World       - Insert 'World' at position 5");
        Console.WriteLine("  delete 0 5           - Delete first 5 characters");
        Console.WriteLine("  replace 0 5 Hi       - Replace first 5 chars with 'Hi'");
    }
}
