// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.UndoRedo;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Reads <see cref="ChangeMetadata.CustomData"/> back as plain values rather than <see cref="JsonElement"/>
/// </summary>
/// <remarks>
/// A string comes back as a <see cref="string"/>, a whole number as a <see cref="long"/> (so an
/// <see cref="int"/> is read back as a <see cref="long"/>), any other number as a <see cref="double"/>,
/// true and false as a <see cref="bool"/>, an array as a <see cref="List{T}"/> of object, and an object as a
/// <see cref="Dictionary{TKey, TValue}"/> of string to object. Anything else is written as JSON and comes
/// back as one of those: a <see cref="DateTime"/>, for example, is read back as its string.
/// </remarks>
internal sealed class CustomDataJsonConverter : JsonConverter<IReadOnlyDictionary<string, object>?>
{
	public override bool HandleNull => true;

	public override IReadOnlyDictionary<string, object>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Null)
		{
			return null;
		}

		using JsonDocument document = JsonDocument.ParseValue(ref reader);
		return document.RootElement.ValueKind == JsonValueKind.Object
			? ReadObject(document.RootElement)
			: throw new JsonException("Custom data must be a JSON object.");
	}

	public override void Write(Utf8JsonWriter writer, IReadOnlyDictionary<string, object>? value, JsonSerializerOptions options)
	{
		Ensure.NotNull(writer);
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		writer.WriteStartObject();
		foreach (KeyValuePair<string, object> entry in value)
		{
			writer.WritePropertyName(options?.DictionaryKeyPolicy?.ConvertName(entry.Key) ?? entry.Key);
			JsonSerializer.Serialize(writer, entry.Value, entry.Value?.GetType() ?? typeof(object), options);
		}

		writer.WriteEndObject();
	}

	private static Dictionary<string, object> ReadObject(JsonElement element)
	{
		Dictionary<string, object> values = [];
		foreach (JsonProperty property in element.EnumerateObject())
		{
			values[property.Name] = ReadValue(property.Value)!;
		}

		return values;
	}

	private static object? ReadValue(JsonElement element) => element.ValueKind switch
	{
		JsonValueKind.String => element.GetString(),
		JsonValueKind.Number => element.TryGetInt64(out long whole) ? whole : (object)element.GetDouble(),
		JsonValueKind.True => true,
		JsonValueKind.False => false,
		JsonValueKind.Array => element.EnumerateArray().Select(ReadValue).ToList(),
		JsonValueKind.Object => ReadObject(element),
		_ => null,
	};
}
