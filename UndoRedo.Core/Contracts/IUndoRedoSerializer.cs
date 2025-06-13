// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

using ktsu.UndoRedo.Core.Models;

namespace ktsu.UndoRedo.Core.Contracts;

/// <summary>
/// Interface for serializing and deserializing undo/redo stack state
/// </summary>
public interface IUndoRedoSerializer
{
	/// <summary>
	/// Serializes the current stack state to a byte array
	/// </summary>
	/// <param name="commands">The commands in the stack</param>
	/// <param name="currentPosition">The current position in the stack</param>
	/// <param name="saveBoundaries">The save boundaries</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Serialized stack state</returns>
	Task<byte[]> SerializeAsync(
		IReadOnlyList<ICommand> commands,
		int currentPosition,
		IReadOnlyList<SaveBoundary> saveBoundaries,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Deserializes stack state from a byte array
	/// </summary>
	/// <param name="data">The serialized data</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Deserialized stack state</returns>
	Task<UndoRedoStackState> DeserializeAsync(
		byte[] data,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the supported format version for this serializer
	/// </summary>
	string FormatVersion { get; }

	/// <summary>
	/// Checks if this serializer can handle the given format version
	/// </summary>
	/// <param name="version">The format version to check</param>
	/// <returns>True if supported, false otherwise</returns>
	bool SupportsVersion(string version);
}
