// Run with `dotnet run --project examples/TypeSafe.Sdk.Demo`. Needs TYPESAFE_API_KEY in the environment.
using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TypeSafe;

using var client = new TypeSafeClient(new TypeSafeClientOptions { LogLevel = LogLevel.Information });

var models = await client.Models.ListAsync();
Console.WriteLine("Available models: " + string.Join(", ", models.Select(m => m.Name)));

var state = new DemoState(
    Subject: "Charged twice this month",
    Body: "Hi, I see two charges of $49 on my card for August. I only have one account. Please fix this ASAP, I'm pretty frustrated.");

// Name-first questions: each one pairs its name with the answer type it is read back as.
// The tone and urgency questions take their labels and their rubric from an enum, so the answers come
// back as Tone and Urgency members rather than strings and integers.
var isBillingQuestion = Question.Named.Noul("isBilling", "Is this ticket about billing?");
var sentimentQuestion = Question.Named.Choice<Tone>("sentiment", "What is the customer's tone?");
var urgencyQuestion = Question.Named.Score<Urgency>("urgency", "How urgent is this ticket?");
var refundRiskQuestion = Question.Named.Score("refundRisk", "How likely is the customer to demand a refund?", "unlikely", "possible", "likely");

try
{
    var response = await client.SystemOneAsync(
        state: Content.From(state, DemoJsonContext.Default.DemoState),
        questions: [isBillingQuestion, sentimentQuestion, urgencyQuestion, refundRiskQuestion]);

    // Each question is its own key, and hands back the answer type it is answered with.
    var isBilling = response.Get(isBillingQuestion);
    var sentiment = response.Get(sentimentQuestion);
    var urgency = response.Get(urgencyQuestion);
    var refundRisk = response.Get(refundRiskQuestion);

    Console.WriteLine($"billing?     {isBilling.Probability:F2}");
    // The enum-typed answers are keyed by the enum: no label or rubric level is ever spelled twice.
    Console.WriteLine($"tone         {sentiment.Choice} ({sentiment.Probabilities[sentiment.Choice]:F2}), " +
                      $"frustrated={sentiment.Probabilities[Tone.Frustrated]:F2}, very angry={sentiment.Probabilities[Tone.VeryAngry]:F2}");
    Console.WriteLine($"urgency      {urgency.Nearest} ({urgency.Legend[urgency.Nearest]}) at {urgency.Score:F2}: " +
                      string.Join(", ", urgency.Probabilities.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value:F2}")));
    Console.WriteLine($"refund risk  {refundRisk.Score:F2} ({refundRisk.Confidence:F2} confidence)");
    Console.WriteLine($"tokens       {response.Usage.InputTokens} in / {response.Usage.OutputTokens} out");
    Console.WriteLine($"request id   {response.RequestId ?? "unknown"}");
}
catch (TypeSafeApiException error)
{
    Console.Error.WriteLine($"API error {(int)error.StatusCode} (request {error.RequestId ?? "unknown"}): {error.Body?.ToJsonString() ?? "(no body)"}");
    return 1;
}

return 0;

/// <summary>
/// The tones the ticket can be written in. A member's wire label is its
/// <c>[JsonStringEnumMemberName]</c> when it carries one and the snake_case of its name otherwise, so
/// these are sent as <c>calm</c>, <c>frustrated</c> and <c>very_angry</c>.
/// </summary>
internal enum Tone
{
    Calm,
    Frustrated,
    [JsonStringEnumMemberName("very_angry")]
    VeryAngry,
}

/// <summary>
/// The urgency rubric, lowest level first. A score rubric is indexed by score, so the members must be
/// numbered exactly 0 to N-1, and each one's <c>[Description]</c> is the rubric text sent to the model.
/// </summary>
internal enum Urgency
{
    [Description("can wait")]
    Low = 0,
    [Description("today")]
    Medium = 1,
    [Description("right now")]
    High = 2,
}

/// <summary>The support ticket this demo sends as the request state.</summary>
internal record DemoState(string Subject, string Body);

/// <summary>
/// Source-generated metadata for <see cref="DemoState"/>, so the state is serialized without
/// reflection and the demo stays trim- and AOT-safe. CamelCase naming keeps the wire keys
/// <c>subject</c> and <c>body</c>.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DemoState))]
internal partial class DemoJsonContext : JsonSerializerContext;
