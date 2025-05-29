// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.UndoRedo.Core;

/// <summary>
/// Interface for providing navigation capabilities to focus on changes
/// </summary>
public interface INavigationProvider
{
	/// <summary>
	/// Navigate to a specific context, typically to show where a change was made
	/// </summary>
	/// <param name="context">The navigation context from a command</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>True if navigation was successful</returns>
	public Task<bool> NavigateToAsync(string context, CancellationToken cancellationToken = default);

	/// <summary>
	/// Check if a navigation context is valid/reachable
	/// </summary>
	/// <param name="context">The navigation context to validate</param>
	/// <returns>True if the context is valid</returns>
	public bool IsValidContext(string context);
}
