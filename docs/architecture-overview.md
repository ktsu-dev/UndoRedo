# Architecture Overview

## Introduction

The UndoRedo library is built using modern .NET design patterns and principles, specifically implementing the Command pattern with a clean, modular architecture that follows SOLID principles. The library has been refactored from a monolithic design to a dependency injection-based architecture for better testability, maintainability, and extensibility.

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        Client Application                   │
│                                                             │
│  ┌─────────────────┐    ┌──────────────────────────────────┐│
│  │ Navigation      │    │ Custom Commands                  ││
│  │ Provider        │    │ (BaseCommand, DelegateCommand,   ││
│  │ (Optional)      │    │  CompositeCommand)              ││
│  └─────────────────┘    └──────────────────────────────────┘│
└─────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────┐
│                    UndoRedo Core Library                    │
│                                                             │
│  ┌─────────────────────────────────────────────────────────┐│
│  │                 Main Service Layer                     ││
│  │  ┌─────────────────────────────────────────────────────┤│
│  │  │           IUndoRedoService                         ││
│  │  │         (UndoRedoService)                          ││
│  │  └─────────────────────────────────────────────────────┘│
│  └─────────────────────────────────────────────────────────┘│
│                                │                             │
│                                ▼                             │
│  ┌─────────────────────────────────────────────────────────┐│
│  │              Specialized Services                      ││
│  │                                                        ││
│  │  ┌───────────────┐ ┌─────────────────┐ ┌─────────────┐ ││
│  │  │ IStackManager │ │ISaveBoundary    │ │ICommand     │ ││
│  │  │ (StackManager)│ │Manager          │ │Merger       │ ││
│  │  │               │ │(SaveBoundary    │ │(Command     │ ││
│  │  │               │ │Manager)         │ │Merger)      │ ││
│  │  └───────────────┘ └─────────────────┘ └─────────────┘ ││
│  └─────────────────────────────────────────────────────────┘│
│                                │                             │
│                                ▼                             │
│  ┌─────────────────────────────────────────────────────────┐│
│  │                   Data Models                          ││
│  │                                                        ││
│  │  ┌─────────────────┐ ┌─────────────────┐ ┌─────────────┐ ││
│  │  │ UndoRedoOptions │ │ ChangeMetadata  │ │SaveBoundary │ ││
│  │  │                 │ │                 │ │             │ ││
│  │  └─────────────────┘ └─────────────────┘ └─────────────┘ ││
│  │                                                        ││
│  │  ┌─────────────────┐ ┌─────────────────────────────────┐ ││
│  │  │ Change          │ │ Event Args Classes              │ ││
│  │  │ Visualization   │ │ (CommandExecutedEventArgs, etc.)│ ││
│  │  └─────────────────┘ └─────────────────────────────────┘ ││
│  └─────────────────────────────────────────────────────────┘│
│                                │                             │
│                                ▼                             │
│  ┌─────────────────────────────────────────────────────────┐│
│  │                 Contract Layer                          ││
│  │                                                        ││
│  │  ┌─────────────────┐ ┌─────────────────┐ ┌─────────────┐ ││
│  │  │ ICommand        │ │ INavigation     │ │Service      │ ││
│  │  │                 │ │ Provider        │ │Interfaces   │ ││
│  │  └─────────────────┘ └─────────────────┘ └─────────────┘ ││
│  └─────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────┐
│              Microsoft.Extensions.DependencyInjection      │
└─────────────────────────────────────────────────────────────┘
```

## Core Components

### 1. Main Service (`IUndoRedoService`)

The central orchestrator that coordinates all undo/redo operations. It:

-   Manages the overall workflow
-   Coordinates between specialized services
-   Provides the public API for clients
-   Handles events and notifications
-   Manages async operations and navigation

### 2. Specialized Services

#### Stack Manager (`IStackManager`)

-   **Responsibility**: Command storage and position tracking
-   **Key Operations**: Add/remove commands, position management, stack trimming
-   **Isolation**: Pure data structure management without business logic

#### Save Boundary Manager (`ISaveBoundaryManager`)

-   **Responsibility**: Track save states and unsaved changes
-   **Key Operations**: Create boundaries, detect unsaved changes, position adjustment
-   **Isolation**: Focused solely on save state management

#### Command Merger (`ICommandMerger`)

-   **Responsibility**: Merge compatible commands for optimization
-   **Key Operations**: Compatibility checking, command merging
-   **Isolation**: Pure merging logic without side effects

### 3. Models and Data

#### Configuration Models

-   `UndoRedoOptions`: Central configuration with builder pattern support
-   `UndoRedoOptionsBuilder`: Fluent configuration API

#### Data Models

-   `ChangeMetadata`: Describes the nature and scope of changes
-   `SaveBoundary`: Represents save points in the command history
-   `ChangeVisualization`: UI-friendly representation of changes

#### Event Models

-   Various event argument classes for different operations
-   Type-safe event data with relevant context

### 4. Command Hierarchy

```
ICommand (Interface)
├── BaseCommand (Abstract base with common functionality)
│   ├── DelegateCommand (Simple delegate-based commands)
│   └── CompositeCommand (Multi-command operations)
└── [Custom implementations by clients]
```

## Design Patterns Used

### 1. Command Pattern

-   **Purpose**: Encapsulate operations as objects for undo/redo
-   **Implementation**: `ICommand` interface with Execute/Undo methods
-   **Benefits**: Decouples invoker from receiver, enables queuing and logging

### 2. Strategy Pattern

-   **Purpose**: Pluggable navigation providers
-   **Implementation**: `INavigationProvider` interface
-   **Benefits**: Flexible navigation integration without coupling

### 3. Dependency Injection

-   **Purpose**: Loose coupling and testability
-   **Implementation**: Microsoft.Extensions.DependencyInjection
-   **Benefits**: Easy testing, configuration, and extension

### 4. Builder Pattern

-   **Purpose**: Fluent configuration API
-   **Implementation**: `UndoRedoOptionsBuilder`
-   **Benefits**: Readable configuration with sensible defaults

### 5. Composite Pattern

-   **Purpose**: Treat single and composite commands uniformly
-   **Implementation**: `CompositeCommand` implements `ICommand`
-   **Benefits**: Hierarchical command structures, batch operations

## Key Design Principles

### Single Responsibility Principle (SRP)

Each service has one clear responsibility:

-   `StackManager`: Command storage only
-   `SaveBoundaryManager`: Save state tracking only
-   `CommandMerger`: Command merging logic only

### Open/Closed Principle (OCP)

-   Open for extension through interfaces
-   Closed for modification via dependency injection
-   Custom commands via `ICommand` implementation

### Dependency Inversion Principle (DIP)

-   High-level modules depend on abstractions
-   Concrete implementations injected at runtime
-   No direct dependencies on concrete classes

### Interface Segregation Principle (ISP)

-   Focused, minimal interfaces
-   Clients depend only on methods they use
-   No fat interfaces forcing unnecessary dependencies

## Threading and Concurrency

The library is designed to be thread-safe when used properly:

### Thread Safety Approach

-   **Single Writer**: Commands should be executed from a single thread
-   **Immutable Data**: Models are immutable records where possible
-   **Event Safety**: Events are raised synchronously to maintain order
-   **Navigation Async**: Only navigation operations are truly async

### Usage Guidelines

-   Use from UI thread for consistency
-   Async operations use `ConfigureAwait(false)`
-   Cancellation tokens supported for long-running navigation

## Performance Considerations

### Memory Management

-   Command stack size limits prevent memory leaks
-   Weak references in navigation providers
-   Efficient collection types (`List<T>`, `ReadOnlyList<T>`)

### Execution Performance

-   O(1) undo/redo operations
-   O(n) stack trimming (configurable frequency)
-   Lazy evaluation where possible

### Optimization Features

-   Command merging reduces stack size
-   Configurable stack size limits
-   Minimal allocations in hot paths

## Extension Points

The architecture provides several extension points:

1. **Custom Commands**: Implement `ICommand` or extend `BaseCommand`
2. **Navigation Providers**: Implement `INavigationProvider`
3. **Service Replacement**: Replace any service implementation via DI
4. **Configuration**: Extensive options via `UndoRedoOptions`
5. **Event Handling**: Subscribe to operation events

## Migration and Versioning

The library is designed for forward compatibility:

-   Interface-based design allows implementation changes
-   Semantic versioning for public API changes
-   Migration guides for breaking changes
-   Backward compatibility within major versions
