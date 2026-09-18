using System.Runtime.InteropServices;

namespace TypeSafe.Internal;

/// <summary>Runtime description for the <c>X-TypeSafe-Runtime</c> header, cached for the process lifetime.</summary>
internal static class RuntimeInfo
{
    public static readonly string Description = Describe();

    private static string Describe()
    {
        var platform =
            OperatingSystem.IsWindows() ? "windows" :
            OperatingSystem.IsMacOS() ? "macos" :
            OperatingSystem.IsLinux() ? "linux" :
            OperatingSystem.IsFreeBSD() ? "freebsd" :
            OperatingSystem.IsAndroid() ? "android" :
            OperatingSystem.IsIOS() ? "ios" :
            OperatingSystem.IsBrowser() ? "browser" :
            "unknown";
        var arch = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
        return $"dotnet/{Environment.Version} ({platform}; {arch})";
    }
}
