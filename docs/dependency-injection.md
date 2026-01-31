# Dependency Injection

This document explains how to configure and use the UndoRedo library with dependency injection containers.

## Overview

The UndoRedo library is designed with dependency injection as a first-class citizen. All services follow DI best practices:

-   **Interface-based**: All dependencies are abstractions
-   **Modular**: Services can be registered independently
-   **Configurable**: Flexible registration options
-   **Testable**: Easy to mock and test

## Supported DI Containers

The library uses `Microsoft.Extensions.DependencyInjection.Abstractions`, making it compatible with:

-   **Microsoft.Extensions.DependencyInjection** (built-in .NET DI)
-   **Autofac** (with Microsoft.Extensions.DependencyInjection bridge)
-   **Unity** (with Microsoft.Extensions.DependencyInjection bridge)
-   **Any container** that supports the Microsoft DI abstractions

## Basic Registration

### Default Configuration

The simplest way to register all services:

```csharp
using ktsu.UndoRedo;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Register all undo/redo services with default configuration
services.AddUndoRedo();

var serviceProvider = services.BuildServiceProvider();
```

This registers:

-   `IStackManager` → `StackManager` (Transient)
-   `ISaveBoundaryManager` → `SaveBoundaryManager` (Transient)
-   `ICommandMerger` → `CommandMerger` (Transient)
-   `IUndoRedoService` → `UndoRedoService` (Transient)
-   `UndoRedoOptions` → Default options (Singleton)

### Singleton Registration

For applications where you want a single shared undo/redo service:

```csharp
// Register as singletons instead of transient
services.AddSingletonUndoRedo();
```

This is useful for:

-   Desktop applications with a single document
-   Applications where undo/redo state should be shared
-   Performance-critical scenarios

## Configuration Options

### Fluent Builder API

Use the fluent builder for readable configuration:

```csharp
services.AddUndoRedo(options => options
    .WithMaxStackSize(1000)                    // Limit command stack size
    .WithAutoMerge(true)                       // Enable automatic command merging
    .WithNavigation(true)                      // Enable navigation features
    .WithNavigationTimeout(TimeSpan.FromSeconds(5))); // Set navigation timeout
```

### Options Object

Or configure using the options object directly:

```csharp
var undoRedoOptions = new UndoRedoOptions(
    MaxStackSize: 1000,
    AutoMergeCommands: true,
    EnableNavigation: true,
    DefaultNavigationTimeout: TimeSpan.FromSeconds(5));

services.AddUndoRedo(undoRedoOptions);
```

### Configuration from appsettings.json

You can also configure from JSON configuration:

```json
{
    "UndoRedo": {
        "MaxStackSize": 1000,
        "AutoMergeCommands": true,
        "EnableNavigation": true,
        "DefaultNavigationTimeout": "00:00:05"
    }
}
```

```csharp
// In your startup code
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var undoRedoOptions = configuration.GetSection("UndoRedo").Get<UndoRedoOptions>();
services.AddUndoRedo(undoRedoOptions);
```

## Navigation Provider Registration

### Scoped Navigation Provider

For most applications, register a scoped navigation provider:

```csharp
services.AddUndoRedo();
services.AddNavigationProvider<MyNavigationProvider>();
```

### Singleton Navigation Provider

For navigation providers that maintain no state:

```csharp
services.AddUndoRedo();
services.AddSingletonNavigationProvider<MyNavigationProvider>();
```

### Custom Navigation Provider

```csharp
public class WpfNavigationProvider : INavigationProvider
{
    private readonly IDispatcher _dispatcher;
    private readonly IViewManager _viewManager;

    public WpfNavigationProvider(IDispatcher dispatcher, IViewManager viewManager)
    {
        _dispatcher = dispatcher;
        _viewManager = viewManager;
    }

    public async Task NavigateToAsync(string context, CancellationToken cancellationToken)
    {
        await _dispatcher.InvokeAsync(() =>
        {
            // Parse context and navigate
            if (TryParseContext(context, out var location))
            {
                _viewManager.NavigateTo(location);
            }
        }, cancellationToken);
    }

    private bool TryParseContext(string context, out ViewLocation location)
    {
        // Implementation specific to your application
        // e.g., "file:line:column" or "document:section:element"
    }
}
```

## Service Lifetime Management

### Understanding Lifetimes

| Service                | Default Lifetime | Reason                                      |
| ---------------------- | ---------------- | ------------------------------------------- |
| `IUndoRedoService`     | Transient        | Each consumer gets own instance             |
| `IStackManager`        | Transient        | Isolated command stacks                     |
| `ISaveBoundaryManager` | Transient        | Isolated save state tracking                |
| `ICommandMerger`       | Transient        | Stateless, can be transient                 |
| `UndoRedoOptions`      | Singleton        | Configuration is shared                     |
| `INavigationProvider`  | Scoped           | UI-related, often scoped to request/session |

### Custom Lifetime Registration

Override default lifetimes as needed:

```csharp
// Register with custom lifetimes
services.AddTransient<IStackManager, StackManager>();
services.AddSingleton<ISaveBoundaryManager, SaveBoundaryManager>();
services.AddScoped<ICommandMerger, CommandMerger>();
services.AddScoped<IUndoRedoService, UndoRedoService>();
```

## Advanced Registration Scenarios

### Multiple Undo/Redo Services

For applications with multiple independent undo/redo contexts:

```csharp
// Register named services
services.AddTransient<IUndoRedoService>(provider =>
    new UndoRedoService(
        new StackManager(),
        new SaveBoundaryManager(),
        new CommandMerger(),
        provider.GetService<UndoRedoOptions>()));

// Or use keyed services (if using .NET 8+)
services.AddKeyedTransient<IUndoRedoService>("Document1", (provider, key) =>
    new UndoRedoService(
        new StackManager(),
        new SaveBoundaryManager(),
        new CommandMerger(),
        provider.GetService<UndoRedoOptions>()));
```

### Custom Service Implementations

Replace any service with your own implementation:

```csharp
services.AddUndoRedo();

// Replace the stack manager with a custom implementation
services.AddTransient<IStackManager, DatabaseStackManager>();

// Replace the command merger with an advanced version
services.AddTransient<ICommandMerger, AdvancedCommandMerger>();
```

### Conditional Registration

Register different implementations based on conditions:

```csharp
services.AddUndoRedo();

if (isDevelopment)
{
    // Use a debug-friendly stack manager in development
    services.AddTransient<IStackManager, DebuggingStackManager>();
}
else
{
    // Use a performance-optimized version in production
    services.AddTransient<IStackManager, OptimizedStackManager>();
}
```

## ASP.NET Core Integration

### Startup.cs (Legacy)

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Other service registrations...

        services.AddUndoRedo(options => options
            .WithMaxStackSize(500)
            .WithAutoMerge(true)
            .WithNavigation(false)); // Navigation typically not needed in web apps

        // Register other services...
    }
}
```

### Program.cs (Minimal API / .NET 6+)

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add undo/redo services
builder.Services.AddUndoRedo(options => options
    .WithMaxStackSize(500)
    .WithAutoMerge(true)
    .WithNavigation(false));

var app = builder.Build();
```

### Scoped per Request

For web applications where each request should have its own undo/redo context:

```csharp
services.AddScoped<IUndoRedoService, UndoRedoService>();
services.AddScoped<IStackManager, StackManager>();
services.AddScoped<ISaveBoundaryManager, SaveBoundaryManager>();
services.AddTransient<ICommandMerger, CommandMerger>(); // Stateless, can remain transient
```

## WPF/WinUI Integration

### App.xaml.cs

```csharp
public partial class App : Application
{
    public IServiceProvider ServiceProvider { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();

        // Register application services
        services.AddSingleton<IDispatcher, WpfDispatcher>();
        services.AddSingleton<IViewManager, ViewManager>();

        // Register undo/redo services
        services.AddSingletonUndoRedo(options => options
            .WithMaxStackSize(1000)
            .WithAutoMerge(true)
            .WithNavigation(true)
            .WithNavigationTimeout(TimeSpan.FromSeconds(3)));

        services.AddSingletonNavigationProvider<WpfNavigationProvider>();

        ServiceProvider = services.BuildServiceProvider();

        base.OnStartup(e);
    }
}
```

### ViewModel Integration

```csharp
public class DocumentViewModel : ViewModelBase
{
    private readonly IUndoRedoService _undoRedoService;

    public DocumentViewModel(IUndoRedoService undoRedoService)
    {
        _undoRedoService = undoRedoService;

        // Subscribe to events
        _undoRedoService.CommandExecuted += OnCommandExecuted;

        // Create commands
        UndoCommand = new RelayCommand(
            () => _undoRedoService.Undo(),
            () => _undoRedoService.CanUndo);

        RedoCommand = new RelayCommand(
            () => _undoRedoService.Redo(),
            () => _undoRedoService.CanRedo);
    }

    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
}
```

## Console Application Integration

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddUndoRedo();
                services.AddTransient<MyApplication>();
            })
            .Build();

        var app = host.Services.GetRequiredService<MyApplication>();
        await app.RunAsync();
    }
}

class MyApplication
{
    private readonly IUndoRedoService _undoRedoService;

    public MyApplication(IUndoRedoService undoRedoService)
    {
        _undoRedoService = undoRedoService;
    }

    public async Task RunAsync()
    {
        // Use undo/redo service...
    }
}
```

## Testing with Dependency Injection

### Unit Testing with Mocks

```csharp
[Test]
public void TestUndoRedoService()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddUndoRedo();

    // Replace services with mocks
    services.AddTransient<IStackManager>(_ => Mock.Of<IStackManager>());

    var serviceProvider = services.BuildServiceProvider();
    var undoRedoService = serviceProvider.GetRequiredService<IUndoRedoService>();

    // Act & Assert
    // Test the service...
}
```

### Integration Testing

```csharp
[Test]
public void TestCompleteWorkflow()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddUndoRedo(options => options.WithMaxStackSize(10));

    var serviceProvider = services.BuildServiceProvider();
    var undoRedoService = serviceProvider.GetRequiredService<IUndoRedoService>();

    // Act
    var command = new DelegateCommand("Test", () => { }, () => { });
    undoRedoService.Execute(command);

    // Assert
    Assert.That(undoRedoService.CanUndo, Is.True);
    Assert.That(undoRedoService.CommandCount, Is.EqualTo(1));
}
```

## Best Practices

### 1. Choose Appropriate Lifetimes

-   **Transient**: Default choice for most scenarios
-   **Scoped**: For web applications or when state should be isolated per scope
-   **Singleton**: Only when sharing state across the entire application

### 2. Configuration Management

-   Use the fluent builder API for readability
-   Store configuration in `appsettings.json` for flexibility
-   Validate configuration at startup

### 3. Service Organization

-   Group related registrations together
-   Use extension methods for complex registrations
-   Document lifetime choices and rationale

### 4. Testing Strategy

-   Use the real implementations for integration tests
-   Mock individual services for unit tests
-   Test service registration and resolution

### 5. Performance Considerations

-   Prefer transient for stateless services
-   Use singletons for expensive-to-create services
-   Monitor service resolution performance in production

## Troubleshooting

### Common Issues

1. **Service not registered**: Ensure `AddUndoRedo()` is called
2. **Circular dependencies**: Check service dependencies
3. **Wrong lifetime**: Verify lifetime choices for your scenario
4. **Missing navigation provider**: Register if navigation is enabled

### Debugging Registration

```csharp
// Verify service registration
var serviceProvider = services.BuildServiceProvider();

// Check if service is registered
var undoRedoService = serviceProvider.GetService<IUndoRedoService>();
if (undoRedoService == null)
{
    throw new InvalidOperationException("IUndoRedoService not registered");
}

// Verify implementation type
Console.WriteLine($"Implementation: {undoRedoService.GetType().Name}");
```
