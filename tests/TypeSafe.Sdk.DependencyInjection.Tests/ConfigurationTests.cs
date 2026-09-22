using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TypeSafe.DependencyInjection.Tests;

public class ConfigurationTests
{
    private static IConfigurationRoot Config(params (string Key, string Value)[] entries) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(entries.ToDictionary(entry => entry.Key, entry => (string?)entry.Value))
            .Build();

    [Fact]
    public void AConfigurationSectionConfiguresTheClientAndCodeOverridesWin()
    {
        using var env = EnvScope.Clean();
        var configuration = Config(
            ("TypeSafe:ApiKey", "sk-config"),
            ("TypeSafe:BaseUrl", "https://config.example/"),
            ("TypeSafe:DefaultModel", "from-config"));
        var services = new ServiceCollection();

        services.AddTypeSafeClient(
            configuration.GetSection("TypeSafe"),
            options => options with { DefaultModel = "from-code" });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<TypeSafeClient>();

        Assert.Equal("https://config.example", client.BaseUrl);
        Assert.Equal("from-code", client.DefaultModel);
    }

    [Fact]
    public void AConfigurationRootBindsTheTypeSafeSection()
    {
        using var env = EnvScope.Clean();
        var configuration = Config(
            ("TypeSafe:ApiKey", "sk-config"),
            ("TypeSafe:BaseUrl", "https://root.example"),
            ("TypeSafe:DefaultModel", "from-root"));
        var services = new ServiceCollection();

        services.AddTypeSafeClient(configuration);

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<TypeSafeClient>();

        Assert.Equal("https://root.example", client.BaseUrl);
        Assert.Equal("from-root", client.DefaultModel);
    }

    [Fact]
    public void CodeOverridesWinOverEnvironmentVariables()
    {
        using var env = EnvScope.Clean(
            (TypeSafeConstants.ApiKeyEnv, "sk-env"),
            (TypeSafeConstants.DefaultModelEnv, "from-env"));
        var configuration = Config(("TypeSafe:DefaultModel", "from-config"));
        var services = new ServiceCollection();

        services.AddTypeSafeClient(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.Equal("from-config", provider.GetRequiredService<TypeSafeClient>().DefaultModel);
    }

    [Fact]
    public void TimeoutRetryHeadersAndLogLevelBindFromConfiguration()
    {
        using var env = EnvScope.Clean();
        var configuration = Config(
            ("TypeSafe:ApiKey", "sk-config"),
            ("TypeSafe:Timeout", "00:00:30"),
            ("TypeSafe:LogLevel", "Debug"),
            ("TypeSafe:DefaultHeaders:X-Tenant", "acme"),
            ("TypeSafe:Retry:MaxRetries", "5"),
            ("TypeSafe:Retry:BackoffMax", "00:00:02"),
            ("TypeSafe:Retry:HttpStatuses:0", "503"));
        var services = new ServiceCollection();

        services.AddTypeSafeClient(configuration);

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<TypeSafeClient>();

        Assert.Equal(TimeSpan.FromSeconds(30), client.Timeout);
        Assert.Equal(LogLevel.Debug, client.LogLevel);
        Assert.Equal("acme", client.DefaultHeaders["X-Tenant"]);
        Assert.Equal(5, client.Retry.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(2), client.Retry.BackoffMax);
        Assert.Equal(new[] { 503 }, client.Retry.HttpStatuses);
    }

    [Fact]
    public void AnUnsetTimeoutKeepsTheSdkDefaultRatherThanTheHttpClientTimeout()
    {
        using var env = EnvScope.Clean();
        var services = new ServiceCollection();
        services.AddTypeSafeClient(options => options with { ApiKey = "sk-test" });

        using var provider = services.BuildServiceProvider();

        Assert.Equal(TypeSafeConstants.DefaultTimeout, provider.GetRequiredService<TypeSafeClient>().Timeout);
    }
}
