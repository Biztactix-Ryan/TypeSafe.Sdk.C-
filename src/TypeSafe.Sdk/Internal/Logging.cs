using Microsoft.Extensions.Logging;

namespace TypeSafe.Internal;

/// <summary>Log level names accepted by <c>TYPESAFE_LOG_LEVEL</c> and their <see cref="LogLevel"/> equivalents.</summary>
internal static class LogLevelParser
{
    public const LogLevel Default = LogLevel.Warning;

    private static readonly Dictionary<string, LogLevel> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["trace"] = LogLevel.Trace,
        ["debug"] = LogLevel.Debug,
        ["info"] = LogLevel.Information,
        ["information"] = LogLevel.Information,
        ["warn"] = LogLevel.Warning,
        ["warning"] = LogLevel.Warning,
        ["error"] = LogLevel.Error,
        ["critical"] = LogLevel.Critical,
        ["off"] = LogLevel.None,
        ["none"] = LogLevel.None,
    };

    public static LogLevel Parse(string value, string source)
    {
        if (Names.TryGetValue(value.Trim(), out var level)) return level;
        throw new TypeSafeException(
            $"Invalid log level \"{value}\" from {source}. Expected one of: debug, info, warn, error, off.");
    }
}

/// <summary>A logger filtered to a minimum level that formats messages with the <c>[typesafe-sdk]</c> prefix.</summary>
internal sealed class SdkLog
{
    private readonly ILogger _logger;
    private readonly LogLevel _minimum;

    public SdkLog(ILogger logger, LogLevel minimum)
    {
        _logger = logger;
        _minimum = minimum;
    }

    public bool IsEnabled(LogLevel level) =>
        _minimum != LogLevel.None && level >= _minimum && _logger.IsEnabled(level);

    public void Debug(string message) => Write(LogLevel.Debug, message);
    public void Info(string message) => Write(LogLevel.Information, message);
    public void Warn(string message) => Write(LogLevel.Warning, message);
    public void Error(string message) => Write(LogLevel.Error, message);

    private void Write(LogLevel level, string message)
    {
        // Messages may contain JSON braces, so they are passed as a value rather than as a template.
        if (IsEnabled(level)) _logger.Log(level, "{Message}", message);
    }
}

/// <summary>Default logger writing prefixed lines to standard error.</summary>
internal sealed class ConsoleLogger : ILogger
{
    public static readonly ConsoleLogger Instance = new();

    private const string Prefix = "[typesafe-sdk]";

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var text = formatter(state, exception);
        Console.Error.WriteLine($"{Prefix} {Label(logLevel)} {text}");
    }

    private static string Label(LogLevel level) => level switch
    {
        LogLevel.Trace => "trace",
        LogLevel.Debug => "debug",
        LogLevel.Information => "info",
        LogLevel.Warning => "warn",
        LogLevel.Error => "error",
        LogLevel.Critical => "critical",
        _ => "log",
    };
}

/// <summary>Credential redaction for logged headers.</summary>
internal static class Redaction
{
    /// <summary>Credential headers that retain a key suffix for identification.</summary>
    private static readonly HashSet<string> KeyHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization",
        "proxy-authorization",
        "x-api-key",
        "api-key",
    };

    /// <summary>Headers whose values are redacted in full.</summary>
    private static readonly HashSet<string> OpaqueHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "cookie",
        "set-cookie",
    };

    public static string Redact(string name, string value)
    {
        if (KeyHeaders.Contains(name)) return RedactKey(value);
        var lower = name.ToLowerInvariant();
        if (OpaqueHeaders.Contains(name) || lower.Contains("token") || lower.Contains("secret")) return "***";
        return value;
    }

    /// <summary>Mask a key, preserving its scheme and the last four characters of secrets longer than eight.</summary>
    private static string RedactKey(string value)
    {
        string? scheme = null;
        var secret = value;
        var space = value.IndexOf(' ');
        if (space >= 0)
        {
            scheme = value[..space];
            secret = value[(space + 1)..].TrimStart();
        }
        var tail = secret.Length > 8 ? secret[^4..] : "";
        return scheme is null ? $"***{tail}" : $"{scheme} ***{tail}";
    }

    /// <summary>Format headers for a log line with credential values redacted.</summary>
    public static string Describe(IEnumerable<KeyValuePair<string, string>> headers) =>
        "{" + string.Join(", ", headers.Select(h => $"{h.Key}: {Redact(h.Key, h.Value)}")) + "}";
}
