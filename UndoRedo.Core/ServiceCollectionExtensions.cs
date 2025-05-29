// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

using ktsu.UndoRedo.Core.Contracts;
using ktsu.UndoRedo.Core.Models;
using ktsu.UndoRedo.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ktsu.UndoRedo.Core;

/// <summary>
/// Extension methods for registering undo/redo services with dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Adds undo/redo services to the service collection
	/// </summary>
	/// <param name="services">The service collection</param>
	/// <param name="options">Configuration options</param>
	/// <returns>The service collection for chaining</returns>
	public static IServiceCollection AddUndoRedo(this IServiceCollection services, UndoRedoOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register configuration
		services.TryAddSingleton(options ?? UndoRedoOptions.Default);

		// Register core services
		services.TryAddTransient<IStackManager, StackManager>();
		services.TryAddTransient<ISaveBoundaryManager, SaveBoundaryManager>();
		services.TryAddTransient<ICommandMerger, CommandMerger>();

		// Register main service
		services.TryAddTransient<IUndoRedoService, UndoRedoService>();

		return services;
	}

	/// <summary>
	/// Adds undo/redo services to the service collection with custom configuration
	/// </summary>
	/// <param name="services">The service collection</param>
	/// <param name="configureOptions">Configuration action</param>
	/// <returns>The service collection for chaining</returns>
	public static IServiceCollection AddUndoRedo(this IServiceCollection services, Action<UndoRedoOptionsBuilder> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		var builder = new UndoRedoOptionsBuilder();
		configureOptions(builder);
		var options = builder.Build();

		return services.AddUndoRedo(options);
	}

	/// <summary>
	/// Adds a singleton undo/redo service to the service collection
	/// </summary>
	/// <param name="services">The service collection</param>
	/// <param name="options">Configuration options</param>
	/// <returns>The service collection for chaining</returns>
	public static IServiceCollection AddSingletonUndoRedo(this IServiceCollection services, UndoRedoOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register configuration
		services.TryAddSingleton(options ?? UndoRedoOptions.Default);

		// Register core services as singletons
		services.TryAddSingleton<IStackManager, StackManager>();
		services.TryAddSingleton<ISaveBoundaryManager, SaveBoundaryManager>();
		services.TryAddSingleton<ICommandMerger, CommandMerger>();

		// Register main service as singleton
		services.TryAddSingleton<IUndoRedoService, UndoRedoService>();

		return services;
	}

	/// <summary>
	/// Adds a scoped navigation provider to the service collection
	/// </summary>
	/// <typeparam name="TNavigationProvider">The navigation provider type</typeparam>
	/// <param name="services">The service collection</param>
	/// <returns>The service collection for chaining</returns>
	public static IServiceCollection AddNavigationProvider<TNavigationProvider>(this IServiceCollection services)
		where TNavigationProvider : class, INavigationProvider
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddScoped<INavigationProvider, TNavigationProvider>();
		return services;
	}

	/// <summary>
	/// Adds a singleton navigation provider to the service collection
	/// </summary>
	/// <typeparam name="TNavigationProvider">The navigation provider type</typeparam>
	/// <param name="services">The service collection</param>
	/// <returns>The service collection for chaining</returns>
	public static IServiceCollection AddSingletonNavigationProvider<TNavigationProvider>(this IServiceCollection services)
		where TNavigationProvider : class, INavigationProvider
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<INavigationProvider, TNavigationProvider>();
		return services;
	}
}

/// <summary>
/// Builder for undo/redo options
/// </summary>
public sealed class UndoRedoOptionsBuilder
{
	private int _maxStackSize = 1000;
	private bool _autoMergeCommands = true;
	private bool _enableNavigation = true;
	private TimeSpan _defaultNavigationTimeout = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Sets the maximum stack size
	/// </summary>
	/// <param name="maxStackSize">Maximum number of commands to keep</param>
	/// <returns>The builder for chaining</returns>
	public UndoRedoOptionsBuilder WithMaxStackSize(int maxStackSize)
	{
		_maxStackSize = maxStackSize;
		return this;
	}

	/// <summary>
	/// Enables or disables automatic command merging
	/// </summary>
	/// <param name="autoMerge">Whether to auto-merge commands</param>
	/// <returns>The builder for chaining</returns>
	public UndoRedoOptionsBuilder WithAutoMerge(bool autoMerge)
	{
		_autoMergeCommands = autoMerge;
		return this;
	}

	/// <summary>
	/// Enables or disables navigation
	/// </summary>
	/// <param name="enableNavigation">Whether to enable navigation</param>
	/// <returns>The builder for chaining</returns>
	public UndoRedoOptionsBuilder WithNavigation(bool enableNavigation)
	{
		_enableNavigation = enableNavigation;
		return this;
	}

	/// <summary>
	/// Sets the default navigation timeout
	/// </summary>
	/// <param name="timeout">Navigation timeout</param>
	/// <returns>The builder for chaining</returns>
	public UndoRedoOptionsBuilder WithNavigationTimeout(TimeSpan timeout)
	{
		_defaultNavigationTimeout = timeout;
		return this;
	}

	/// <summary>
	/// Builds the options
	/// </summary>
	/// <returns>The configured options</returns>
	internal UndoRedoOptions Build() => new(_maxStackSize, _autoMergeCommands, _enableNavigation, _defaultNavigationTimeout);
}
