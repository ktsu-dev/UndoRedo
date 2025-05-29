# SOLID Principles Implementation

This document explains how the UndoRedo library implements each of the SOLID design principles with concrete examples from the codebase.

## Single Responsibility Principle (SRP)

_"A class should have only one reason to change."_

The library demonstrates SRP by separating concerns into focused services:

### Examples

#### ✅ Good: Specialized Services

**StackManager** - Only manages command storage:

```csharp
public sealed class StackManager : IStackManager
{
    // ONLY handles command storage and position tracking
    private readonly List<ICommand> _commands = [];
    private int _currentPosition = -1;

    public void AddCommand(ICommand command) { /* stack operations only */ }
    public ICommand? MovePrevious() { /* position management only */ }
    public void Clear() { /* storage management only */ }
}
```

**SaveBoundaryManager** - Only manages save boundaries:

```csharp
public sealed class SaveBoundaryManager : ISaveBoundaryManager
{
    // ONLY handles save boundary tracking
    private readonly List<SaveBoundary> _saveBoundaries = [];

    public SaveBoundary CreateSaveBoundary(int position, string? description) { /* save tracking only */ }
    public bool HasUnsavedChanges(int currentPosition) { /* save state only */ }
}
```

**CommandMerger** - Only handles command merging:

```csharp
public sealed class CommandMerger : ICommandMerger
{
    // ONLY handles command merging logic
    public bool CanMerge(ICommand first, ICommand second) { /* merging logic only */ }
    public ICommand Merge(ICommand first, ICommand second) { /* merging logic only */ }
}
```

#### ❌ Bad: Original Monolithic Design (Refactored)

The original `UndoRedoStack` violated SRP by handling:

-   Command storage
-   Save boundary management
-   Command merging
-   Navigation
-   Event handling
-   Configuration

This made it difficult to test individual concerns and led to complex, tightly coupled code.

### Benefits Achieved

1. **Easier Testing**: Each service can be unit tested in isolation
2. **Clearer Code**: Each class has a single, well-defined purpose
3. **Easier Maintenance**: Changes to one concern don't affect others
4. **Better Reusability**: Services can be composed differently

## Open/Closed Principle (OCP)

_"Software entities should be open for extension but closed for modification."_

The library allows extension without modifying existing code through interfaces and inheritance.

### Examples

#### ✅ Open for Extension

**Custom Commands via ICommand**:

```csharp
// Clients can extend without modifying library code
public class CustomTextEditCommand : BaseCommand
{
    private readonly string _oldText;
    private readonly string _newText;
    private readonly ITextEditor _editor;

    public CustomTextEditCommand(ITextEditor editor, string oldText, string newText)
        : base(ChangeType.Modify, [editor.DocumentPath])
    {
        _editor = editor;
        _oldText = oldText;
        _newText = newText;
    }

    public override void Execute() => _editor.SetText(_newText);
    public override void Undo() => _editor.SetText(_oldText);
}
```

**Custom Navigation Providers**:

```csharp
// Navigation can be extended without modifying the core library
public class CustomNavigationProvider : INavigationProvider
{
    public Task NavigateToAsync(string context, CancellationToken cancellationToken)
    {
        // Custom navigation logic for your application
        return MyCustomNavigationLogic(context, cancellationToken);
    }
}
```

**Service Replacement via DI**:

```csharp
// Replace any service implementation without modifying library code
services.AddTransient<IStackManager, MyCustomStackManager>();
services.AddTransient<ICommandMerger, MyAdvancedCommandMerger>();
```

#### ✅ Closed for Modification

The core interfaces and base classes don't need to change when extending:

-   `ICommand` interface remains stable
-   `BaseCommand` provides common functionality
-   Service interfaces define contracts that don't change

### Benefits Achieved

1. **Extensibility**: Add new command types without changing existing code
2. **Stability**: Core library code doesn't change when extending
3. **Backwards Compatibility**: Extensions don't break existing functionality

## Liskov Substitution Principle (LSP)

_"Objects of a superclass should be replaceable with objects of a subclass without breaking functionality."_

All implementations correctly implement their interface contracts.

### Examples

#### ✅ Proper Substitution

**Command Implementations**:

```csharp
// All these can be used wherever ICommand is expected
ICommand command1 = new DelegateCommand("Delete", () => Delete(), () => Restore());
ICommand command2 = new CompositeCommand("Multi-edit", [cmd1, cmd2, cmd3]);
ICommand command3 = new CustomCommand(/* parameters */);

// All work identically through the interface
undoRedoService.Execute(command1);  // ✅ Works
undoRedoService.Execute(command2);  // ✅ Works
undoRedoService.Execute(command3);  // ✅ Works
```

**Service Implementations**:

```csharp
// Any IStackManager implementation works the same way
IStackManager manager1 = new StackManager();
IStackManager manager2 = new CustomStackManager();

// Both satisfy the contract
var service1 = new UndoRedoService(manager1, boundaryManager, merger);  // ✅ Works
var service2 = new UndoRedoService(manager2, boundaryManager, merger);  // ✅ Works
```

#### Contract Guarantees

All implementations maintain the contracts defined by interfaces:

```csharp
public interface IStackManager
{
    bool CanUndo { get; }  // Must always reflect actual undo availability
    bool CanRedo { get; }  // Must always reflect actual redo availability

    // Must return the command that was undone, or null if none
    ICommand? MovePrevious();
}
```

### Benefits Achieved

1. **Interchangeability**: Any implementation can replace another
2. **Polymorphism**: Client code works with abstractions
3. **Testability**: Easy to create mock implementations

## Interface Segregation Principle (ISP)

_"Clients should not be forced to depend on interfaces they do not use."_

The library provides focused, minimal interfaces rather than large, monolithic ones.

### Examples

#### ✅ Focused Interfaces

**Specialized Service Interfaces**:

```csharp
// Clients only depend on what they need
public interface IStackManager
{
    // Only stack management methods - no save boundary or merging concerns
    IReadOnlyList<ICommand> Commands { get; }
    bool CanUndo { get; }
    bool CanRedo { get; }
    void AddCommand(ICommand command);
    ICommand? MovePrevious();
    ICommand? MoveNext();
}

public interface ISaveBoundaryManager
{
    // Only save boundary methods - no stack or merging concerns
    IReadOnlyList<SaveBoundary> SaveBoundaries { get; }
    bool HasUnsavedChanges(int currentPosition);
    SaveBoundary CreateSaveBoundary(int position, string? description);
}

public interface ICommandMerger
{
    // Only merging methods - no storage or save concerns
    bool CanMerge(ICommand first, ICommand second);
    ICommand Merge(ICommand first, ICommand second);
}
```

#### ✅ Optional Interfaces

**Navigation Provider** (optional dependency):

```csharp
public interface INavigationProvider
{
    // Completely optional - only implement if navigation is needed
    Task NavigateToAsync(string context, CancellationToken cancellationToken);
}
```

#### ❌ Bad: Fat Interface (Avoided)

We avoided creating a monolithic interface like:

```csharp
// BAD - This would violate ISP
public interface IUndoRedoEverything
{
    // Stack management
    void AddCommand(ICommand command);
    bool CanUndo { get; }

    // Save boundaries
    void MarkAsSaved();
    bool HasUnsavedChanges { get; }

    // Navigation
    Task NavigateToAsync(string context);

    // Command merging
    bool CanMerge(ICommand first, ICommand second);

    // Configuration
    void SetMaxStackSize(int size);

    // Events
    event EventHandler<CommandExecutedEventArgs> CommandExecuted;
    // ... many more methods
}
```

### Benefits Achieved

1. **Focused Dependencies**: Classes depend only on methods they use
2. **Easier Testing**: Smaller interfaces are easier to mock
3. **Reduced Coupling**: Changes to one interface don't affect unrelated clients
4. **Clearer Contracts**: Interface purpose is immediately clear

## Dependency Inversion Principle (DIP)

_"High-level modules should not depend on low-level modules. Both should depend on abstractions."_

The library inverts dependencies by depending on interfaces rather than concrete classes.

### Examples

#### ✅ Depending on Abstractions

**High-level UndoRedoService depends on abstractions**:

```csharp
public sealed class UndoRedoService : IUndoRedoService
{
    // Depends on interfaces, not concrete classes
    private readonly IStackManager _stackManager;           // ✅ Interface
    private readonly ISaveBoundaryManager _saveBoundaryManager;  // ✅ Interface
    private readonly ICommandMerger _commandMerger;         // ✅ Interface
    private INavigationProvider? _navigationProvider;      // ✅ Interface

    public UndoRedoService(
        IStackManager stackManager,                    // ✅ Injected abstraction
        ISaveBoundaryManager saveBoundaryManager,      // ✅ Injected abstraction
        ICommandMerger commandMerger,                  // ✅ Injected abstraction
        UndoRedoOptions? options = null,
        INavigationProvider? navigationProvider = null) // ✅ Optional abstraction
    {
        // All dependencies are abstractions
    }
}
```

**Dependency Injection Registration**:

```csharp
// High-level policy: how services are wired together
services.AddTransient<IStackManager, StackManager>();
services.AddTransient<ISaveBoundaryManager, SaveBoundaryManager>();
services.AddTransient<ICommandMerger, CommandMerger>();
services.AddTransient<IUndoRedoService, UndoRedoService>();

// Optional navigation provider
services.AddTransient<INavigationProvider, MyNavigationProvider>();
```

#### ✅ Abstractions Don't Depend on Details

**Interfaces are pure contracts**:

```csharp
public interface IStackManager
{
    // No dependency on concrete implementation details
    // No knowledge of how storage is implemented
    // No coupling to specific collection types
    IReadOnlyList<ICommand> Commands { get; }
    void AddCommand(ICommand command);
}
```

#### ❌ Bad: Direct Dependencies (Avoided)

We avoided coupling like this:

```csharp
// BAD - Direct dependency on concrete class
public class UndoRedoService
{
    private readonly StackManager _stackManager;  // ❌ Concrete dependency

    public UndoRedoService()
    {
        _stackManager = new StackManager();  // ❌ Direct instantiation
    }
}
```

### Benefits Achieved

1. **Testability**: Easy to inject mocks and test doubles
2. **Flexibility**: Can swap implementations without changing dependent code
3. **Loose Coupling**: High-level code doesn't depend on implementation details
4. **Configuration**: Runtime composition via dependency injection

## SOLID Benefits Summary

| Principle | Benefit                        | How We Achieve It                              |
| --------- | ------------------------------ | ---------------------------------------------- |
| **SRP**   | Single reason to change        | Focused services with clear responsibilities   |
| **OCP**   | Extension without modification | Interface-based extension points               |
| **LSP**   | Reliable substitution          | Proper interface contracts and implementations |
| **ISP**   | Focused dependencies           | Small, specific interfaces                     |
| **DIP**   | Loose coupling                 | Dependency injection with abstractions         |

## Testing Benefits

The SOLID design makes the library highly testable:

```csharp
[Test]
public void Execute_WithAutoMerge_MergesCommands()
{
    // Easy to mock dependencies due to DIP
    var mockStackManager = new Mock<IStackManager>();
    var mockSaveBoundaryManager = new Mock<ISaveBoundaryManager>();
    var mockCommandMerger = new Mock<ICommandMerger>();

    // SRP means we can test just the service logic
    var service = new UndoRedoService(
        mockStackManager.Object,
        mockSaveBoundaryManager.Object,
        mockCommandMerger.Object,
        new UndoRedoOptions(AutoMergeCommands: true));

    // Test specific behavior in isolation
    // ...
}
```

## Real-World Benefits

1. **Maintainability**: Changes to stack management don't affect save boundaries
2. **Extensibility**: Add new command types without changing core library
3. **Testability**: Each service can be tested independently
4. **Flexibility**: Swap implementations based on requirements
5. **Clarity**: Each interface has a clear, single purpose
