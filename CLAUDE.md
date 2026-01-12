# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ktsu.UndoRedo is a comprehensive .NET library implementing undo/redo functionality using the command pattern. It provides advanced features including save boundaries, change visualization, navigation integration, command merging, serialization, and dependency injection support.

**Key Capabilities:**
- Command pattern implementation with execute/undo operations
- Save boundary tracking for identifying unsaved work
- Navigation integration to jump to where changes were made
- Intelligent command merging (e.g., consecutive typing operations)
- Composite commands for grouping atomic operations
- Event system for UI synchronization
- Async/await support for navigation operations
- JSON serialization for persisting undo/redo stacks

## Build and Test Commands

```bash
# Restore dependencies (may show "Unable to find project" warning with ktsu.Sdk - can be ignored if build succeeds)
dotnet restore

# Build the solution
dotnet build

# Run all tests
dotnet test --nologo

# Run tests with detailed output
dotnet test --verbosity normal --logger "console;verbosity=detailed" | cat

# Alternative: restore individual projects directly
dotnet restore UndoRedo.Core/UndoRedo.Core.csproj && dotnet restore UndoRedo.Test/UndoRedo.Test.csproj
```

## Project Structure

**Solution:** `UndoRedo.sln` contains two projects:
- `UndoRedo.Core` - Main library with multi-targeting (net5.0-net9.0, netstandard2.0/2.1)
- `UndoRedo.Test` - MSTest test project (net9.0)

**Build System:**
- Uses custom `ktsu.Sdk` MSBuild SDK (version specified in `global.json`)
- Centralized package management via `Directory.Packages.props`
- Target frameworks: net9.0, net8.0, net7.0, net6.0, net5.0, netstandard2.0, netstandard2.1

## Core Architecture

The library follows a **contracts, models, and services** architecture with dependency injection support:

### Contracts (Interfaces)
Located in `UndoRedo.Core/Contracts/`:
- `ICommand` - Core interface for undoable commands with Execute/Undo, metadata, and merge support
- `IUndoRedoService` - Main service interface for undo/redo operations
- `IStackManager` - Manages the command stack and position tracking
- `ISaveBoundaryManager` - Tracks save points in the undo stack
- `ICommandMerger` - Handles merging of compatible commands
- `INavigationProvider` - Optional interface for navigating to change locations
- `IUndoRedoSerializer` - Interface for serializing/deserializing undo stacks

### Models
Located in `UndoRedo.Core/Models/`:
- `ChangeMetadata` - Rich metadata about changes (type, affected items, timestamp)
- `ChangeVisualization` - Data for displaying change history in UI
- `SaveBoundary` - Represents a save point with position and description
- `UndoRedoOptions` - Configuration options (max stack size, auto-merge, navigation settings)
- `UndoRedoStackState` - Serializable state for persistence

### Services
Located in `UndoRedo.Core/Services/`:
- `UndoRedoService` - Main service orchestrating undo/redo operations
- `StackManager` - Concrete implementation of command stack management
- `SaveBoundaryManager` - Manages save boundary creation and cleanup
- `CommandMerger` - Default command merging logic
- `JsonUndoRedoSerializer` - JSON-based serialization implementation

### Command Implementations
Located in `UndoRedo.Core/`:
- `BaseCommand` - Abstract base class with common functionality
- `DelegateCommand` - Simple command using delegates for execute/undo
- `CompositeCommand` - Groups multiple commands into atomic operations

### Dependency Injection
- `ServiceCollectionExtensions` provides extension methods for registering services
- Use `AddUndoRedo()` for transient services or `AddSingletonUndoRedo()` for singleton pattern
- Navigation providers can be registered with `AddNavigationProvider<T>()` or `AddSingletonNavigationProvider<T>()`

## Key Design Patterns

**Command Pattern:** All undoable operations implement `ICommand` with Execute/Undo methods.

**Service Architecture:** Core logic is split into focused services (stack management, save boundaries, merging) coordinated by `UndoRedoService`.

**Command Merging:** Commands can implement `CanMergeWith()` and `MergeWith()` to combine consecutive related operations (like typing characters) into single undo units.

**Navigation Context:** Commands can include navigation context strings (e.g., "file:line:col") that integrate with `INavigationProvider` to automatically navigate during undo/redo.

**Save Boundaries:** Track which commands represent saved states, enabling features like "undo to last save" and identifying unsaved changes.

**Event System:** Comprehensive events (`CommandExecuted`, `CommandUndone`, `CommandRedone`, `SaveBoundaryCreated`) for UI synchronization.

## Code Quality Guidelines

- Follow SOLID and DRY principles
- Validate parameters with `ArgumentNullException.ThrowIfNull()` to prevent null reference issues
- Use dependency injection throughout
- Apply modern C# pattern matching and syntax
- Use specific exception types rather than generic catch blocks
- For warnings, prefer explicit suppression attributes with justifications over global suppressions
- Only use preprocessor defines for suppressions as a last resort, with comment justifications
- Make suppressions as targeted as possible

## Package Management

- All package versions are managed centrally in `Directory.Packages.props`
- When updating packages, sort and deduplicate entries, keeping only the highest version
- Key dependencies: Microsoft.Extensions.DependencyInjection (for DI support)

## Testing

- Tests use MSTest framework with modern Testing.Platform
- Test classes: `UndoRedoStackTests.cs`, `CompositeCommandTests.cs`, `SerializationTests.cs`
- Tests verify core functionality, command merging, save boundaries, serialization, and edge cases

## Git Workflow

After making commits, always push changes: `git push origin main`
