// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Test;

using ktsu.UndoRedo.Contracts;
using ktsu.UndoRedo.Core;
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
}
