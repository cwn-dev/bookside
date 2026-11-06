using System.Globalization;
using System.Reflection;

namespace Bookside.UI;

/// <summary>
/// The app's version and copyright details, shown in the footer.
/// </summary>
/// <param name="Version">
/// Release number, e.g. "1.6.1"; null for a dev build.
/// </param>
public sealed record AppInfo(string? Version)
{
    public const int StartYear = 2026;

    public const string Company = "n7e";

    public const string RepositoryUrl = "https://github.com/cwn-dev/bookside";

    /// <summary>
    /// The GitHub release page for <see cref="Version"/> (tags are "v1.6.1").
    /// </summary>
    public string? ReleaseUrl =>
        Version is null ? null : $"{RepositoryUrl}/releases/tag/v{Version}";

    /// <summary>
    /// "2026" in the first year, then "2026–2027" and so on.
    /// </summary>
    public static string CopyrightYears(int currentYear) =>
        currentYear > StartYear
            ? $"{StartYear}–{currentYear.ToString(CultureInfo.InvariantCulture)}"
            : StartYear.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads the informational version set by the build (-p:Version=…).
    /// The default "0.0.0-dev" (Directory.Build.props) means a dev build.
    /// </summary>
    public static AppInfo FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        var isDev = string.IsNullOrWhiteSpace(version)
            || version.EndsWith("-dev", StringComparison.Ordinal);

        return new AppInfo(isDev ? null : version);
    }
}
