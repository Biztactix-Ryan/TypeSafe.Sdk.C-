using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TypeSafe;
using TypeSafe.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers <see cref="TypeSafeClient"/> in an <see cref="IServiceCollection"/> the way the rest of
/// .NET registers an API client: options bound from configuration, an <see cref="HttpClient"/> from
/// <see cref="IHttpClientFactory"/>, and an <see cref="IHttpClientBuilder"/> returned so the caller can
/// add resilience or logging handlers.
/// </summary>
public static class TypeSafeServiceCollectionExtensions
{
    /// <summary>The name of the <see cref="HttpClient"/> the client is built on.</summary>
    public const string HttpClientName = "TypeSafe";

    /// <summary>The configuration section bound when a configuration root is passed.</summary>
    public const string ConfigurationSectionName = "TypeSafe";

    /// <summary>
    /// Register <see cref="ITypeSafeClient"/> and <see cref="TypeSafeClient"/>, configured from code,
    /// from the <c>TypeSafe</c> configuration section if one was bound by another call, and from the
    /// <c>TYPESAFE_*</c> environment variables for anything still unset.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configure">
    /// Code overrides, applied after configuration binding so code wins. Options are an init-only
    /// record, so the callback returns a derived value rather than mutating one:
    /// <c>services.AddTypeSafeClient(o =&gt; o with { DefaultModel = "jev-1" })</c>. Setting
    /// <see cref="TypeSafeClientOptions.HttpClient"/> or <see cref="TypeSafeClientOptions.HttpMessageHandler"/>
    /// here is rejected, because the transport belongs to <see cref="IHttpClientFactory"/>: configure it
    /// on the returned <see cref="IHttpClientBuilder"/> instead.
    /// </param>
    /// <returns>The builder for the named <see cref="HttpClient"/>, for adding handlers to.</returns>
    /// <remarks>
    /// Both registrations are transient: each resolution is a new client wrapping a factory-managed
    /// <see cref="HttpClient"/>, which is the lifetime <see cref="IHttpClientFactory"/> is designed for.
    /// Disposing a resolved client does not dispose the factory's <see cref="HttpClient"/>. The client
    /// logs to the <see cref="ILoggerFactory"/> in the container when one is registered, and uses a
    /// registered <see cref="System.TimeProvider"/> for retry timing when one is registered.
    /// </remarks>
    public static IHttpClientBuilder AddTypeSafeClient(
        this IServiceCollection services,
        Func<TypeSafeClientOptions, TypeSafeClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<TypeSafeClientSettings>();
        // The SDK applies its own per-attempt timeout, so the HttpClient's must not interfere.
        var builder = services.AddHttpClient(
            HttpClientName,
            static http => http.Timeout = System.Threading.Timeout.InfiniteTimeSpan);

        services.TryAddTransient(provider => CreateClient(provider, configure));
        services.TryAddTransient<ITypeSafeClient>(static provider => provider.GetRequiredService<TypeSafeClient>());
        return builder;
    }

    /// <summary>
    /// Register <see cref="ITypeSafeClient"/> and <see cref="TypeSafeClient"/> with options bound from
    /// configuration: the given section, or its <c>TypeSafe</c> section when a configuration root is passed.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">The section to bind, or a configuration root whose <c>TypeSafe</c> section is bound.</param>
    /// <param name="configure">Code overrides, applied after binding so code wins. See the other overload.</param>
    /// <returns>The builder for the named <see cref="HttpClient"/>, for adding handlers to.</returns>
    public static IHttpClientBuilder AddTypeSafeClient(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<TypeSafeClientOptions, TypeSafeClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration is IConfigurationRoot
            ? configuration.GetSection(ConfigurationSectionName)
            : configuration;
        services.Configure<TypeSafeClientSettings>(section);
        return services.AddTypeSafeClient(configure);
    }

    /// <summary>Build a client from the bound settings, the code overrides, and a factory <see cref="HttpClient"/>.</summary>
    /// <exception cref="TypeSafeException">No API key was configured or the environment does not supply one.</exception>
    /// <exception cref="InvalidOperationException">The code overrides supply a transport that must come from <see cref="IHttpClientFactory"/>.</exception>
    private static TypeSafeClient CreateClient(
        IServiceProvider provider,
        Func<TypeSafeClientOptions, TypeSafeClientOptions>? configure)
    {
        var settings = provider.GetRequiredService<IOptionsMonitor<TypeSafeClientSettings>>().CurrentValue;
        var options = settings.ToOptions() with
        {
            Logger = provider.GetService<ILoggerFactory>()?.CreateLogger(typeof(TypeSafeClient).FullName!),
            TimeProvider = provider.GetService<TimeProvider>() ?? TimeProvider.System,
        };

        if (configure is not null)
        {
            options = configure(options)
                ?? throw new InvalidOperationException("The AddTypeSafeClient configure callback returned null.");
        }

        if (options.HttpMessageHandler is not null)
        {
            throw new InvalidOperationException(
                "TypeSafeClientOptions.HttpMessageHandler cannot be set through AddTypeSafeClient, because the HttpClient " +
                "comes from IHttpClientFactory. Call ConfigurePrimaryHttpMessageHandler(...) on the IHttpClientBuilder that " +
                "AddTypeSafeClient returns instead.");
        }

        if (options.HttpClient is not null)
        {
            throw new InvalidOperationException(
                "TypeSafeClientOptions.HttpClient cannot be set through AddTypeSafeClient, because the HttpClient comes " +
                $"from IHttpClientFactory under the name \"{HttpClientName}\". Configure it on the IHttpClientBuilder that " +
                "AddTypeSafeClient returns instead.");
        }

        var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
        // The factory owns the HttpClient and its handler chain, so the client must not dispose it.
        return new TypeSafeClient(options with { HttpClient = http, DisposeHttpClient = false });
    }
}
