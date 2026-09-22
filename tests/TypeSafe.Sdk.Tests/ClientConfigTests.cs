using Microsoft.Extensions.Logging;

namespace TypeSafe.Tests;

public class ClientConfigTests
{
    [Fact]
    public void MissingApiKeyThrowsNamingTheEnvironmentVariable()
    {
        using var env = EnvScope.Clean();
        var error = Assert.Throws<TypeSafeException>(() => new TypeSafeClient());
        Assert.Contains(TypeSafeConstants.ApiKeyEnv, error.Message);
    }

    [Fact]
    public void ApiKeyFallsBackToEnvironment()
    {
        using var env = EnvScope.Clean((TypeSafeConstants.ApiKeyEnv, "from-env"));
        using var client = new TypeSafeClient();
        Assert.Equal(TypeSafeConstants.DefaultBaseUrl, client.BaseUrl);
        Assert.Equal(TypeSafeConstants.DefaultModel, client.DefaultModel);
        Assert.Equal(TypeSafeConstants.DefaultTimeout, client.Timeout);
        Assert.Equal(LogLevel.Warning, client.LogLevel);
        Assert.Equal(RetryPolicy.Default, client.Retry);
    }

    [Fact]
    public void BlankEnvironmentValuesAreIgnored()
    {
        using var env = EnvScope.Clean((TypeSafeConstants.ApiKeyEnv, "   "));
        Assert.Throws<TypeSafeException>(() => new TypeSafeClient());
    }

    [Fact]
    public void ExplicitOptionsBeatEnvironment()
    {
        using var env = EnvScope.Clean(
            (TypeSafeConstants.ApiKeyEnv, "env-key"),
            (TypeSafeConstants.BaseUrlEnv, "https://env.example/"),
            (TypeSafeConstants.DefaultModelEnv, "env-model"),
            (TypeSafeConstants.LogLevelEnv, "debug"));
        using var client = new TypeSafeClient(new TypeSafeClientOptions
        {
            ApiKey = "code-key",
            BaseUrl = "https://code.example///",
            DefaultModel = "code-model",
            LogLevel = LogLevel.Error,
        });
        Assert.Equal("https://code.example", client.BaseUrl);
        Assert.Equal("code-model", client.DefaultModel);
        Assert.Equal(LogLevel.Error, client.LogLevel);
    }

    [Fact]
    public void EnvironmentSuppliesBaseUrlModelAndLogLevel()
    {
        using var env = EnvScope.Clean(
            (TypeSafeConstants.ApiKeyEnv, "env-key"),
            (TypeSafeConstants.BaseUrlEnv, " https://env.example/ "),
            (TypeSafeConstants.DefaultModelEnv, "env-model"),
            (TypeSafeConstants.LogLevelEnv, "info"));
        using var client = new TypeSafeClient();
        Assert.Equal("https://env.example", client.BaseUrl);
        Assert.Equal("env-model", client.DefaultModel);
        Assert.Equal(LogLevel.Information, client.LogLevel);
    }

    [Theory]
    [InlineData("debug", LogLevel.Debug)]
    [InlineData("INFO", LogLevel.Information)]
    [InlineData("warn", LogLevel.Warning)]
    [InlineData("warning", LogLevel.Warning)]
    [InlineData("error", LogLevel.Error)]
    [InlineData("off", LogLevel.None)]
    public void LogLevelNamesFromEnvironmentAreParsed(string name, LogLevel expected)
    {
        using var env = EnvScope.Clean((TypeSafeConstants.ApiKeyEnv, "k"), (TypeSafeConstants.LogLevelEnv, name));
        using var client = new TypeSafeClient();
        Assert.Equal(expected, client.LogLevel);
    }

    [Fact]
    public void InvalidLogLevelFromEnvironmentThrows()
    {
        using var env = EnvScope.Clean((TypeSafeConstants.ApiKeyEnv, "k"), (TypeSafeConstants.LogLevelEnv, "loud"));
        var error = Assert.Throws<TypeSafeException>(() => new TypeSafeClient());
        Assert.Contains("loud", error.Message);
        Assert.Contains(TypeSafeConstants.LogLevelEnv, error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NonPositiveTimeoutThrows(int seconds)
    {
        var error = Assert.Throws<TypeSafeException>(() =>
            new TypeSafeClient(new TypeSafeClientOptions { ApiKey = "k", Timeout = TimeSpan.FromSeconds(seconds) }));
        Assert.Contains("Timeout", error.Message);
    }

    [Fact]
    public void InfiniteTimeoutIsAllowed()
    {
        using var client = new TypeSafeClient(new TypeSafeClientOptions { ApiKey = "k", Timeout = Timeout.InfiniteTimeSpan });
        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
    }

    [Fact]
    public void SuppliedHttpClientTimeoutIsInherited()
    {
        using var http = new HttpClient(new StubHandler((_, _) => Http.Empty(200))) { Timeout = TimeSpan.FromSeconds(42) };
        using var client = new TypeSafeClient(new TypeSafeClientOptions { ApiKey = "k", HttpClient = http });
        Assert.Equal(TimeSpan.FromSeconds(42), client.Timeout);
    }

    [Fact]
    public void HttpClientAndHandlerAreMutuallyExclusive()
    {
        using var http = new HttpClient();
        Assert.Throws<ArgumentException>(() => new TypeSafeClient(new TypeSafeClientOptions
        {
            ApiKey = "k",
            HttpClient = http,
            HttpMessageHandler = new StubHandler((_, _) => Http.Empty(200)),
        }));
    }

    [Fact]
    public async Task SuppliedHttpClientIsNotDisposedByDefault()
    {
        var handler = new StubHandler((_, _) => Http.Json(200, Http.ModelsBody));
        var http = new HttpClient(handler);
        var client = new TypeSafeClient(new TypeSafeClientOptions { ApiKey = "k", HttpClient = http, LogLevel = LogLevel.None });
        client.Dispose();
        // Still usable: the SDK did not dispose it.
        using var response = await http.GetAsync("https://example.invalid/");
        Assert.Equal(200, (int)response.StatusCode);
    }

    [Fact]
    public async Task DisposedClientRefusesRequests()
    {
        var client = Clients.Create(new StubHandler((_, _) => Http.Json(200, Http.SystemOneBody)));
        client.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.SystemOneAsync("x", Clients.SampleQuestions()));
    }

    [Fact]
    public void DefaultHeadersAreCopied()
    {
        var headers = new Dictionary<string, string> { ["X-Team"] = "billing" };
        using var client = new TypeSafeClient(new TypeSafeClientOptions { ApiKey = "k", DefaultHeaders = headers });
        headers["X-Team"] = "changed";
        Assert.Equal("billing", client.DefaultHeaders["X-Team"]);
        Assert.Equal("billing", client.DefaultHeaders["x-team"]);
    }

    [Theory]
    [InlineData(-1, 0.25, 0)]
    [InlineData(2, 1.5, 0)]
    [InlineData(2, 0.25, 99)]
    public void InvalidRetryPolicyThrows(int maxRetries, double jitter, int status)
    {
        var policy = RetryPolicy.Default with
        {
            MaxRetries = maxRetries,
            BackoffJitter = jitter,
            HttpStatuses = status == 0 ? RetryPolicy.DefaultHttpStatuses : new HashSet<int> { status },
        };
        Assert.Throws<TypeSafeException>(() => new TypeSafeClient(new TypeSafeClientOptions { ApiKey = "k", Retry = policy }));
    }

    [Fact]
    public void WithKeepsTheOtherOptionValues()
    {
        var logger = new CapturingLogger();
        var options = new TypeSafeClientOptions
        {
            ApiKey = "k",
            BaseUrl = "https://code.example",
            DefaultModel = "code-model",
            Timeout = TimeSpan.FromSeconds(5),
            Logger = logger,
            LogLevel = LogLevel.Error,
        };

        var slower = options with { Timeout = TimeSpan.FromSeconds(30) };

        Assert.Equal(TimeSpan.FromSeconds(30), slower.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(5), options.Timeout);
        Assert.Equal("k", slower.ApiKey);
        Assert.Equal("https://code.example", slower.BaseUrl);
        Assert.Equal("code-model", slower.DefaultModel);
        Assert.Same(logger, slower.Logger);
        Assert.Equal(LogLevel.Error, slower.LogLevel);
        Assert.Same(options.TimeProvider, slower.TimeProvider);
        Assert.NotEqual(options, slower);
        Assert.Equal(options, options with { });
    }

    [Fact]
    public void ToStringDoesNotPrintTheApiKey()
    {
        var options = new TypeSafeClientOptions
        {
            ApiKey = "sk-super-secret-value",
            BaseUrl = "https://code.example",
            DefaultHeaders = new Dictionary<string, string> { ["X-Auth"] = "header-secret" },
        };

        var text = options.ToString();

        Assert.DoesNotContain("sk-super-secret-value", text, StringComparison.Ordinal);
        Assert.DoesNotContain("header-secret", text, StringComparison.Ordinal);
        Assert.Contains("[redacted]", text, StringComparison.Ordinal);
        Assert.Contains("https://code.example", text, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-super-secret-value", $"{options}", StringComparison.Ordinal);
    }
}
