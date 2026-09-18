using System.Globalization;
using System.Text.Json.Nodes;
using TypeSafe.Internal;

namespace TypeSafe;

/// <summary>A response object that also exposes the originating HTTP response and request ID.</summary>
public abstract class ApiResponse
{
    /// <summary>The <c>x-typesafe-request-id</c> response header, or <c>null</c> when the response did not include one.</summary>
    public string? RequestId { get; private set; }

    /// <summary>HTTP response status code, or <c>0</c> when not created from an HTTP response.</summary>
    public int Status { get; private set; }

    /// <summary>HTTP response headers, keyed case-insensitively.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; private set; } = HeaderSnapshot.Empty;

    /// <summary>The underlying HTTP response with its buffered body, or <c>null</c> when not created from an HTTP response.</summary>
    public HttpResponseMessage? RawHttpResponse { get; private set; }

    internal void Attach(HttpResponseMessage response, IReadOnlyDictionary<string, string> headers)
    {
        RawHttpResponse = response;
        Status = (int)response.StatusCode;
        Headers = headers;
        RequestId = HeaderSnapshot.RequestId(headers);
    }
}

/// <summary>An answer to a single question, identified by its <see cref="Type"/>.</summary>
public abstract class Answer
{
    private protected Answer() { }

    /// <summary>The wire discriminator: <c>noul</c>, <c>choice</c>, or <c>score</c>.</summary>
    public abstract string Type { get; }
}

/// <summary>A yes/no answer. See the <see href="https://docs.typesafe.ai/primitives/noul">noul primitive</see>.</summary>
public sealed class NoulAnswer : Answer
{
    /// <summary>Create a yes/no answer.</summary>
    public NoulAnswer(double noul)
    {
        Noul = noul;
    }

    /// <inheritdoc/>
    public override string Type => "noul";

    /// <summary>Probability of a yes answer, from zero to one.</summary>
    public double Noul { get; }
}

/// <summary>A selected label and its probabilities. See the <see href="https://docs.typesafe.ai/primitives/choice">choice primitive</see>.</summary>
public sealed class ChoiceAnswer : Answer
{
    /// <summary>Create a choice answer.</summary>
    public ChoiceAnswer(string choice, double confidence, IReadOnlyDictionary<string, double> probabilities)
    {
        Choice = choice ?? throw new ArgumentNullException(nameof(choice));
        Confidence = confidence;
        Probabilities = probabilities ?? throw new ArgumentNullException(nameof(probabilities));
    }

    /// <inheritdoc/>
    public override string Type => "choice";

    /// <summary>The selected label.</summary>
    public string Choice { get; }

    /// <summary>Reported confidence in the selected label.</summary>
    public double Confidence { get; }

    /// <summary>Probabilities keyed by label.</summary>
    public IReadOnlyDictionary<string, double> Probabilities { get; }
}

/// <summary>An expected score with its rubric and probabilities. See the <see href="https://docs.typesafe.ai/primitives/score">score primitive</see>.</summary>
public sealed class ScoreAnswer : Answer
{
    /// <summary>Create a score answer.</summary>
    public ScoreAnswer(double score, double confidence, IReadOnlyDictionary<int, JsonNode?> legend, IReadOnlyDictionary<int, double> probabilities)
    {
        Score = score;
        Confidence = confidence;
        Legend = legend ?? throw new ArgumentNullException(nameof(legend));
        Probabilities = probabilities ?? throw new ArgumentNullException(nameof(probabilities));
    }

    /// <inheritdoc/>
    public override string Type => "score";

    /// <summary>Expected score, which may fall between the integer rubric levels.</summary>
    public double Score { get; }

    /// <summary>Reported confidence in the score.</summary>
    public double Confidence { get; }

    /// <summary>Rubric descriptions keyed by integer score, as text, a JSON object, or an array.</summary>
    public IReadOnlyDictionary<int, JsonNode?> Legend { get; }

    /// <summary>Probabilities keyed by integer score.</summary>
    public IReadOnlyDictionary<int, double> Probabilities { get; }
}

/// <summary>Token counts for a request, when reported by the API.</summary>
public sealed class Usage
{
    /// <summary>Create usage metadata.</summary>
    public Usage(long? inputTokens = null, long? outputTokens = null)
    {
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
    }

    /// <summary>Number of input tokens used, or <c>null</c> when the API did not report it.</summary>
    public long? InputTokens { get; }

    /// <summary>Number of output tokens used, or <c>null</c> when the API did not report it.</summary>
    public long? OutputTokens { get; }
}

/// <summary>
/// Answers keyed by question name, grouped by type, with model and usage metadata.
/// See <see href="https://docs.typesafe.ai/concepts/system-one">System One</see> for details.
/// </summary>
public sealed class SystemOneResponse : ApiResponse
{
    private IReadOnlyDictionary<string, NoulAnswer>? _nouls;
    private IReadOnlyDictionary<string, ChoiceAnswer>? _choices;
    private IReadOnlyDictionary<string, ScoreAnswer>? _scores;

    /// <summary>Create a response from its parts.</summary>
    public SystemOneResponse(string model, Usage usage, IReadOnlyDictionary<string, Answer> answers)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Usage = usage ?? throw new ArgumentNullException(nameof(usage));
        Answers = answers ?? throw new ArgumentNullException(nameof(answers));
    }

    /// <summary>The model used to answer the request.</summary>
    public string Model { get; }

    /// <summary>Token usage for the request.</summary>
    public Usage Usage { get; }

    /// <summary>All answer objects keyed by question name.</summary>
    public IReadOnlyDictionary<string, Answer> Answers { get; }

    /// <summary>Yes/no answers keyed by question name.</summary>
    public IReadOnlyDictionary<string, NoulAnswer> Nouls => _nouls ??= Filter<NoulAnswer>();

    /// <summary>Choice answers keyed by question name.</summary>
    public IReadOnlyDictionary<string, ChoiceAnswer> Choices => _choices ??= Filter<ChoiceAnswer>();

    /// <summary>Score answers keyed by question name.</summary>
    public IReadOnlyDictionary<string, ScoreAnswer> Scores => _scores ??= Filter<ScoreAnswer>();

    private Dictionary<string, TAnswer> Filter<TAnswer>() where TAnswer : Answer
    {
        var filtered = new Dictionary<string, TAnswer>();
        foreach (var (name, answer) in Answers)
        {
            if (answer is TAnswer typed) filtered[name] = typed;
        }
        return filtered;
    }

    /// <summary>Decode a <c>POST /v1/systemone</c> body, skipping answer types this SDK does not model.</summary>
    internal static SystemOneResponse Parse(JsonNode? body, Action<string>? warn)
    {
        var root = Wire.Object(body, "");
        var model = Wire.String(root, "model", "");
        var usage = ParseUsage(Wire.Object(Wire.Required(root, "usage", ""), "usage"), "usage");
        var answers = new Dictionary<string, Answer>();
        var answersJson = Wire.Object(Wire.Required(root, "answers", ""), "answers");
        foreach (var (name, node) in answersJson)
        {
            var path = $"answers.{name}";
            var answerJson = Wire.Object(node, path);
            var type = Wire.String(answerJson, "type", path);
            var answer = ParseAnswer(type, answerJson, path);
            if (answer is null)
            {
                // Forward-compat: ignore answer types this SDK version does not model. The raw payload
                // is still available through RawHttpResponse.
                warn?.Invoke($"Ignoring answer \"{name}\" with unrecognized type \"{type}\"");
                continue;
            }
            answers[name] = answer;
        }
        return new SystemOneResponse(model, usage, answers);
    }

    private static Usage ParseUsage(JsonObject json, string path) =>
        new(Wire.OptionalInteger(json, "input_tokens", path), Wire.OptionalInteger(json, "output_tokens", path));

    private static Answer? ParseAnswer(string type, JsonObject json, string path) => type switch
    {
        "noul" => new NoulAnswer(Wire.Number(json, "noul", path)),
        "choice" => new ChoiceAnswer(
            Wire.String(json, "choice", path),
            Wire.Number(json, "confidence", path),
            Wire.NumberMap(Wire.Object(Wire.Required(json, "probabilities", path), $"{path}.probabilities"), $"{path}.probabilities")),
        "score" => new ScoreAnswer(
            Wire.Number(json, "score", path),
            Wire.Number(json, "confidence", path),
            Wire.Legend(Wire.Object(Wire.Required(json, "legend", path), $"{path}.legend"), $"{path}.legend"),
            Wire.ScoreMap(Wire.Object(Wire.Required(json, "probabilities", path), $"{path}.probabilities"), $"{path}.probabilities")),
        _ => null,
    };
}

/// <summary>Metadata for an available model.</summary>
public sealed class ModelMetadata
{
    /// <summary>Create model metadata.</summary>
    public ModelMetadata(string name, string description, string releaseDate)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        ReleaseDate = releaseDate ?? throw new ArgumentNullException(nameof(releaseDate));
    }

    /// <summary>The model name, usable as a request's <c>model</c>.</summary>
    public string Name { get; }

    /// <summary>A description of the model.</summary>
    public string Description { get; }

    /// <summary>The model's release date, as reported by the API.</summary>
    public string ReleaseDate { get; }
}

/// <summary>The models available to the account.</summary>
public sealed class ListModelsResponse : ApiResponse
{
    /// <summary>Create a model list response.</summary>
    public ListModelsResponse(IReadOnlyList<ModelMetadata> models)
    {
        Models = models ?? throw new ArgumentNullException(nameof(models));
    }

    /// <summary>The available models.</summary>
    public IReadOnlyList<ModelMetadata> Models { get; }

    /// <summary>Decode a <c>GET /v1/models</c> body.</summary>
    internal static ListModelsResponse Parse(JsonNode? body)
    {
        var root = Wire.Object(body, "");
        var list = Wire.Required(root, "models", "") as JsonArray ?? throw new ResponseFieldException("models");
        var models = new List<ModelMetadata>(list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            var path = $"models.{i}";
            var item = Wire.Object(list[i], path);
            models.Add(new ModelMetadata(
                Wire.String(item, "name", path),
                Wire.String(item, "description", path),
                Wire.String(item, "release_date", path)));
        }
        return new ListModelsResponse(models);
    }
}

/// <summary>Raised while decoding a successful response body; carries the dotted path of the bad field.</summary>
internal sealed class ResponseFieldException : Exception
{
    public ResponseFieldException(string fieldPath) : base($"Invalid response data at '{fieldPath}'.")
    {
        FieldPath = fieldPath;
    }

    public string FieldPath { get; }
}

/// <summary>Strict field readers for response bodies. Unknown fields are ignored so newer servers never break older clients.</summary>
internal static class Wire
{
    private static string Join(string path, string key) => path.Length == 0 ? key : $"{path}.{key}";

    public static JsonObject Object(JsonNode? node, string path) =>
        node as JsonObject ?? throw new ResponseFieldException(path);

    public static JsonNode Required(JsonObject json, string key, string path) =>
        json.TryGetPropertyValue(key, out var node) && node is not null ? node : throw new ResponseFieldException(Join(path, key));

    public static string String(JsonObject json, string key, string path) =>
        JsonContent.AsString(Required(json, key, path)) ?? throw new ResponseFieldException(Join(path, key));

    public static double Number(JsonObject json, string key, string path) =>
        JsonContent.AsDouble(Required(json, key, path)) ?? throw new ResponseFieldException(Join(path, key));

    public static long? OptionalInteger(JsonObject json, string key, string path)
    {
        if (!json.TryGetPropertyValue(key, out var node) || node is null) return null;
        if (node is JsonValue value && value.TryGetValue<long>(out var integer)) return integer;
        var number = JsonContent.AsDouble(node);
        if (number is { } d && Math.Floor(d) == d) return (long)d;
        throw new ResponseFieldException(Join(path, key));
    }

    public static Dictionary<string, double> NumberMap(JsonObject json, string path)
    {
        var map = new Dictionary<string, double>();
        foreach (var (key, node) in json)
            map[key] = JsonContent.AsDouble(node) ?? throw new ResponseFieldException(Join(path, key));
        return map;
    }

    public static Dictionary<int, double> ScoreMap(JsonObject json, string path)
    {
        var map = new Dictionary<int, double>();
        foreach (var (key, node) in json)
            map[ScoreKey(key, path)] = JsonContent.AsDouble(node) ?? throw new ResponseFieldException(Join(path, key));
        return map;
    }

    public static Dictionary<int, JsonNode?> Legend(JsonObject json, string path)
    {
        var map = new Dictionary<int, JsonNode?>();
        foreach (var (key, node) in json) map[ScoreKey(key, path)] = node?.DeepClone();
        return map;
    }

    private static int ScoreKey(string key, string path) =>
        int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var score)
            ? score
            : throw new ResponseFieldException(Join(path, key));
}
