// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.UndoRedo.Core.Services;
using System.Text.Json;
using System.Text.Json.Serialization;
using ktsu.UndoRedo.Core.Contracts;
using ktsu.UndoRedo.Core.Models;

/// <summary>
/// JSON-based serializer for undo/redo stack state
/// </summary>
/// <remarks>
/// Initializes a new instance of the JsonUndoRedoSerializer
/// </remarks>
/// <param name="options">Custom JSON serializer options</param>
public class JsonUndoRedoSerializer(JsonSerializerOptions? options = null) : IUndoRedoSerializer
{
	private static readonly JsonSerializerOptions DefaultOptions = new()
	{
		WriteIndented = false,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Converters = { new JsonStringEnumConverter() }
	};

	private readonly JsonSerializerOptions _options = options ?? DefaultOptions;

	/// <inheritdoc />
	public string FormatVersion => "json-v1.0";

	/// <inheritdoc />
	public bool SupportsVersion(string version)
	{
		Guard.ThrowIfNull(version);
		return version == FormatVersion || version.StartsWith("json-v1.");
	}

	/// <inheritdoc />
	public async Task<byte[]> SerializeAsync(
		IReadOnlyList<ICommand> commands,
		int currentPosition,
		IReadOnlyList<SaveBoundary> saveBoundaries,
		CancellationToken cancellationToken = default)
	{
		List<SerializableCommand> serializableCommands = [.. commands.Select(ConvertToSerializableCommand)];
		SerializableStackState state = new()
		{
			Commands = serializableCommands,
			CurrentPosition = currentPosition,
			SaveBoundaries = [.. saveBoundaries],
			FormatVersion = FormatVersion,
			Timestamp = DateTime.UtcNow
		};

		using MemoryStream stream = new();
		await JsonSerializer.SerializeAsync(stream, state, _options, cancellationToken).ConfigureAwait(false);
		return stream.ToArray();
	}

	/// <inheritdoc />
	public async Task<UndoRedoStackState> DeserializeAsync(
		byte[] data,
		CancellationToken cancellationToken = default)
	{
		using MemoryStream stream = new(data);
		SerializableStackState serializableState = await JsonSerializer.DeserializeAsync<SerializableStackState>(stream, _options, cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException("Failed to deserialize stack state");

		if (!SupportsVersion(serializableState.FormatVersion))
		{
			throw new NotSupportedException($"Unsupported format version: {serializableState.FormatVersion}");
		}

		List<ICommand> commands = [.. serializableState.Commands.Select(ConvertFromSerializableCommand)];
		return new UndoRedoStackState(
			commands,
			serializableState.CurrentPosition,
			serializableState.SaveBoundaries,
			serializableState.FormatVersion,
			serializableState.Timestamp);
	}

	private static SerializableCommand ConvertToSerializableCommand(ICommand command)
	{
		return new SerializableCommand
		{
			Type = command.GetType().AssemblyQualifiedName ?? command.GetType().FullName!,
			Description = command.Description,
			NavigationContext = command.NavigationContext,
			Metadata = command.Metadata,
			// Note: Execute/Undo actions cannot be serialized - this is a limitation
			// Applications need to implement their own command types that can reconstruct actions
			Data = command is ISerializableCommand serializableCmd ? serializableCmd.SerializeData() : null
		};
	}

	private static ICommand ConvertFromSerializableCommand(SerializableCommand serializableCommand)
	{
		// This is a simplified approach - real implementations would need a factory pattern
		// or registry to recreate commands from serialized data
		if (string.IsNullOrEmpty(serializableCommand.Data))
		{
			// Return a placeholder command that can't execute but preserves metadata
			return new PlaceholderCommand(serializableCommand.Description, serializableCommand.NavigationContext, serializableCommand.Metadata);
		}

		// For commands that implement ISerializableCommand, try to reconstruct them
		Type? commandType = Type.GetType(serializableCommand.Type);
		if (commandType != null && typeof(ISerializableCommand).IsAssignableFrom(commandType))
		{
			ISerializableCommand? instance = Activator.CreateInstance(commandType) as ISerializableCommand;
			instance?.DeserializeData(serializableCommand.Data!);
			return (ICommand)instance!;
		}

		// Fallback to placeholder
		return new PlaceholderCommand(serializableCommand.Description, serializableCommand.NavigationContext, serializableCommand.Metadata);
	}

	/// <summary>
	/// Serializable representation of a command
	/// </summary>
	private class SerializableCommand
	{
		public string Type { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public string? NavigationContext { get; set; }
		public ChangeMetadata Metadata { get; set; } = null!;
		public string? Data { get; set; }
	}

	/// <summary>
	/// Serializable representation of stack state
	/// </summary>
	private class SerializableStackState
	{
		public List<SerializableCommand> Commands { get; set; } = [];
		public int CurrentPosition { get; set; }
		public List<SaveBoundary> SaveBoundaries { get; set; } = [];
		public string FormatVersion { get; set; } = string.Empty;
		public DateTime Timestamp { get; set; }
	}
}

/// <summary>
/// Interface for commands that can serialize their data
/// </summary>
public interface ISerializableCommand
{
	/// <summary>
	/// Serializes the command's data to a string
	/// </summary>
	/// <returns>Serialized command data</returns>
	public string SerializeData();

	/// <summary>
	/// Deserializes the command's data from a string
	/// </summary>
	/// <param name="data">The serialized data</param>
	public void DeserializeData(string data);
}

/// <summary>
/// Placeholder command used when the original command cannot be deserialized
/// </summary>
internal class PlaceholderCommand(string description, string? navigationContext, ChangeMetadata metadata) : BaseCommand(metadata.ChangeType, metadata.AffectedItems, navigationContext)
{
	public override string Description { get; } = $"[Placeholder] {description}";

	public override void Execute() => throw new NotSupportedException("Placeholder commands cannot be executed");

	public override void Undo() => throw new NotSupportedException("Placeholder commands cannot be undone");
}
