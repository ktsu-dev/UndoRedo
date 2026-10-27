// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Contracts;

using ktsu.UndoRedo.Models;

/// <summary>
/// A serializer that writes a whole <see cref="UndoRedoStackState"/>, including the parts
/// <see cref="IUndoRedoSerializer.SerializeAsync"/> has no parameter for
/// </summary>
/// <remarks>
/// <c>SaveStateAsync</c> uses this overload when the configured serializer implements it. A serializer that
/// doesn't loses <see cref="UndoRedoStackState.LatestSaveBoundaryIndex"/>, so after a branch removed the
/// latest save point a reloaded history counts an older one as clean again.
/// </remarks>
public interface IUndoRedoStateSerializer : IUndoRedoSerializer
{
	/// <summary>
	/// Serializes a stack state to a byte array
	/// </summary>
	/// <param name="state">The state, as <c>IUndoRedoService.GetCurrentState</c> returns it</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Serialized stack state</returns>
	public Task<byte[]> SerializeAsync(UndoRedoStackState state, CancellationToken cancellationToken = default);
}
