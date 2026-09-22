using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TypeSafe.DependencyInjection.Tests;

public class RegistrationTests
{
    [Fact]
    public void RegistrationResolvesBothClientTypes()
    {
        using var env = EnvScope.Clean();
        var services = new ServiceCollection();

        services.AddTypeSafeClient(options => options with { ApiKey = "sk-test" });

        using var provider = services.BuildServiceProvider();
        var fromInterface = provider.GetRequiredService<ITypeSafeClient>();
        var fromClass = provider.GetRequiredService<TypeSafeClient>();

        Assert.IsType<TypeSafeClient>(fromInterface);
        Assert.Equal(TypeSafeConstants.DefaultBaseUrl, fromClass.BaseUrl);
    }

    [Fact]
    public void ClientsAreTransient()
    {
        using var env = EnvScope.Clean();
        var services = new ServiceCollection();
        services.AddTypeSafeClient(options => options with { ApiKey = "sk-test" });

        using var provider = services.BuildServiceProvider();

        Assert.NotSame(provider.GetRequiredService<ITypeSafeClient>(), provider.GetRequiredService<ITypeSafeClient>());
    }

    [Fact]
    public void AddTypeSafeClientReturnsTheNamedHttpClientBuilder()
    {
        var services = new ServiceCollection();

        var builder = services.AddTypeSafeClient();

        Assert.Equal(TypeSafeServiceCollectionExtensions.HttpClientName, builder.Name);
    }

    [Fact]
    public void MissingApiKeySurfacesTypeSafeException()
    {
        using var env = EnvScope.Clean();
        var services = new ServiceCollection();
        services.AddTypeSafeClient();

        using var provider = services.BuildServiceProvider();

        var error = Assert.Throws<TypeSafeException>(() => provider.GetRequiredService<ITypeSafeClient>());
        Assert.Contains(TypeSafeConstants.ApiKeyEnv, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvironmentVariablesStillConfigureTheClient()
    {
        using var env = EnvScope.Clean(
            (TypeSafeConstants.ApiKeyEnv, "sk-env"),
            (TypeSafeConstants.DefaultModelEnv, "from-env"));
        var services = new ServiceCollection();
        services.AddTypeSafeClient();

        using var provider = services.BuildServiceProvider();

        Assert.Equal("from-env", provider.GetRequiredService<TypeSafeClient>().DefaultModel);
    }

    [Fact]
    public void AnHttpMessageHandlerInTheCodeOverridesIsRejected()
    {
        using var env = EnvScope.Clean();
        var services = new ServiceCollection();
        services.AddTypeSafeClient(options => options with { ApiKey = "sk-test", HttpMessageHandler = new StubHandler() });

        using var provider = services.BuildServiceProvider();

        var error = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ITypeSafeClient>());
        Assert.Contains("ConfigurePrimaryHttpMessageHandler", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnHttpClientInTheCodeOverridesIsRejected()
    {
        using var env = EnvScope.Clean();
        using var http = new HttpClient();
        var services = new ServiceCollection();
        services.AddTypeSafeClient(options => options with { ApiKey = "sk-test", HttpClient = http });

        using var provider = services.BuildServiceProvider();

        var error = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ITypeSafeClient>());
        Assert.Contains("IHttpClientFactory", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheClientLogsToTheLoggerFactoryInTheContainer()
    {
        using var env = EnvScope.Clean();
        var logger = new NullLogger();
        var factory = new StubLoggerFactory(logger);
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(factory);
        services.AddTypeSafeClient(options => options with { ApiKey = "sk-test" });

        using var provider = services.BuildServiceProvider();

        Assert.Same(logger, provider.GetRequiredService<TypeSafeClient>().Logger);
        Assert.Contains("TypeSafe.TypeSafeClient", factory.Categories);
    }

    [Fact]
    public async Task AHandlerAddedThroughTheReturnedBuilderRuns()
    {
        using var env = EnvScope.Clean();
        var stub = new StubHandler();
        var counter = new Counter();
        var services = new ServiceCollection();
        services
            .AddTypeSafeClient(options => options with { ApiKey = "sk-test", BaseUrl = "https://stub.example" })
            .ConfigurePrimaryHttpMessageHandler(() => stub)
            .AddHttpMessageHandler(() => new CountingHandler(counter));

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ITypeSafeClient>();
        var models = await client.Models.ListAsync();

        Assert.Equal(2, models.Count);
        Assert.Equal("https://stub.example/v1/models", Assert.Single(stub.Requests).ToString());
        Assert.Equal(1, counter.Count);
    }

    [Fact]
    public async Task TheFactoryHttpClientIsNotDisposedWithTheClient()
    {
        using var env = EnvScope.Clean();
        var stub = new StubHandler();
        var factory = new RecordingHttpClientFactory(stub);
        var services = new ServiceCollection();
        services.AddTypeSafeClient(options => options with { ApiKey = "sk-test", BaseUrl = "https://stub.example" });
        // Replacing the factory keeps a reference to the exact HttpClient the client was built on.
        services.AddSingleton<IHttpClientFactory>(factory);

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<TypeSafeClient>();
        var http = Assert.Single(factory.Created);
        Assert.Equal(TypeSafeServiceCollectionExtensions.HttpClientName, Assert.Single(factory.Names));

        client.Dispose();

        // A disposed HttpClient throws ObjectDisposedException here instead of reaching the stub.
        using var response = await http.GetAsync("https://stub.example/v1/models");
        Assert.True(response.IsSuccessStatusCode);
    }
}
