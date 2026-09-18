using TypeSafe.Internal;

namespace TypeSafe;

/// <summary>Access to the models available to the account, reached through <see cref="TypeSafeClient.Models"/>.</summary>
public sealed class ModelsResource : IModelsResource
{
    private readonly Transport _transport;

    internal ModelsResource(Transport transport)
    {
        _transport = transport;
    }

    /// <summary>List the models available to the account.</summary>
    /// <param name="options">Per-call timeout, retry, and header overrides.</param>
    /// <param name="cancellationToken">Cancels the request and any pending retries.</param>
    /// <returns>
    /// The available models as an <see cref="IReadOnlyList{T}"/> of <see cref="ModelMetadata"/>, so the
    /// response can be enumerated or indexed directly: <c>foreach (var model in await client.Models.ListAsync())</c>.
    /// Each entry carries the model's name, description, and release date.
    /// </returns>
    /// <exception cref="TypeSafeApiException">The server returns an unsuccessful HTTP response after any retries.</exception>
    /// <exception cref="TypeSafeApiConnectionException">The request cannot connect or times out after any retries.</exception>
    public Task<ListModelsResponse> ListAsync(RequestOptions? options = null, CancellationToken cancellationToken = default) =>
        _transport.SendAsync(
            HttpMethod.Get,
            Protocol.ModelsPath,
            null,
            options,
            TypeSafeJsonContext.Default.ModelList,
            ListModelsResponse.FromBody,
            cancellationToken);
}
