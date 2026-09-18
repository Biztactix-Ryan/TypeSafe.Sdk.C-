using System.Text.Json;
using System.Text.Json.Nodes;

namespace TypeSafe.Internal;

/// <summary>Conversion of user-supplied values into JSON nodes, and lenient response decoding.</summary>
internal static class JsonContent
{
    /// <summary>Options used when converting plain .NET objects (POCOs, anonymous types, dictionaries) into JSON.</summary>
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private static readonly JsonSerializerOptions CompactOptions = new() { WriteIndented = false };

    /// <summary>
    /// Convert a value to a JSON node. <see cref="JsonNode"/> instances pass through (cloned when already
    /// attached to a parent), strings become string values, and any other object is serialized with
    /// web defaults (camelCase property names).
    /// </summary>
    public static JsonNode? From(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case JsonNode node:
                return node.Parent is null ? node : node.DeepClone();
            case string text:
                return JsonValue.Create(text);
            case JsonElement element:
                return element.ValueKind == JsonValueKind.Null ? null : JsonValue.Create(element);
            case JsonDocument document:
                return From(document.RootElement);
            default:
                try
                {
                    return JsonSerializer.SerializeToNode(value, value.GetType(), SerializerOptions);
                }
                catch (Exception error) when (error is JsonException or NotSupportedException or InvalidOperationException)
                {
                    throw new TypeSafeException("The request body could not be encoded as JSON.", error);
                }
        }
    }

    /// <summary>The string content of a string value node, or <c>null</c> for any other node.</summary>
    public static string? AsString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>The numeric content of a number node, or <c>null</c> for any other node.</summary>
    public static double? AsDouble(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<double>(out var number) && double.IsFinite(number) ? number : null;

    public static string Serialize(JsonNode node) => node.ToJsonString(CompactOptions);

    /// <summary>
    /// Decode a response body leniently: valid JSON becomes a node tree, any other text becomes a
    /// string node, and an empty body becomes <c>null</c>.
    /// </summary>
    public static JsonNode? ParseLenient(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return JsonValue.Create(text);
        }
    }
}
