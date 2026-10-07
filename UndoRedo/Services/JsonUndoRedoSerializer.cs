// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo.Core.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using ktsu.UndoRedo.Contracts;
using ktsu.UndoRedo.Models;

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
		Ensure.NotNull(version);
		return version == FormatVersion || version.StartsWith("json-v1.");
	}

	/// <inheritdoc />
	public async Task<byte[]> SerializeAsync(
		IReadOnlyList<ICommand> commands,
		int currentPosition,
		IReadOnlyList<SaveBoundary> saveBoundaries,
		bool initialStateIsClean,
		CancellationToken cancellationToken = default)
	{
		List<SerializableCommand> serializableCommands = [.. commands.Select(ConvertToSerializableCommand)];
		SerializableStackState state = new()
		{
			Commands = serializableCommands,
			CurrentPosition = currentPosition,
			SaveBoundaries = [.. saveBoundaries],
			InitialStateIsClean = initialStateIsClean,
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

		if (serializableState.FormatVersion is null || !SupportsVersion(serializableState.FormatVersion))
		{
			throw new NotSupportedException($"Unsupported format version: {serializableState.FormatVersion}");
		}

		ValidateShape(serializableState);

		List<ICommand> commands = [.. serializableState.Commands.Select(ConvertFromSerializableCommand)];
		return new UndoRedoStackState(
			commands,
			serializableState.CurrentPosition,
			serializableState.SaveBoundaries,
			serializableState.FormatVersion,
			serializableState.Timestamp)
		{
			InitialStateIsClean = serializableState.InitialStateIsClean,
		};
	}

	/// <summary>
	/// Rejects data that parsed as JSON but is missing fields the stack needs, such as a truncated or
	/// hand-edited file. Throws <see cref="InvalidOperationException"/>, which the deserialization
	/// contract already covers, so LoadStateAsync reports false instead of letting a
	/// NullReferenceException or ArgumentNullException escape.
	/// </summary>
	private static void ValidateShape(SerializableStackState state)
	{
		if (state.Commands is null)
		{
			throw new InvalidOperationException("Stack state has no commands list");
		}

		if (state.SaveBoundaries is null)
		{
			throw new InvalidOperationException("Stack state has no save boundaries list");
		}

		if (state.SaveBoundaries.Any(boundary => boundary is null))
		{
			throw new InvalidOperationException("Stack state contains a null save boundary");
		}

		foreach (SerializableCommand? command in state.Commands)
		{
			if (command is null)
			{
				throw new InvalidOperationException("Stack state contains a null command");
			}

			if (command.Metadata is null)
			{
				throw new InvalidOperationException($"Command '{command.Description}' has no metadata");
			}
		}
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
		// A null Data is what ConvertToSerializableCommand writes for a command that is not an
		// ISerializableCommand. An empty string is not that marker: it is a normal SerializeData()
		// result for a command with no parameters, and such a command is reconstructed below.
		if (serializableCommand.Data is null)
		{
			// Return a placeholder command that can't execute but preserves metadata
			return new PlaceholderCommand(serializableCommand.Description, serializableCommand.NavigationContext, serializableCommand.Metadata);
		}

		// For commands that implement ISerializableCommand, try to reconstruct them
		Type? commandType = ResolveCommandType(serializableCommand.Type);
		if (commandType != null && typeof(ISerializableCommand).IsAssignableFrom(commandType))
		{
			if (!typeof(ICommand).IsAssignableFrom(commandType))
			{
				throw new InvalidOperationException(
					$"Cannot reconstruct command type '{commandType.FullName}': it implements {nameof(ISerializableCommand)} but not {nameof(ICommand)}.");
			}

			ISerializableCommand? instance;
			try
			{
				instance = Activator.CreateInstance(commandType) as ISerializableCommand;
			}
			catch (MissingMethodException ex)
			{
				// Activator.CreateInstance needs a public parameterless constructor, which most real
				// command types do not have. Translate it into an exception the deserialization
				// contract already covers, so LoadStateAsync reports false instead of throwing.
				throw new InvalidOperationException(
					$"Cannot reconstruct command type '{commandType.FullName}': {nameof(ISerializableCommand)} implementations must have a public parameterless constructor for DeserializeData to populate.",
					ex);
			}
#pragma warning disable CA1031 // Do not catch general exception types
			catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031 // Do not catch general exception types
			{
				// The constructor itself can throw (surfacing as TargetInvocationException or
				// TypeInitializationException), and an open generic type cannot be constructed at all
				// (ArgumentException). Report these through the deserialization contract as well.
				throw new InvalidOperationException(
					$"Cannot reconstruct command type '{commandType.FullName}': its public parameterless constructor could not create an instance.",
					ex);
			}

			try
			{
				instance?.DeserializeData(serializableCommand.Data);
			}
#pragma warning disable CA1031 // Do not catch general exception types
			catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031 // Do not catch general exception types
			{
				// DeserializeData is the command's own parser, so it can throw anything. Report it
				// through the deserialization contract so LoadStateAsync returns false.
				throw new InvalidOperationException(
					$"Cannot reconstruct command type '{commandType.FullName}': its {nameof(ISerializableCommand.DeserializeData)} rejected the saved data.",
					ex);
			}

			// The parameterless constructor knows neither the navigation context nor the metadata
			// the command was saved with, so put them back; otherwise undo and redo stop navigating
			// after a reload, and the change size and timestamp revert to defaults.
			if (instance is BaseCommand baseCommand)
			{
				baseCommand.RestoreSerializedState(serializableCommand.NavigationContext, serializableCommand.Metadata);
			}

			return (ICommand)instance!;
		}

		// Fallback to placeholder
		return new PlaceholderCommand(serializableCommand.Description, serializableCommand.NavigationContext, serializableCommand.Metadata);
	}

	private static Type? ResolveCommandType(string typeName)
	{
		try
		{
			return Type.GetType(typeName);
		}
		catch (Exception ex) when (ex is IOException or BadImageFormatException or ArgumentException or TypeLoadException)
		{
			// Type.GetType returns null for a type it cannot find, but still throws for a malformed
			// assembly-qualified name or an assembly that fails to load.
			throw new InvalidOperationException($"Cannot resolve command type '{typeName}'.", ex);
		}
	}

	/// <summary>
	/// Serializable representation of a command
	/// </summary>
	private sealed class SerializableCommand
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
	private sealed class SerializableStackState
	{
		public List<SerializableCommand> Commands { get; set; } = [];
		public int CurrentPosition { get; set; }
		public List<SaveBoundary> SaveBoundaries { get; set; } = [];

		// Data saved before this field existed has no value for it, and meant a clean initial state
		public bool InitialStateIsClean { get; set; } = true;
		public string FormatVersion { get; set; } = string.Empty;
		public DateTime Timestamp { get; set; }
	}
}

/// <summary>
/// Interface for commands that can serialize their data
/// </summary>
/// <remarks>
/// Implementations must also provide a public parameterless constructor. Deserialization creates the
/// instance before it has any data to work from, then populates it through <see cref="DeserializeData"/>.
/// Without one, reconstructing the command fails and <c>LoadStateAsync</c> reports <see langword="false"/>.
/// </remarks>
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
internal sealed class PlaceholderCommand(string description, string? navigationContext, ChangeMetadata metadata) : BaseCommand(metadata.ChangeType, metadata.AffectedItems, navigationContext)
{
	public override string Description { get; } = $"[Placeholder] {description}";

	// Keep the deserialized metadata, rather than the fresh timestamp, size and custom data BaseCommand builds
	public override ChangeMetadata Metadata { get; protected set; } = Ensure.NotNull(metadata);

	public override void Execute() => throw new NotSupportedException("Placeholder commands cannot be executed");

	public override void Undo() => throw new NotSupportedException("Placeholder commands cannot be undone");
}
