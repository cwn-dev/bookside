using System.Reflection;
using System.Reflection.Emit;

namespace Bookside.UI.Tests;

public sealed class AppInfoTests
{
    [Theory]
    [InlineData(2025, "2026")]
    [InlineData(2026, "2026")]
    [InlineData(2027, "2026–2027")]
    public void Copyright_years_start_in_2026(int currentYear, string expected) =>
        Assert.Equal(expected, AppInfo.CopyrightYears(currentYear));

    [Theory]
    [InlineData("1.6.1", "1.6.1")]
    [InlineData("1.5.0-beta1", "1.5.0-beta1")]
    [InlineData("0.0.0-dev", null)]
    public void Version_comes_from_the_informational_version(
        string informationalVersion,
        string? expected) =>
            Assert.Equal(
                expected,
                AppInfo.FromAssembly(AssemblyWithVersion(informationalVersion)).Version);

    [Fact]
    public void An_assembly_without_a_version_is_a_dev_build() =>
        Assert.Null(AppInfo.FromAssembly(AssemblyWithVersion(null)).Version);

    [Fact]
    public void Local_builds_are_dev_builds() =>
        Assert.Null(AppInfo.FromAssembly(typeof(AppInfo).Assembly).Version);

    [Fact]
    public void Release_url_points_at_the_github_tag()
    {
        Assert.Equal(
            "https://github.com/cwn-dev/bookside/releases/tag/v1.6.1",
            new AppInfo("1.6.1").ReleaseUrl);
        Assert.Null(new AppInfo(null).ReleaseUrl);
    }

    private static AssemblyBuilder AssemblyWithVersion(string? informationalVersion)
    {
        var constructor = typeof(AssemblyInformationalVersionAttribute)
            .GetConstructor([typeof(string)])!;
        CustomAttributeBuilder[] attributes = informationalVersion is null
            ? []
            : [new CustomAttributeBuilder(constructor, [informationalVersion])];

        return AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Versioned" + Guid.NewGuid().ToString("N")),
            AssemblyBuilderAccess.Run,
            attributes);
    }
}
