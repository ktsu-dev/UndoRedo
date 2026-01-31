// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.UndoRedo.Models;

/// <summary>
/// Configuration options for the undo/redo system
/// </summary>
/// <param name="MaxStackSize">Maximum number of commands to keep (0 for unlimited)</param>
/// <param name="AutoMergeCommands">Whether to automatically merge compatible commands</param>
/// <param name="EnableNavigation">Whether navigation is enabled by default</param>
/// <param name="DefaultNavigationTimeout">Default timeout for navigation operations</param>
public record UndoRedoOptions(
	int MaxStackSize = 1000,
	bool AutoMergeCommands = true,
	bool EnableNavigation = true,
	TimeSpan DefaultNavigationTimeout = default
)
{
	/// <summary>
	/// Default options instance
	/// </summary>
	public static readonly UndoRedoOptions Default = new();

	/// <summary>
	/// Creates options with custom values
	/// </summary>
	/// <param name="maxStackSize">Maximum stack size</param>
	/// <param name="autoMerge">Enable auto-merge</param>
	/// <param name="enableNavigation">Enable navigation</param>
	/// <param name="navigationTimeout">Navigation timeout</param>
	/// <returns>New options instance</returns>
	public static UndoRedoOptions Create(
		int maxStackSize = 1000,
		bool autoMerge = true,
		bool enableNavigation = true,
		TimeSpan navigationTimeout = default) =>
		new(maxStackSize, autoMerge, enableNavigation, navigationTimeout == default ? TimeSpan.FromSeconds(5) : navigationTimeout);

	/// <summary>
	/// Gets the effective navigation timeout
	/// </summary>
	public TimeSpan EffectiveNavigationTimeout => DefaultNavigationTimeout == default ? TimeSpan.FromSeconds(5) : DefaultNavigationTimeout;
}
