using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using TypeSafe.Internal;

namespace TypeSafe;

/// <summary>
/// A piece of content sent to the API: text, JSON object or JSON array. Strings,
/// <see cref="JsonObject"/> and <see cref="JsonArray"/> convert implicitly, and the default value
/// (see <see cref="Null"/>) stands for absent content.
/// </summary>
public readonly record struct Content
{
    /// <summary>
    /// Wrap a JSON node as content. A node that is already attached to a parent is deep-cloned, so the
    /// same node can be reused across several questions or requests without <c>System.Text.Json</c>
    /// throwing when the copy is attached elsewhere.
    /// </summary>
    /// <param name="node">The node to wrap, or <c>null</c> for absent content.</param>
    public Content(JsonNode? node) => Node = node is { Parent: not null } ? node.DeepClone() : node;

    /// <summary>The wrapped JSON node — a string value, object or array — or <c>null</c> when the content is absent.</summary>
    public JsonNode? Node { get; }

    /// <summary>Absent content: the default value, whose <see cref="Node"/> is <c>null</c>.</summary>
    public static Content Null => default;

    /// <summary>
    /// Serialize a typed value as content using source-generated metadata, so no reflection is needed and
    /// the call survives trimming and native AOT. Pass the <see cref="JsonTypeInfo{T}"/> from a
    /// <c>JsonSerializerContext</c>; property naming follows that context's options rather than the SDK's
    /// camelCase default.
    /// </summary>
    /// <typeparam name="T">The type being serialized.</typeparam>
    /// <param name="value">The value to send. A <c>null</c> value yields <see cref="Null"/>.</param>
    /// <param name="typeInfo">Source-generated metadata describing how to serialize <typeparamref name="T"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="typeInfo"/> is <c>null</c>.</exception>
    public static Content From<T>(T value, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        return new Content(JsonSerializer.SerializeToNode(value, typeInfo));
    }

    /// <summary>
    /// Convert an arbitrary value to content: <see cref="JsonNode"/> instances pass through (cloned when
    /// already attached to a parent), strings become string values, and any other object is serialized by
    /// reflection with web defaults (camelCase property names). A <c>null</c> value yields
    /// <see cref="Null"/>. Prefer <see cref="From{T}(T, JsonTypeInfo{T})"/> in trimmed or native AOT apps.
    /// </summary>
    /// <param name="value">The value to send, or <c>null</c> for absent content.</param>
    /// <exception cref="TypeSafeException">The value could not be encoded as JSON.</exception>
    [RequiresUnreferencedCode(JsonContent.ReflectionMessage)]
    [RequiresDynamicCode(JsonContent.ReflectionMessage)]
    public static Content From(object? value) =>
        value is Content content ? content : new Content(JsonContent.From(value));

    /// <summary>Wrap text as content. A <c>null</c> string yields <see cref="Null"/>.</summary>
    /// <param name="text">The text to send.</param>
    public static implicit operator Content(string? text) => new(text is null ? null : JsonValue.Create(text));

    /// <summary>Wrap a JSON object as content, cloning it when it is already attached to a parent.</summary>
    /// <param name="value">The object to send, or <c>null</c> for absent content.</param>
    public static implicit operator Content(JsonObject? value) => new(value);

    /// <summary>Wrap a JSON array as content, cloning it when it is already attached to a parent.</summary>
    /// <param name="value">The array to send, or <c>null</c> for absent content.</param>
    public static implicit operator Content(JsonArray? value) => new(value);
}
