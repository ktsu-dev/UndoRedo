# UndoRedo Library Documentation

Welcome to the comprehensive documentation for the UndoRedo library - a modern .NET library implementing the Command pattern with undo/redo functionality, save boundaries, and navigation integration.

## Overview

The UndoRedo library provides a robust, extensible undo/redo system designed with SOLID principles and dependency injection. It has evolved from a monolithic design to a modular, testable architecture that supports:

-   **Command Pattern**: Full implementation with Execute/Undo operations
-   **Save Boundaries**: Track save states and unsaved changes
-   **Navigation Integration**: Optional navigation to change locations
-   **Command Merging**: Automatic optimization of command stack
-   **Event System**: Rich events for UI integration
-   **Async Support**: Full async/await support for navigation
-   **Dependency Injection**: First-class DI support with flexible configuration

## Quick Start

```csharp
// 1. Register services
services.AddUndoRedo();

// 2. Inject and use
public class MyService
{
    private readonly IUndoRedoService _undoRedo;

    public MyService(IUndoRedoService undoRedo)
    {
        _undoRedo = undoRedo;
    }

    public void ChangeText(TextBox textBox, string newText)
    {
        var command = new DelegateCommand(
            "Change text",
            () => textBox.Text = newText,
            () => textBox.Text = textBox.Text, // captured automatically
            ChangeType.Modify,
            new[] { textBox.Name });

        _undoRedo.Execute(command);
    }
}
```

## Documentation Structure

### 📖 Getting Started

-   **[Getting Started](getting-started.md)** - Installation, setup, and basic usage examples
-   **[Best Practices](best-practices.md)** - Recommended patterns and common pitfalls to avoid

### 🏗️ Architecture & Design

-   **[Architecture Overview](architecture-overview.md)** - High-level architecture and design principles
-   **[SOLID Principles](solid-principles.md)** - How the library implements SOLID design principles
-   **[Dependency Injection](dependency-injection.md)** - DI setup, configuration, and advanced scenarios

### 📋 API Reference

-   **[Contracts](contracts.md)** - Interface definitions and their responsibilities
-   **[Models](models.md)** - Data models and their purposes
-   **[Services](services.md)** - Service implementations and their behaviors
-   **[Commands](commands.md)** - Command types and creation patterns

### 🔧 Advanced Topics

-   **[Advanced Usage](advanced-usage.md)** - Complex scenarios and customization
-   **[Navigation Integration](navigation-integration.md)** - UI navigation setup and patterns
-   **[Testing Guide](testing-guide.md)** - Testing strategies and examples
-   **[Performance Guide](performance-guide.md)** - Optimization and performance considerations

### 📚 Examples & Tutorials

-   **[Examples](examples.md)** - Complete working examples
-   **[Migration Guide](migration-guide.md)** - Migrating from older versions
-   **[Troubleshooting](troubleshooting.md)** - Common issues and solutions

## Key Features

### 🎯 Command Pattern Implementation

```csharp
public class TextEditCommand : BaseCommand
{
    public override string Description => "Edit text";
    public override void Execute() { /* implementation */ }
    public override void Undo() { /* implementation */ }
}
```

### 💾 Save Boundary Tracking

```csharp
// Mark current state as saved
undoRedoService.MarkAsSaved("Document saved");

// Check for unsaved changes
bool hasChanges = undoRedoService.HasUnsavedChanges;

// Undo to last save
await undoRedoService.UndoToSaveBoundaryAsync(lastSave);
```

### 🔄 Automatic Command Merging

```csharp
public override bool CanMergeWith(ICommand other)
{
    return other is TextEditCommand edit &&
           edit.Position == this.Position + this.Text.Length &&
           edit.Timestamp - this.Timestamp < TimeSpan.FromSeconds(2);
}
```

### 🧭 Navigation Integration

```csharp
// Navigate to change location during undo/redo
await undoRedoService.UndoAsync(navigateToChange: true);

public class MyNavigationProvider : INavigationProvider
{
    public async Task NavigateToAsync(string context, CancellationToken cancellationToken)
    {
        // Custom navigation logic
    }
}
```

### 📡 Event-Driven Architecture

```csharp
undoRedoService.CommandExecuted += (s, e) => UpdateUI();
undoRedoService.CommandUndone += (s, e) => ShowUndoNotification(e.Command);
undoRedoService.SaveBoundaryCreated += (s, e) => UpdateSaveStatus();
```

## Architecture Highlights

### Modular Design

The library is split into focused services following single responsibility principle:

-   **IUndoRedoService**: Main orchestration service
-   **IStackManager**: Command storage and navigation
-   **ISaveBoundaryManager**: Save state tracking
-   **ICommandMerger**: Command optimization

### Dependency Injection First

```csharp
// Simple registration
services.AddUndoRedo();

// Advanced configuration
services.AddUndoRedo(options => options
    .WithMaxStackSize(1000)
    .WithAutoMerge(true)
    .WithNavigation(true));
```

### SOLID Compliance

-   **Single Responsibility**: Each service has one clear purpose
-   **Open/Closed**: Extensible through interfaces
-   **Liskov Substitution**: Proper interface contracts
-   **Interface Segregation**: Focused, minimal interfaces
-   **Dependency Inversion**: Services depend on abstractions

## Use Cases

### Desktop Applications

-   Document editors with undo/redo functionality
-   Image editing applications
-   CAD/design tools
-   IDE extensions

### Web Applications

-   Rich text editors
-   Form builders
-   Canvas-based applications
-   Collaborative editing tools

### Games

-   Level editors
-   Turn-based strategy games
-   Puzzle games with move history

## Technology Stack

-   **.NET Standard 2.0+** - Broad compatibility
-   **Microsoft.Extensions.DependencyInjection.Abstractions** - DI support
-   **C# 11+** - Modern language features
-   **Records** - Immutable data models
-   **Nullable Reference Types** - Enhanced null safety

## Performance Characteristics

-   **Memory Efficient**: Configurable stack size limits
-   **Command Merging**: Automatic optimization
-   **Weak References**: Optional for large objects
-   **Async Support**: Non-blocking navigation operations

## Thread Safety

The library is designed for single-threaded use (typically UI thread). For multi-threaded scenarios:

-   Use appropriate synchronization
-   Consider scoped services per thread
-   Implement thread-safe command implementations

## Extensibility Points

### Custom Commands

```csharp
public class MyCustomCommand : BaseCommand
{
    // Implement Execute/Undo logic
}
```

### Custom Services

```csharp
public class MyStackManager : IStackManager
{
    // Custom storage implementation
}

services.AddTransient<IStackManager, MyStackManager>();
```

### Custom Navigation

```csharp
public class MyNavigationProvider : INavigationProvider
{
    // Application-specific navigation
}
```

## Community & Support

-   **GitHub**: [ktsu-dev/UndoRedo](https://github.com/ktsu-dev/UndoRedo)
-   **Issues**: Report bugs and feature requests
-   **Discussions**: Community support and questions
-   **Wiki**: Additional examples and community contributions

## License

This library is released under the MIT License. See the LICENSE file for details.

## Contributing

We welcome contributions! Please see the CONTRIBUTING.md file for guidelines on:

-   Code style and standards
-   Testing requirements
-   Pull request process
-   Issue reporting

## Version History

-   **v1.0**: Initial release with monolithic design
-   **v2.0**: Refactored to SOLID architecture with DI support
-   **Current**: Enhanced documentation and examples

---

## Next Steps

1. **New to the library?** Start with [Getting Started](getting-started.md)
2. **Migrating from v1.x?** Check the [Migration Guide](migration-guide.md)
3. **Want to understand the design?** Read [Architecture Overview](architecture-overview.md)
4. **Need examples?** Browse [Examples](examples.md)
5. **Having issues?** Check [Troubleshooting](troubleshooting.md)

Happy coding! 🚀
