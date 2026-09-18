using System.Net;
using System.Text.Json.Nodes;

namespace TypeSafe.Tests;

public class ErrorTests
{
    private static async Task<TypeSafeApiException> Fail(HttpResponseMessage response)
    {
        using var client = Clients.Create(new StubHandler((_, _) => response), o => o.Retry = RetryPolicy.None);
        return await Assert.ThrowsAnyAsync<TypeSafeApiException>(() => client.SystemOneAsync("x", Clients.SampleQuestions()));
    }

    [Theory]
    [InlineData(400, typeof(TypeSafeBadRequestException))]
    [InlineData(401, typeof(TypeSafeAuthenticationException))]
    [InlineData(403, typeof(TypeSafePermissionDeniedException))]
    [InlineData(404, typeof(TypeSafeNotFoundException))]
    [InlineData(422, typeof(TypeSafeUnprocessableEntityException))]
    [InlineData(429, typeof(TypeSafeRateLimitException))]
    [InlineData(500, typeof(TypeSafeInternalServerException))]
    [InlineData(503, typeof(TypeSafeInternalServerException))]
    [InlineData(418, typeof(TypeSafeApiException))]
    public async Task StatusCodesMapToExceptionTypes(int status, Type expected)
    {
        var error = await Fail(Http.Json(status, """{"error":"nope"}"""));
        Assert.IsType(expected, error);
        Assert.Equal("nope", error.Detail);
        Assert.Equal((HttpStatusCode)status, error.StatusCode);
    }

    [Fact]
    public async Task MessageIncludesEndpointStatusDetailAndRequestId()
    {
        var error = await Fail(Http.Json(401, """{"error":{"message":"bad key"}}""", ("x-typesafe-request-id", "req-9")));
        Assert.Equal("POST https://api.typesafe.ai/v1/systemone: 401 bad key (request_id=req-9)", error.Message);
        Assert.Equal("req-9", error.RequestId);
        Assert.Equal("POST https://api.typesafe.ai/v1/systemone", error.Endpoint);
        Assert.Equal("bad key", (string?)error.Body!["error"]!["message"]);
        Assert.Equal("req-9", error.Headers["x-typesafe-request-id"]);
    }

    [Theory]
    [InlineData("""{"message":"top-level message"}""", "top-level message")]
    [InlineData("""{"detail":"detail text"}""", "detail text")]
    [InlineData("""{"detail":{"message":"nested detail"}}""", "nested detail")]
    [InlineData("""{"error":"error text","message":"ignored"}""", "error text")]
    [InlineData("""{"unrelated":true}""", "{\"unrelated\":true}")]
    public async Task MessageIsExtractedFromKnownBodyShapes(string body, string expectedDetail)
    {
        var error = await Fail(Http.Json(400, body));
        Assert.Equal(expectedDetail, error.Detail);
    }

    [Fact]
    public async Task ValidationDetailListsAreFormatted()
    {
        const string body = """
            {"detail":[
              {"loc":["body","questions","tone","criteria"],"msg":"field required","type":"missing"},
              {"loc":["body","state"],"msg":"invalid state","type":"value_error"},
              {"msg":"no location"},
              {"loc":["body"],"type":"no msg"}
            ]}
            """;
        var error = await Fail(Http.Json(422, body));
        Assert.Equal("questions.tone.criteria: field required; state: invalid state; no location", error.Detail);
    }

    [Fact]
    public async Task PlainTextAndEmptyBodiesProduceMessages()
    {
        var text = await Fail(Http.Text(502, "Bad Gateway"));
        Assert.Equal("Bad Gateway", text.Detail);
        Assert.Equal("Bad Gateway", (string?)text.Body);

        var empty = await Fail(Http.Empty(500));
        Assert.Equal("status code (no body)", empty.Detail);
        Assert.Null(empty.Body);
    }

    [Fact]
    public async Task LongRawBodiesAreTruncated()
    {
        var body = "{\"data\":\"" + new string('x', 400) + "\"}";
        var error = await Fail(Http.Json(400, body));
        Assert.Equal(201, error.Detail.Length);
        Assert.EndsWith("…", error.Detail);
    }

    [Fact]
    public async Task RateLimitExposesRetryAfter()
    {
        var ms = await Fail(Http.Json(429, "{}", ("retry-after-ms", "1500"), ("retry-after", "10")));
        Assert.Equal(TimeSpan.FromMilliseconds(1500), Assert.IsType<TypeSafeRateLimitException>(ms).RetryAfter);

        var seconds = await Fail(Http.Json(429, "{}", ("Retry-After", "3")));
        Assert.Equal(TimeSpan.FromSeconds(3), Assert.IsType<TypeSafeRateLimitException>(seconds).RetryAfter);

        var none = await Fail(Http.Json(429, "{}"));
        Assert.Null(Assert.IsType<TypeSafeRateLimitException>(none).RetryAfter);
    }

    [Fact]
    public async Task ConnectionFailuresBecomeConnectionExceptions()
    {
        var handler = new StubHandler((_, _, _) => throw new HttpRequestException("boom"));
        using var client = Clients.Create(handler, o => o.Retry = RetryPolicy.None);

        var error = await Assert.ThrowsAsync<TypeSafeApiConnectionException>(() => client.SystemOneAsync("x", Clients.SampleQuestions()));

        Assert.Equal("Connection error: boom", error.Message);
        Assert.IsType<HttpRequestException>(error.InnerException);
    }

    [Fact]
    public async Task TimeoutsBecomeTimeoutExceptions()
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

        Assert.Equal(TimeSpan.FromMilliseconds(50), error.Timeout);
        Assert.Equal("Request timed out after 50ms.", error.Message);
        Assert.IsAssignableFrom<TypeSafeApiConnectionException>(error);
    }

    [Fact]
    public async Task PerCallTimeoutOverridesClientTimeout()
    {
        var handler = new StubHandler(async (_, _, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Http.Empty(200);
        });
        using var client = Clients.Create(handler, o => o.Retry = RetryPolicy.None);

        var error = await Assert.ThrowsAsync<TypeSafeApiTimeoutException>(() =>
            client.SystemOneAsync("x", Clients.SampleQuestions(), options: new RequestOptions { Timeout = TimeSpan.FromMilliseconds(20) }));
        Assert.Equal(TimeSpan.FromMilliseconds(20), error.Timeout);

        await Assert.ThrowsAsync<TypeSafeException>(() =>
            client.SystemOneAsync("x", Clients.SampleQuestions(), options: new RequestOptions { Timeout = TimeSpan.Zero }));
    }

    [Fact]
    public async Task CallerCancellationSurfacesAsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        var handler = new StubHandler(async (_, _, ct) =>
        {
            cts.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Http.Empty(200);
        });
        using var client = Clients.Create(handler);

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.SystemOneAsync("x", Clients.SampleQuestions(), cancellationToken: cts.Token));

        Assert.Equal(cts.Token, error.CancellationToken);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task PreCancelledTokenNeverSendsARequest()
    {
        var handler = new StubHandler((_, _) => Http.Json(200, Http.SystemOneBody));
        using var client = Clients.Create(handler);
        var token = new CancellationToken(canceled: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.SystemOneAsync("x", Clients.SampleQuestions(), cancellationToken: token));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void ExceptionsCanBeConstructedDirectly()
    {
        var error = TypeSafeApiException.FromResponse(HttpStatusCode.NotFound, JsonNode.Parse("""{"error":"missing"}"""));
        Assert.IsType<TypeSafeNotFoundException>(error);
        Assert.Equal("404 missing", error.Message);
        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
        Assert.Null(error.RequestId);
        Assert.Empty(error.Headers);

        var custom = new TypeSafeApiException(HttpStatusCode.BadRequest, null, message: "custom");
        Assert.Equal("400 custom", custom.Message);
        Assert.Equal("custom", custom.Detail);
    }
}
