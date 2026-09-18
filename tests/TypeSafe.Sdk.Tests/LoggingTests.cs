using Microsoft.Extensions.Logging;
using TypeSafe.Internal;

namespace TypeSafe.Tests;

public class LoggingTests
{
    [Fact]
    public async Task InfoLogsRequestSummariesWithoutBodies()
    {
        var logger = new CapturingLogger();
        var handler = new StubHandler((_, _) => Http.Json(200, Http.SystemOneBody, ("x-typesafe-request-id", "req-1")));
        using var client = Clients.Create(handler, o =>
        {
            o.Logger = logger;
            o.LogLevel = LogLevel.Information;
        });

        await client.SystemOneAsync("secret document", Clients.SampleQuestions());

        var info = Assert.Single(logger.Messages(LogLevel.Information));
        Assert.StartsWith("#1 POST /v1/systemone <- 200 in ", info);
        Assert.EndsWith("(request req-1)", info);
        Assert.Empty(logger.Messages(LogLevel.Debug));
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("secret document"));
    }

    [Fact]
    public async Task DebugLogsRedactedHeadersAndBodies()
    {
        var logger = new CapturingLogger();
        var handler = new StubHandler((_, _) => Http.Json(200, Http.SystemOneBody, ("Set-Cookie", "session=abc")));
        using var client = Clients.Create(handler, o =>
        {
            o.Logger = logger;
            o.LogLevel = LogLevel.Debug;
            o.DefaultHeaders = new Dictionary<string, string> { ["X-Api-Key"] = "abcdefghijkl", ["X-Team"] = "billing" };
        });

        await client.SystemOneAsync("the document", Clients.SampleQuestions());

        var debug = logger.Messages(LogLevel.Debug).ToList();
        Assert.Equal(2, debug.Count);
        var outbound = debug[0];
        Assert.Contains("Authorization: Bearer ***7890", outbound);
        Assert.Contains("X-Api-Key: ***ijkl", outbound);
        Assert.Contains("X-Team: billing", outbound);
        Assert.DoesNotContain(Clients.ApiKey, outbound);
        Assert.Contains("\"the document\"", outbound);
        var inbound = debug[1];
        Assert.Contains("Set-Cookie: ***", inbound);
        Assert.Contains("\"answers\"", inbound);
    }

    [Fact]
    public async Task RetriesAreLoggedAtInfo()
    {
        var logger = new CapturingLogger();
        var handler = new StubHandler((_, attempt) => attempt == 0 ? Http.Json(500, "{}") : Http.Json(200, Http.SystemOneBody));
        using var client = Clients.Create(handler, o =>
        {
            o.Logger = logger;
            o.LogLevel = LogLevel.Information;
        });

        await client.SystemOneAsync("x", Clients.SampleQuestions());

        Assert.Contains(logger.Messages(LogLevel.Information), m => m.Contains("retrying in") && m.Contains("(retry 1/2) after 500"));
    }

    [Fact]
    public async Task LevelOffSilencesEverything()
    {
        var logger = new CapturingLogger();
        var handler = new StubHandler((_, _) => Http.Json(200, """{"model":"m","usage":{},"answers":{"x":{"type":"new"}}}"""));
        using var client = Clients.Create(handler, o =>
        {
            o.Logger = logger;
            o.LogLevel = LogLevel.None;
        });

        await client.SystemOneAsync("x", Clients.SampleQuestions());
        Assert.Empty(logger.Entries);
    }

    [Theory]
    [InlineData("Authorization", "Bearer sk-live-1234567890", "Bearer ***7890")]
    [InlineData("authorization", "Bearer short", "Bearer ***")]
    [InlineData("X-Api-Key", "abcdefghijkl", "***ijkl")]
    [InlineData("Proxy-Authorization", "Basic dXNlcjpwYXNz", "Basic ***YXNz")]
    [InlineData("Cookie", "a=b", "***")]
    [InlineData("X-Auth-Token", "anything", "***")]
    [InlineData("X-Client-Secret", "anything", "***")]
    [InlineData("Content-Type", "application/json", "application/json")]
    public void CredentialHeadersAreRedacted(string name, string value, string expected)
    {
        Assert.Equal(expected, Redaction.Redact(name, value));
    }
}
