// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Test;

using ktsu.UndoRedo.Contracts;
using ktsu.UndoRedo.Core;
using ktsu.UndoRedo.Models;
using Microsoft.Extensions.DependencyInjection;

[TestClass]
public class ServiceCollectionExtensionsTests
{
#pragma warning disable CA1812 // Instantiated by the service provider
	private sealed class TestNavigationProvider : INavigationProvider
	{
		public Task<bool> NavigateToAsync(string context, CancellationToken cancellationToken = default) => Task.FromResult(true);

		public bool IsValidContext(string context) => true;
	}
#pragma warning restore CA1812

	[TestMethod]
	[DataRow(false, false, DisplayName = "AddUndoRedo + AddNavigationProvider")]
	[DataRow(false, true, DisplayName = "AddUndoRedo + AddSingletonNavigationProvider")]
	[DataRow(true, false, DisplayName = "AddSingletonUndoRedo + AddNavigationProvider")]
	[DataRow(true, true, DisplayName = "AddSingletonUndoRedo + AddSingletonNavigationProvider")]
	public void Registration_WithScopeValidation_ResolvesFromRootAndScope(bool singletonUndoRedo, bool singletonNavigation)
	{
		// Arrange
		ServiceCollection services = new();
		if (singletonUndoRedo)
		{
			services.AddSingletonUndoRedo();
		}
		else
		{
			services.AddUndoRedo();
		}

		if (singletonNavigation)
		{
			services.AddSingletonNavigationProvider<TestNavigationProvider>();
		}
		else
		{
			services.AddNavigationProvider<TestNavigationProvider>();
		}

		// Act: the same validation Host.CreateDefaultBuilder turns on in Development
		using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateOnBuild = true,
			ValidateScopes = true,
		});
		using IServiceScope scope = provider.CreateScope();

		// Assert
		Assert.IsNotNull(provider.GetRequiredService<IUndoRedoService>(), "The service should resolve from the root provider");
		Assert.IsNotNull(scope.ServiceProvider.GetRequiredService<IUndoRedoService>(), "The service should resolve from a scope");
	}
	[TestMethod]
	public void AddSingletonUndoRedo_WithBuilder_RegistersConfiguredSingleton()
	{
		// Arrange: the WPF setup from docs/dependency-injection.md
		ServiceCollection services = new();
		services.AddSingletonUndoRedo(options => options
			.WithMaxStackSize(42)
			.WithAutoMerge(false)
			.WithNavigation(true)
			.WithNavigationTimeout(TimeSpan.FromSeconds(3)));

		// Act
		using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateOnBuild = true,
			ValidateScopes = true,
		});
		using IServiceScope scope = provider.CreateScope();
		UndoRedoOptions options = provider.GetRequiredService<UndoRedoOptions>();

		// Assert
		Assert.AreEqual(42, options.MaxStackSize);
		Assert.IsFalse(options.AutoMergeCommands);
		Assert.IsTrue(options.EnableNavigation);
		Assert.AreEqual(TimeSpan.FromSeconds(3), options.DefaultNavigationTimeout);
		Assert.AreSame(
			provider.GetRequiredService<IUndoRedoService>(),
			scope.ServiceProvider.GetRequiredService<IUndoRedoService>(),
			"The builder overload should register the service as a singleton");
	}
}
