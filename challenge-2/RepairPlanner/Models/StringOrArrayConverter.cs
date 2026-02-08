using System.Text.Json;
using System.Text.Json.Serialization;

namespace RepairPlanner.Models;

/// <summary>
/// Custom JSON converter that handles both string and array inputs for List&lt;string&gt;.
/// LLMs sometimes return a single string instead of an array when there's only one item.
/// This converter handles both cases gracefully.
/// </summary>
public sealed class StringOrArrayConverter : JsonConverter<List<string>>
{
    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Handle null
        if (reader.TokenType == JsonTokenType.Null)
        {
            return [];
        }

        // Handle string (convert to single-item list)
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            return string.IsNullOrEmpty(value) ? [] : [value];
        }

        // Handle array
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<string>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.String)
                {
                    var item = reader.GetString();
                    if (!string.IsNullOrEmpty(item))
                    {
                        list.Add(item);
                    }
                }
            }
            return list;
        }

        throw new JsonException($"Unexpected token type: {reader.TokenType}. Expected String or StartArray.");
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        // Always write as array
        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }
        writer.WriteEndArray();
    }
}
