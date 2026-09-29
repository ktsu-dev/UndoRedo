// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Contracts;

using ktsu.UndoRedo.Models;

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
	/// <param name="initialStateIsClean">
	/// Whether position -1 holds the clean initial state, as <see cref="ISaveBoundaryManager.InitialStateIsClean"/>
	/// reports it. It must round-trip into <see cref="UndoRedoStackState.InitialStateIsClean"/>, so a
	/// reloaded stack whose oldest commands were trimmed still reports unsaved changes at -1.
	/// </param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Serialized stack state</returns>
	public Task<byte[]> SerializeAsync(
		IReadOnlyList<ICommand> commands,
		int currentPosition,
		IReadOnlyList<SaveBoundary> saveBoundaries,
		bool initialStateIsClean,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Deserializes stack state from a byte array
	/// </summary>
	/// <param name="data">The serialized data</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Deserialized stack state</returns>
	public Task<UndoRedoStackState> DeserializeAsync(
		byte[] data,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the supported format version for this serializer
	/// </summary>
	public string FormatVersion { get; }

	/// <summary>
	/// Checks if this serializer can handle the given format version
	/// </summary>
	/// <param name="version">The format version to check</param>
	/// <returns>True if supported, false otherwise</returns>
	public bool SupportsVersion(string version);
}
