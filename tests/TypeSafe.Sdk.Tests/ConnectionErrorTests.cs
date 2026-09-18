namespace TypeSafe.Tests;

public class ConnectionErrorTests
{
    [Fact]
    public async Task HttpRequestErrorIsCarriedOnTheConnectionException()
    {
        var handler = new StubHandler((_, _, _) => throw new HttpRequestException(HttpRequestError.NameResolutionError, "dns"));
        using var client = Clients.Create(handler, o => o.Retry = RetryPolicy.None);

        var error = await Assert.ThrowsAsync<TypeSafeApiConnectionException>(() => client.SystemOneAsync("x", Clients.SampleQuestions()));

        Assert.Equal(HttpRequestError.NameResolutionError, error.Error);
        Assert.Equal("Connection error: dns", error.Message);
    }

    [Fact]
    public async Task NonHttpFailuresCarryNoHttpRequestError()
    {
        var handler = new StubHandler((_, _, _) => throw new IOException("socket closed"));
        using var client = Clients.Create(handler, o => o.Retry = RetryPolicy.None);

        var error = await Assert.ThrowsAsync<TypeSafeApiConnectionException>(() => client.SystemOneAsync("x", Clients.SampleQuestions()));

        Assert.Null(error.Error);
        Assert.IsType<IOException>(error.InnerException);
    }

    [Fact]
    public async Task TimeoutsCarryNoHttpRequestError()
    {
        var handler = new StubHandler(async (_, _, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Http.Empty(200);
        });
        using var client = Clients.Create(handler, o =>
        {
            o.Retry = RetryPolicy.None;
            o.Timeout = TimeSpan.FromMilliseconds(50);
        });

        var error = await Assert.ThrowsAsync<TypeSafeApiTimeoutException>(() => client.SystemOneAsync("x", Clients.SampleQuestions()));

        Assert.Null(error.Error);
    }

    [Fact]
    public async Task TheRetryPredicateCanSelectOnTheHttpRequestError()
    {
        var retryDnsOnly = Clients.FastRetry with
        {
            RetryConnectionErrors = false,
            RetryWhen = e => e is TypeSafeApiConnectionException { Error: HttpRequestError.NameResolutionError },
        };

        var dns = new StubHandler((_, attempt, _) => attempt == 0
            ? throw new HttpRequestException(HttpRequestError.NameResolutionError, "dns")
            : Task.FromResult(Http.Json(200, Http.SystemOneBody)));
        using var retried = Clients.Create(dns, o => o.Retry = retryDnsOnly);
        await retried.SystemOneAsync("x", Clients.SampleQuestions());
        Assert.Equal(2, dns.Requests.Count);

        var reset = new StubHandler((_, _, _) => throw new HttpRequestException(HttpRequestError.ConnectionError, "reset"));
        using var notRetried = Clients.Create(reset, o => o.Retry = retryDnsOnly);
        var error = await Assert.ThrowsAsync<TypeSafeApiConnectionException>(() => notRetried.SystemOneAsync("x", Clients.SampleQuestions()));

        Assert.Equal(HttpRequestError.ConnectionError, error.Error);
        Assert.Single(reset.Requests);
    }
}
