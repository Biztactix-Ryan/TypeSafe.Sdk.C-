using System.Text.Json;
using System.Text.Json.Serialization;

namespace TypeSafe.Internal;

/// <summary>
/// Reads and writes the <c>answers</c> map of a <see cref="SystemOneBody"/> one entry at a time, so an
/// answer whose <c>type</c> this SDK version does not model cannot fail the whole read.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="JsonUnknownDerivedTypeHandling.FallBackToBaseType"/> covers serialization only: the
/// polymorphic reader throws on a discriminator it does not know — reading
/// <c>{"type":"rank","order":["a","b"]}</c> through <c>TypeSafeJsonContext.Default.Answer</c> raises
/// <c>JsonException: Read unrecognized type discriminator id 'rank'.</c> at path <c>$</c>, as
/// <c>JsonContextTests</c> asserts. Each entry is therefore peeked for its discriminator first. A
/// known one is read through <c>TypeSafeJsonContext.Default.Answer</c>; an unknown one becomes a bare
/// <see cref="Answer"/> whose <see cref="Answer.AdditionalProperties"/> keeps every field, <c>type</c>
/// included, so <c>AdditionalProperties["type"]</c> is the unrecognized type.
/// <see cref="SystemOneResponse"/> warns about such an answer and keeps it in
/// <see cref="SystemOneResponse.Answers"/>, where it reaches none of the per-type views.
/// </para>
/// <para>
/// A missing or non-string <c>type</c>, and an entry that is not an object at all, remain read errors,
/// as they were in the hand-written parser. Their paths are composed here rather than left to
/// <c>System.Text.Json</c>: the per-answer read is a nested <see cref="JsonSerializer"/> call, which
/// reports paths relative to the answer, and a converter's own exceptions are only given the path of
/// the map. Composing them keeps the SDK's field paths (<c>answers.tone.confidence</c>) intact.
/// </para>
/// </remarks>
internal sealed class AnswerMapConverter : JsonConverter<IReadOnlyDictionary<string, Answer>>
{
    /// <summary>The JSON path of the map itself, matching the <c>answers</c> property this converter is declared on.</summary>
    private const string Root = "$.answers";

    private static readonly string[] KnownTypes = ["noul", "choice", "score"];

    /// <summary>Read the map, answer by answer.</summary>
    public override IReadOnlyDictionary<string, Answer> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject) throw Invalid(Root);

        var answers = new Dictionary<string, Answer>(StringComparer.Ordinal);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) return answers;

            var name = reader.GetString()!;
            reader.Read();
            answers[name] = ReadAnswer(ref reader, $"{Root}.{name}");
        }

        throw Invalid(Root);
    }

    /// <summary>Write the map, letting the polymorphic writer emit each answer's discriminator.</summary>
    public override void Write(
        Utf8JsonWriter writer,
        IReadOnlyDictionary<string, Answer> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (name, answer) in value)
        {
            writer.WritePropertyName(name);
            JsonSerializer.Serialize(writer, answer, TypeSafeJsonContext.Default.Answer);
        }
        writer.WriteEndObject();
    }

    /// <summary>Read one answer, at <paramref name="path"/>, keeping an unmodelled type as a bare answer.</summary>
    private static Answer ReadAnswer(ref Utf8JsonReader reader, string path)
    {
        if (reader.TokenType != JsonTokenType.StartObject) throw Invalid(path);

        // The reader is a struct, so the copy can scan ahead for the discriminator without consuming it.
        var type = PeekType(reader);
        if (type is null) throw Invalid($"{path}.type");

        if (Array.IndexOf(KnownTypes, type) >= 0)
        {
            try
            {
                return JsonSerializer.Deserialize(ref reader, TypeSafeJsonContext.Default.Answer)!;
            }
            catch (JsonException error)
            {
                throw Invalid(Nest(path, error.Path), error);
            }
        }

        // Forward-compat: an unmodelled type is carried whole, so nothing the server sent is lost.
        var element = JsonElement.ParseValue(ref reader);
        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var field in element.EnumerateObject()) fields[field.Name] = field.Value.Clone();
        return new Answer { AdditionalProperties = fields };
    }

    /// <summary>The string value of the <c>type</c> property of the object the copied reader is on, or <c>null</c>.</summary>
    private static string? PeekType(Utf8JsonReader reader)
    {
        reader.Read();
        while (reader.TokenType == JsonTokenType.PropertyName)
        {
            var isType = reader.ValueTextEquals("type");
            reader.Read();
            if (isType) return reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
            reader.Skip();
            reader.Read();
        }
        return null;
    }

    /// <summary>Rebase a nested reader's path (<c>$.confidence</c>) onto the answer's path.</summary>
    private static string Nest(string path, string? nested) =>
        nested is null || nested.Length == 0 || nested == "$" ? path : path + nested[1..];

    private static JsonException Invalid(string path, Exception? innerException = null) =>
        new($"Invalid response data at '{path}'.", path, lineNumber: null, bytePositionInLine: null, innerException);
}
