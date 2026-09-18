using System.Net;
using TypeSafe.Internal;

namespace TypeSafe.Tests;

public class RetryTests
{
    [Fact]
    public async Task ServerErrorsAreRetriedThenSucceed()
    {
        var handler = new StubHandler((_, attempt) => attempt < 2 ? Http.Json(500, """{"error":"flaky"}""") : Http.Json(200, Http.SystemOneBody));
        using var client = Clients.Create(handler);

        var response = await client.SystemOneAsync("x", Clients.SampleQuestions());

        Assert.Equal("jev-latest", response.Model);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Null(handler.Requests[0].Header("X-TypeSafe-Retry-Count"));
        Assert.Equal("1", handler.Requests[1].Header("X-TypeSafe-Retry-Count"));
        Assert.Equal("2", handler.Requests[2].Header("X-TypeSafe-Retry-Count"));
        Assert.All(handler.Requests, r => Assert.Equal(handler.Requests[0].Body, r.Body));
    }

    [Fact]
    public async Task RetriesExhaustedRethrowsLastError()
    {
        var handler = new StubHandler((_, attempt) => Http.Json(503, $$$"""{"error":"down {{{attempt}}}"}"""));
        using var client = Clients.Create(handler);

        var error = await Assert.ThrowsAsync<TypeSafeInternalServerException>(() => client.SystemOneAsync("x", Clients.SampleQuestions()));

        Assert.Equal("down 2", error.Detail);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(404)]
    [InlineData(422)]
    public async Task ClientErrorsAreNotRetried(int status)
    {
        var handler = new StubHandler((_, _) => Http.Json(status, "{}"));
        using var client = Clients.Create(handler);

        await Assert.ThrowsAnyAsync<TypeSafeApiException>(() => client.SystemOneAsync("x", Clients.SampleQuestions()));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task RateLimitsAndTimeoutsAreRetriedByDefault()
    {
        var handler = new StubHandler((_, attempt) => attempt == 0
            ? Http.Json(429, "{}", ("retry-after-ms", "1"))
            : attempt == 1 ? Http.Json(408, "{}") : Http.Json(200, Http.SystemOneBody));
        using var client = Clients.Create(handler);

        await client.SystemOneAsync("x", Clients.SampleQuestions());
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task ConnectionErrorsAndTimeoutsAreRetried()
    {
        var handler = new StubHandler(async (_, attempt, ct) =>
        {
            if (attempt == 0) throw new HttpRequestException("reset");
            if (attempt == 1) await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Http.Json(200, Http.SystemOneBody);
        });
        using var client = Clients.Create(handler, o => o.Timeout = TimeSpan.FromMilliseconds(30));

        await client.SystemOneAsync("x", Clients.SampleQuestions());
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task ConnectionAndTimeoutRetriesCanBeDisabled()
    {
        var connection = new StubHandler((_, _, _) => throw new HttpRequestException("reset"));
        using var noConnectionRetry = Clients.Create(connection, o => o.Retry = Clients.FastRetry with { RetryConnectionErrors = false });
        await Assert.ThrowsAsync<TypeSafeApiConnectionException>(() => noConnectionRetry.SystemOneAsync("x", Clients.SampleQuestions()));
        Assert.Single(connection.Requests);

        var slow = new StubHandler(async (_, _, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Http.Empty(200);
        });
        using var noTimeoutRetry = Clients.Create(slow, o =>
        {
            o.Timeout = TimeSpan.FromMilliseconds(20);
            o.Retry = Clients.FastRetry with { RetryTimeouts = false };
        });
        await Assert.ThrowsAsync<TypeSafeApiTimeoutException>(() => noTimeoutRetry.SystemOneAsync("x", Clients.SampleQuestions()));
        Assert.Single(slow.Requests);
    }

    [Fact]
    public async Task MaxRetriesZeroDisablesRetries()
    {
        var handler = new StubHandler((_, _) => Http.Json(500, "{}"));
        using var client = Clients.Create(handler, o => o.Retry = RetryPolicy.None);

        await Assert.ThrowsAsync<TypeSafeInternalServerException>(() => client.SystemOneAsync("x", Clients.SampleQuestions()));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task PerCallRetryPolicyOverridesClientPolicy()
    {
        var handler = new StubHandler((_, _) => Http.Json(500, "{}"));
        using var client = Clients.Create(handler);

        await Assert.ThrowsAsync<TypeSafeInternalServerException>(() =>
            client.SystemOneAsync("x", Clients.SampleQuestions(), options: new RequestOptions { Retry = client.Retry with { MaxRetries = 4 } }));
        Assert.Equal(5, handler.Requests.Count);

        handler.Requests.Clear();
        await Assert.ThrowsAsync<TypeSafeInternalServerException>(() =>
            client.Models.ListAsync(new RequestOptions { Retry = RetryPolicy.None }));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task CustomStatusSetAndRetryWhenExtendRetries()
    {
        var handler = new StubHandler((_, attempt) => attempt == 0 ? Http.Json(404, "{}") : Http.Json(200, Http.SystemOneBody));
        using var statuses = Clients.Create(handler, o => o.Retry = Clients.FastRetry with { HttpStatuses = new HashSet<int> { 404 } });
        await statuses.SystemOneAsync("x", Clients.SampleQuestions());
        Assert.Equal(2, handler.Requests.Count);

        handler.Requests.Clear();
        using var retryWhen = Clients.Create(handler, o => o.Retry = Clients.FastRetry with
        {
            HttpStatuses = new HashSet<int>(),
            RetryWhen = error => error is TypeSafeNotFoundException,
        });
        await retryWhen.SystemOneAsync("x", Clients.SampleQuestions());
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task TotalTimeoutStopsBeforeADelayThatWouldExceedIt()
    {
        var handler = new StubHandler((_, _) => Http.Json(500, "{}"));
        using var client = Clients.Create(handler, o => o.Retry = RetryPolicy.Default with
        {
            BackoffInitial = TimeSpan.FromSeconds(5),
            TotalTimeout = TimeSpan.FromMilliseconds(100),
        });

        var error = await Assert.ThrowsAsync<TypeSafeInternalServerException>(() => client.SystemOneAsync("x", Clients.SampleQuestions()));
        Assert.Equal(HttpStatusCode.InternalServerError, error.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task CancellationDuringBackoffStopsPromptly()
    {
        var handler = new StubHandler((_, _) => Http.Json(500, "{}"));
        using var client = Clients.Create(handler, o => o.Retry = RetryPolicy.Default with { BackoffInitial = TimeSpan.FromSeconds(30) });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var started = DateTime.UtcNow;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.SystemOneAsync("x", Clients.SampleQuestions(), cancellationToken: cts.Token));

        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public void BackoffDoublesUpToTheCapWithJitterSubtracted()
    {
        var policy = RetryPolicy.Default with { BackoffJitter = 0 };
        Assert.Equal(TimeSpan.FromMilliseconds(500), policy.DelayFor(0));
        Assert.Equal(TimeSpan.FromMilliseconds(1000), policy.DelayFor(1));
        Assert.Equal(TimeSpan.FromMilliseconds(2000), policy.DelayFor(2));
        Assert.Equal(TimeSpan.FromMilliseconds(4000), policy.DelayFor(3));
        Assert.Equal(TimeSpan.FromMilliseconds(5000), policy.DelayFor(4));
        Assert.Equal(TimeSpan.FromMilliseconds(5000), policy.DelayFor(10));

        var jittered = RetryPolicy.Default.DelayFor(0, random: new Random(1));
        Assert.InRange(jittered.TotalMilliseconds, 375, 500);
    }

    [Fact]
    public void ServerDelaysAreHonoredUpToTheMaximum()
    {
        var policy = RetryPolicy.Default with { BackoffJitter = 0 };
        var small = new Dictionary<string, string> { ["retry-after-ms"] = "1234" };
        var large = new Dictionary<string, string> { ["retry-after"] = "120" };
        Assert.Equal(TimeSpan.FromMilliseconds(1234), policy.DelayFor(0, small));
        Assert.Equal(TimeSpan.FromMilliseconds(500), policy.DelayFor(0, large));
        Assert.Equal(TimeSpan.FromSeconds(120), (policy with { MaxRetryAfter = TimeSpan.FromMinutes(5) }).DelayFor(0, large));
        Assert.Equal(TimeSpan.FromMilliseconds(500), (policy with { RespectRetryAfter = false }).DelayFor(0, small));
    }

    [Theory]
    [InlineData("retry-after-ms", "250", 250)]
    [InlineData("retry-after-ms", " 0 ", 0)]
    [InlineData("retry-after", "2", 2000)]
    [InlineData("retry-after", "1.5", 1500)]
    [InlineData("retry-after", "-1", null)]
    [InlineData("retry-after", "soon", null)]
    [InlineData("retry-after-ms", "NaN", null)]
    [InlineData("retry-after-ms", "-5", null)]
    public void RetryAfterHeadersAreParsed(string name, string value, int? expectedMs)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [name] = value };
        var parsed = RetryAfterParser.Parse(headers);
        Assert.Equal(expectedMs is null ? null : TimeSpan.FromMilliseconds(expectedMs.Value), parsed);
    }

    [Fact]
    public void RetryAfterHttpDatesAreRelativeToNow()
    {
        var now = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.Zero);
        var future = new Dictionary<string, string> { ["Retry-After"] = "Fri, 18 Sep 2026 10:00:30 GMT" };
        var past = new Dictionary<string, string> { ["Retry-After"] = "Fri, 18 Sep 2026 09:00:00 GMT" };
        Assert.Equal(TimeSpan.FromSeconds(30), RetryAfterParser.Parse(future, now));
        Assert.Equal(TimeSpan.Zero, RetryAfterParser.Parse(past, now));
    }

    [Fact]
    public void InvalidRetryAfterMsFallsBackToRetryAfter()
    {
        var headers = new Dictionary<string, string> { ["retry-after-ms"] = "later", ["retry-after"] = "4" };
        Assert.Equal(TimeSpan.FromSeconds(4), RetryAfterParser.Parse(headers));
    }
}
