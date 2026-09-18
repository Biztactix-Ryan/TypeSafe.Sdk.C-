namespace TypeSafe;

/// <summary>Public environment-variable names and client defaults.</summary>
public static class TypeSafeConstants
{
    /// <summary>Environment variable for the API key.</summary>
    public const string ApiKeyEnv = "TYPESAFE_API_KEY";

    /// <summary>Environment variable for the API base URL.</summary>
    public const string BaseUrlEnv = "TYPESAFE_BASE_URL";

    /// <summary>Environment variable for the default model.</summary>
    public const string DefaultModelEnv = "TYPESAFE_DEFAULT_MODEL";

    /// <summary>Environment variable for the logging level.</summary>
    public const string LogLevelEnv = "TYPESAFE_LOG_LEVEL";

    /// <summary>Default API base URL.</summary>
    public const string DefaultBaseUrl = "https://api.typesafe.ai";

    /// <summary>Default model name.</summary>
    public const string DefaultModel = "jev-latest";

    /// <summary>Default timeout for each HTTP attempt.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    /// <summary>The SDK version reported in the <c>User-Agent</c> and <c>X-TypeSafe-SDK</c> headers.</summary>
    public const string Version = "0.7.0";

    /// <summary>Response header carrying the server-assigned request ID.</summary>
    public const string RequestIdHeader = "x-typesafe-request-id";
}

/// <summary>Internal protocol constants shared across the transport.</summary>
internal static class Protocol
{
    public const string SdkName = "typesafe-sdk";
    public const string SystemOnePath = "/v1/systemone";
    public const string ModelsPath = "/v1/models";
    public const string JsonContentType = "application/json";

    public const string AuthorizationHeader = "Authorization";
    public const string AcceptHeader = "Accept";
    public const string ContentTypeHeader = "Content-Type";
    public const string UserAgentHeader = "User-Agent";
    public const string SdkHeader = "X-TypeSafe-SDK";
    public const string RuntimeHeader = "X-TypeSafe-Runtime";
    public const string RetryCountHeader = "X-TypeSafe-Retry-Count";
    public const string RetryAfterHeader = "retry-after";
    public const string RetryAfterMsHeader = "retry-after-ms";

    public const int MaxErrorBodyLength = 200;

    /// <summary>Headers the SDK owns; user-supplied values for these names are dropped.</summary>
    public static readonly HashSet<string> ProtectedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        AuthorizationHeader,
        AcceptHeader,
        ContentTypeHeader,
        UserAgentHeader,
        SdkHeader,
        RuntimeHeader,
        RetryCountHeader,
    };
}
