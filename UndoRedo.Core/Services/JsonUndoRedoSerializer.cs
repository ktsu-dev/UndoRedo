// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

using ktsu.UndoRedo.Core.Contracts;
using ktsu.UndoRedo.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ktsu.UndoRedo.Core.Services;

/// <summary>
/// JSON-based serializer for undo/redo stack state
/// </summary>
public class JsonUndoRedoSerializer : IUndoRedoSerializer
{
	private static readonly JsonSerializerOptions DefaultOptions = new()
	{
		WriteIndented = false,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Converters = { new JsonStringEnumConverter() }
	};

	private readonly JsonSerializerOptions _options;

	/// <summary>
	/// Initializes a new instance of the JsonUndoRedoSerializer
	/// </summary>
	/// <param name="options">Custom JSON serializer options</param>
	public JsonUndoRedoSerializer(JsonSerializerOptions? options = null)
	{
		_options = options ?? DefaultOptions;
	}

	/// <inheritdoc />
	public string FormatVersion => "json-v1.0";

	/// <inheritdoc />
	public bool SupportsVersion(string version) =>
		version == FormatVersion || version.StartsWith("json-v1.");

	/// <inheritdoc />
	public async Task<byte[]> SerializeAsync(
		IReadOnlyList<ICommand> commands,
		int currentPosition,
		IReadOnlyList<SaveBoundary> saveBoundaries,
		CancellationToken cancellationToken = default)
	{
		var serializableCommands = commands.Select(ConvertToSerializableCommand).ToList();
		var state = new SerializableStackState
		{
			Commands = serializableCommands,
			CurrentPosition = currentPosition,
			SaveBoundaries = saveBoundaries.ToList(),
			FormatVersion = FormatVersion,
			Timestamp = DateTime.UtcNow
		};

		using var stream = new MemoryStream();
		await JsonSerializer.SerializeAsync(stream, state, _options, cancellationToken);
		return stream.ToArray();
	}

	/// <inheritdoc />
	public async Task<UndoRedoStackState> DeserializeAsync(
		byte[] data,
		CancellationToken cancellationToken = default)
	{
		using var stream = new MemoryStream(data);
		var serializableState = await JsonSerializer.DeserializeAsync<SerializableStackState>(stream, _options, cancellationToken)
			?? throw new InvalidOperationException("Failed to deserialize stack state");

		if (!SupportsVersion(serializableState.FormatVersion))
		{
			throw new NotSupportedException($"Unsupported format version: {serializableState.FormatVersion}");
		}

		var commands = serializableState.Commands.Select(ConvertFromSerializableCommand).ToList();
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
			return new PlaceholderCommand(serializableCommand.Description, serializableCommand.Metadata);
		}

		// For commands that implement ISerializableCommand, try to reconstruct them
		var commandType = Type.GetType(serializableCommand.Type);
		if (commandType != null && typeof(ISerializableCommand).IsAssignableFrom(commandType))
		{
			var instance = Activator.CreateInstance(commandType) as ISerializableCommand;
			instance?.DeserializeData(serializableCommand.Data);
			return (ICommand)instance!;
		}

		// Fallback to placeholder
		return new PlaceholderCommand(serializableCommand.Description, serializableCommand.Metadata);
	}

	/// <summary>
	/// Serializable representation of a command
	/// </summary>
	private class SerializableCommand
	{
		public string Type { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public ChangeMetadata Metadata { get; set; } = null!;
		public string? Data { get; set; }
	}

	/// <summary>
	/// Serializable representation of stack state
	/// </summary>
	private class SerializableStackState
	{
		public List<SerializableCommand> Commands { get; set; } = new();
		public int CurrentPosition { get; set; }
		public List<SaveBoundary> SaveBoundaries { get; set; } = new();
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
	string SerializeData();

	/// <summary>
	/// Deserializes the command's data from a string
	/// </summary>
	/// <param name="data">The serialized data</param>
	void DeserializeData(string data);
}

/// <summary>
/// Placeholder command used when the original command cannot be deserialized
/// </summary>
internal class PlaceholderCommand : BaseCommand
{
	public PlaceholderCommand(string description, ChangeMetadata metadata)
		: base(metadata.ChangeType, metadata.AffectedItems, metadata.NavigationContext)
	{
		Description = $"[Placeholder] {description}";
	}

	public override string Description { get; }

	public override void Execute()
	{
		throw new NotSupportedException("Placeholder commands cannot be executed");
	}

	public override void Undo()
	{
		throw new NotSupportedException("Placeholder commands cannot be undone");
	}
}
