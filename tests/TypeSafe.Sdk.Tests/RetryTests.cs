using System.Diagnostics;
using System.Globalization;
using System.Net;
using Microsoft.Extensions.Time.Testing;
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

    [Fact]
    public void RetryAfterHttpDatesFollowTheInjectedTimeProvider()
    {
        Assert.Same(TimeProvider.System, new TypeSafeClientOptions().TimeProvider);

        var clock = new FixedClock(new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.Zero));
        var headers = new Dictionary<string, string> { ["Retry-After"] = "Fri, 18 Sep 2026 10:00:30 GMT" };

        Assert.Equal(TimeSpan.FromSeconds(30), RetryAfterParser.Parse(headers, timeProvider: clock));
        Assert.Equal(TimeSpan.FromSeconds(30), RetryPolicy.Default.DelayFor(0, headers, clock));
    }

    /// <summary>A clock stopped at one instant, enough to prove which provider the parser consults.</summary>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task DefaultBackoffScheduleIsHalfASecondThenOneThenTwoSeconds()
    {
        var fake = new FakeTimeProvider(new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.Zero));
        var clock = new BackoffRecordingClock(fake);
        var attempts = new List<DateTimeOffset>();
        var handler = new StubHandler((_, attempt) =>
        {
            attempts.Add(fake.GetUtcNow());
            return attempt < 3 ? Http.Json(500, """{"error":"flaky"}""") : Http.Json(200, Http.SystemOneBody);
        });
        // The default backoff, with jitter off so the schedule is exact and one extra retry so the 2s step is reached.
        using var client = Clients.Create(handler, o =>
        {
            o.Retry = RetryPolicy.Default with { MaxRetries = 3, BackoffJitter = 0 };
            o.TimeProvider = clock;
        });

        var wall = Stopwatch.StartNew();
        var call = client.SystemOneAsync("x", Clients.SampleQuestions());
        await AdvanceThroughBackoffsAsync(fake, clock, call, 3);
        var response = await call;
        wall.Stop();

        var schedule = new[] { TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) };
        Assert.Equal("jev-latest", response.Model);
        Assert.Equal(4, handler.Requests.Count);
        Assert.Equal(schedule, clock.Delays);
        Assert.Equal(schedule, Deltas(attempts));
        Assert.Equal(TimeSpan.FromMilliseconds(3500), attempts[^1] - attempts[0]);
        AssertNoRealSleeping(wall.Elapsed);
    }

    [Fact]
    public async Task DefaultJitterKeepsEachBackoffWithinAQuarterOfTheSchedule()
    {
        var fake = new FakeTimeProvider(new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.Zero));
        var clock = new BackoffRecordingClock(fake);
        var attempts = new List<DateTimeOffset>();
        var handler = new StubHandler((_, _) =>
        {
            attempts.Add(fake.GetUtcNow());
            return Http.Json(503, """{"error":"down"}""");
        });
        using var client = Clients.Create(handler, o =>
        {
            o.Retry = RetryPolicy.Default;
            o.TimeProvider = clock;
        });

        var wall = Stopwatch.StartNew();
        var call = client.SystemOneAsync("x", Clients.SampleQuestions());
        await AdvanceThroughBackoffsAsync(fake, clock, call, 2);
        await Assert.ThrowsAsync<TypeSafeInternalServerException>(() => call);
        wall.Stop();

        // Jitter subtracts up to BackoffJitter (0.25) of each exponential step.
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(2, clock.Delays.Count);
        Assert.InRange(clock.Delays[0].TotalMilliseconds, 375, 500);
        Assert.InRange(clock.Delays[1].TotalMilliseconds, 750, 1000);
        Assert.Equal(clock.Delays, Deltas(attempts));
        AssertNoRealSleeping(wall.Elapsed);
    }

    [Fact]
    public async Task RetryAfterHttpDateIsWaitedOutOnTheFakeClock()
    {
        var fake = new FakeTimeProvider(new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.Zero));
        var clock = new BackoffRecordingClock(fake);
        var attempts = new List<DateTimeOffset>();
        var handler = new StubHandler((_, attempt) =>
        {
            var now = fake.GetUtcNow();
            attempts.Add(now);
            return attempt == 0
                ? Http.Json(429, "{}", ("Retry-After", now.AddSeconds(30).ToString("R", CultureInfo.InvariantCulture)))
                : Http.Json(200, Http.SystemOneBody);
        });
        using var client = Clients.Create(handler, o =>
        {
            o.Retry = RetryPolicy.Default;
            o.TimeProvider = clock;
        });

        var wall = Stopwatch.StartNew();
        var call = client.SystemOneAsync("x", Clients.SampleQuestions());
        await AdvanceThroughBackoffsAsync(fake, clock, call, 1);
        var response = await call;
        wall.Stop();

        Assert.Equal("jev-latest", response.Model);
        Assert.Equal(2, handler.Requests.Count);
        // The date is read against the injected clock, so the wait is the full 30s of fake time, not backoff.
        Assert.Equal(TimeSpan.FromSeconds(30), Assert.Single(clock.Delays));
        Assert.Equal(TimeSpan.FromSeconds(30), Assert.Single(Deltas(attempts)));
        AssertNoRealSleeping(wall.Elapsed);
    }

    /// <summary>
    /// Releases the next <paramref name="backoffs"/> retry sleeps: wait until the transport has armed
    /// its timer on the fake clock, then advance by exactly the delay it asked for, so no wall-clock
    /// time passes and the fake time between attempts is the backoff itself.
    /// </summary>
    private static async Task AdvanceThroughBackoffsAsync(FakeTimeProvider fake, BackoffRecordingClock clock, Task call, int backoffs)
    {
        for (var armed = 0; armed < backoffs; armed++)
        {
            var stopwatch = Stopwatch.StartNew();
            while (clock.Delays.Count <= armed && !call.IsCompleted)
            {
                if (stopwatch.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException($"Backoff {armed + 1} was never scheduled on the fake clock.");
                await Task.Delay(1);
            }
            // A call that finished early scheduled fewer sleeps than expected; the test's own assertions report it.
            if (clock.Delays.Count <= armed) return;
            fake.Advance(clock.Delays[armed]);
        }
    }

    private static TimeSpan[] Deltas(List<DateTimeOffset> instants) =>
        instants.Zip(instants.Skip(1), (earlier, later) => later - earlier).ToArray();

    private static void AssertNoRealSleeping(TimeSpan elapsed) =>
        Assert.True(elapsed < TimeSpan.FromSeconds(2), $"The fake-clocked retries slept for {elapsed.TotalMilliseconds:0}ms of real time.");

    /// <summary>
    /// A <see cref="FakeTimeProvider"/> that also records the due time of every timer armed against it.
    /// <c>Task.Delay(delay, provider, token)</c> arms one timer per sleep, so the recorded due times are
    /// the backoffs the retry policy chose, captured before any fake time is advanced.
    /// </summary>
    private sealed class BackoffRecordingClock(FakeTimeProvider inner) : TimeProvider
    {
        private readonly List<TimeSpan> _delays = new();

        public IReadOnlyList<TimeSpan> Delays
        {
            get { lock (_delays) return _delays.ToArray(); }
        }

        public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();

        public override long GetTimestamp() => inner.GetTimestamp();

        public override long TimestampFrequency => inner.TimestampFrequency;

        public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = inner.CreateTimer(callback, state, dueTime, period);
            lock (_delays) _delays.Add(dueTime);
            return timer;
        }
    }
}
