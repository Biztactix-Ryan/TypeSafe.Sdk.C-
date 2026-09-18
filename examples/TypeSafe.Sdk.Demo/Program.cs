// Run with `dotnet run --project examples/TypeSafe.Sdk.Demo`. Needs TYPESAFE_API_KEY in the environment.
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TypeSafe;

using var client = new TypeSafeClient(new TypeSafeClientOptions { LogLevel = LogLevel.Information });

var models = await client.Models.ListAsync();
Console.WriteLine("Available models: " + string.Join(", ", models.Select(m => m.Name)));

var state = new DemoState(
    Subject: "Charged twice this month",
    Body: "Hi, I see two charges of $49 on my card for August. I only have one account. Please fix this ASAP, I'm pretty frustrated.");

try
{
    var response = await client.SystemOneAsync(
        state: Content.From(state, DemoJsonContext.Default.DemoState),
        questions: new Questions
        {
            ["isBilling"] = Question.Noul("Is this ticket about billing?"),
            ["sentiment"] = Question.Choice("What is the customer's tone?", "calm", "frustrated", "angry"),
            ["urgency"] = Question.Score("How urgent is this ticket?", "can wait", "this week", "today", "right now"),
            ["refundRisk"] = Question.Score("How likely is the customer to demand a refund?", "unlikely", "possible", "likely"),
        });

    // Every answer is available by name, grouped by question type.
    var isBilling = response.Nouls["isBilling"];
    var sentiment = response.Choices["sentiment"];
    var urgency = response.Scores["urgency"];
    var refundRisk = response.Scores["refundRisk"];

    Console.WriteLine($"billing?     {isBilling.Noul:F2}");
    Console.WriteLine($"tone         {sentiment.Choice} ({sentiment.Probabilities[sentiment.Choice]:F2})");
    Console.WriteLine($"urgency      {urgency.Score:F2} on a 0-{urgency.Legend.Count - 1} scale: " +
                      string.Join(", ", urgency.Legend.OrderBy(l => l.Key).Select(l => $"{l.Key}={l.Value}")));
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
