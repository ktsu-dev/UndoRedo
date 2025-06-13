// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.UndoRedo.Core.Models;

/// <summary>
/// Visualization data for a change in the undo/redo stack
/// </summary>
/// <param name="Command">The command</param>
/// <param name="Position">Position in the stack</param>
/// <param name="IsExecuted">Whether this command is currently executed (not undone)</param>
/// <param name="HasSaveBoundary">Whether there's a save boundary at this position</param>
public record ChangeVisualization(
	ICommand Command,
	int Position,
	bool IsExecuted,
	bool HasSaveBoundary
);
