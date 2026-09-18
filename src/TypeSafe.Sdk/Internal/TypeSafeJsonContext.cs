using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace TypeSafe.Internal;

/// <summary>
/// Source-generated <c>System.Text.Json</c> metadata for every wire body the SDK reads or writes, so
/// responses are decoded without reflection (and the SDK stays trim- and AOT-friendly).
/// </summary>
/// <remarks>
/// <para>
/// The API speaks snake_case (<c>input_tokens</c>, <c>release_date</c>, <c>request_id</c>), so the
/// naming policy is <see cref="JsonKnownNamingPolicy.SnakeCaseLower"/>; the body records also pin
/// every name with <c>[JsonPropertyName]</c>, so the wire shape survives a caller serializing them
/// through their own options.
/// </para>
/// <para>
/// <see cref="JsonUnknownDerivedTypeHandling.FallBackToBaseType"/> on <see cref="Answer"/> applies to
/// serialization only: deserializing an answer whose <c>type</c> discriminator this SDK version does
/// not know still throws (<c>Read unrecognized type discriminator id 'rank'.</c>).
/// <see cref="AnswerMapConverter"/> therefore reads the <c>answers</c> map entry by entry, carrying an
/// unmodelled answer as a bare <see cref="Answer"/> — discriminator and all in
/// <see cref="Answer.AdditionalProperties"/> — for <see cref="SystemOneResponse"/> to warn about and
/// keep out of the per-type views.
/// </para>
/// <para>
/// Request bodies are assembled as a <see cref="JsonObject"/> from context-serialized parts — the
/// envelope has caller-chosen keys (<c>extra_body</c>) and must keep byte-for-byte wire parity with the
/// upstream SDKs — and written through <see cref="JsonObject"/> metadata registered here, so no
/// reflection-based serializer overload is reached on the request path either.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    AllowOutOfOrderMetadataProperties = true,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(SystemOneBody))]
[JsonSerializable(typeof(ModelList))]
[JsonSerializable(typeof(Answer))]
[JsonSerializable(typeof(NoulAnswer))]
[JsonSerializable(typeof(ChoiceAnswer))]
[JsonSerializable(typeof(ScoreAnswer))]
[JsonSerializable(typeof(Usage))]
[JsonSerializable(typeof(ModelMetadata))]
// The assembled request-body envelope, written verbatim: a JsonObject carries the raw question objects
// RawQuestion forwards, and the arbitrary top-level keys ExtraBody merges in, without reflection.
[JsonSerializable(typeof(JsonObject))]
// Questions are only ever written, never read, but they keep the default generation mode: the
// serialization-only mode does not support the [JsonDerivedType] attributes that write a question's
// "type" discriminator (SYSLIB1039).
[JsonSerializable(typeof(Question))]
[JsonSerializable(typeof(NoulQuestion))]
[JsonSerializable(typeof(ChoiceQuestion))]
[JsonSerializable(typeof(ScoreQuestion))]
[JsonSerializable(typeof(NoulCriteria))]
internal partial class TypeSafeJsonContext : JsonSerializerContext;
