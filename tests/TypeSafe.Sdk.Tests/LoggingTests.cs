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

    [Fact]
    public async Task ResponseEventCarriesRequestIdAndStatusCodeAsProperties()
    {
        var logger = new CapturingLogger();
        var handler = new StubHandler((_, _) => Http.Json(200, Http.SystemOneBody, ("x-typesafe-request-id", "req-42")));
        using var client = Clients.Create(handler, o =>
        {
            o.Logger = logger;
            o.LogLevel = LogLevel.Information;
        });

        await client.SystemOneAsync("x", Clients.SampleQuestions());

        // 1003 is the response-received event that carries a server request id (see TransportLog).
        var entry = logger.Event(1003);
        Assert.Equal(nameof(TransportLog.ResponseReceivedWithRequestId), entry.EventId.Name);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("req-42", entry.Property("RequestId"));
        Assert.Equal(200, entry.Property("StatusCode"));
        Assert.Equal(1, entry.Property("Attempt"));
        Assert.Equal(1, entry.Property("RequestNumber"));
        Assert.Equal("POST", entry.Property("Method"));
        Assert.Equal("/v1/systemone", entry.Property("Path"));
        var elapsed = Assert.IsType<long>(entry.Property("ElapsedMs"));
        Assert.True(elapsed >= 0, $"ElapsedMs should be non-negative but was {elapsed}.");
    }

    [Fact]
    public async Task ResponseEventWithoutARequestIdUsesTheOtherEventId()
    {
        var logger = new CapturingLogger();
        var handler = new StubHandler((_, _) => Http.Json(200, Http.SystemOneBody));
        using var client = Clients.Create(handler, o =>
        {
            o.Logger = logger;
            o.LogLevel = LogLevel.Information;
        });

        await client.SystemOneAsync("x", Clients.SampleQuestions());

        // No request id header, so the server-id-less variant, 1002, is logged instead of 1003.
        var entry = logger.Event(1002);
        Assert.Equal(nameof(TransportLog.ResponseReceived), entry.EventId.Name);
        Assert.False(entry.Has("RequestId"));
        Assert.Equal(200, entry.Property("StatusCode"));
        Assert.Equal(1, entry.Property("Attempt"));
        Assert.True(Assert.IsType<long>(entry.Property("ElapsedMs")) >= 0);
        Assert.DoesNotContain(logger.Entries, e => e.EventId.Id == 1003);
    }

    [Fact]
    public async Task RetryScheduledEventCarriesDelayAndRetryCountsAsProperties()
    {
        var logger = new CapturingLogger();
        var handler = new StubHandler((_, attempt) => attempt == 0 ? Http.Json(500, "{}") : Http.Json(200, Http.SystemOneBody));
        using var client = Clients.Create(handler, o =>
        {
            o.Logger = logger;
            o.LogLevel = LogLevel.Information;
        });

        await client.SystemOneAsync("x", Clients.SampleQuestions());

        var entry = logger.Event(1009);
        Assert.Equal(nameof(TransportLog.RetryScheduled), entry.EventId.Name);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(1, entry.Property("Retry"));
        Assert.Equal(2, entry.Property("TotalRetries"));
        Assert.Equal("500", entry.Property("Reason"));
        Assert.Equal("POST", entry.Property("Method"));
        Assert.Equal("/v1/systemone", entry.Property("Path"));
        var delay = Assert.IsType<long>(entry.Property("DelayMs"));
        Assert.True(delay >= 0, $"DelayMs should be non-negative but was {delay}.");
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
