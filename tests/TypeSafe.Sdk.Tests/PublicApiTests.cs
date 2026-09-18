using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DiffEngine;
using PublicApiGenerator;
using VerifyXunit;

namespace TypeSafe.Tests;

/// <summary>
/// Snapshots the whole public surface of the SDK assembly. Any change to a public signature makes
/// this test fail until the snapshot is re-approved, so breaking changes are deliberate and reviewed.
/// See CONTRIBUTING.md for how to review and accept a snapshot change.
/// </summary>
public partial class PublicApiTests
{
    /// <summary>
    /// Runs before any test in this assembly. Keeps Verify non-interactive: a failing snapshot writes
    /// <c>*.received.txt</c> next to the approved file instead of popping a diff tool open, so
    /// <c>dotnet test</c> in a bare shell — or in CI — needs no environment variables.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize() => DiffRunner.Disabled = true;

    /// <summary>
    /// Assembly-level attributes are excluded wholesale: they carry the assembly, file and
    /// informational versions, which a release bump changes without touching a single public
    /// signature, plus <c>InternalsVisibleTo</c> and the compiler's own bookkeeping.
    /// </summary>
    private static readonly ApiGeneratorOptions Options = new()
    {
        IncludeAssemblyAttributes = false,
    };

    /// <summary>
    /// Matches the declared value of <see cref="TypeSafeConstants.Version"/>. The constant is part of
    /// the surface and stays in the snapshot; only its value is scrubbed, for the same reason the
    /// assembly attributes are dropped — bumping the version is not an API change.
    /// </summary>
    [GeneratedRegex("""^(?<prefix>\s*public const string Version = )"[^"]*";$""")]
    private static partial Regex VersionConstant();

    [Fact]
    public Task PublicApi()
    {
        var publicApi = typeof(Content).Assembly.GeneratePublicApi(Options);

        var settings = new VerifySettings();
        settings.ScrubLinesWithReplace(line => VersionConstant().Replace(line, """${prefix}"{scrubbed version}";"""));

        return Verifier.Verify(publicApi, settings);
    }
}
