// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.UndoRedo.Core.Services;
using ktsu.UndoRedo.Core.Contracts;

/// <summary>
/// Service for merging compatible commands
/// </summary>
public sealed class CommandMerger : ICommandMerger
{
	/// <inheritdoc />
	public bool CanMerge(ICommand first, ICommand second)
	{
		Guard.ThrowIfNull(first);
		Guard.ThrowIfNull(second);

		return first.CanMergeWith(second);
	}

	/// <inheritdoc />
	public ICommand Merge(ICommand first, ICommand second)
	{
		Guard.ThrowIfNull(first);
		Guard.ThrowIfNull(second);

		return !CanMerge(first, second) ? throw new InvalidOperationException("Commands cannot be merged") : first.MergeWith(second);
	}
}
