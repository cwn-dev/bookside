using Bookside.UI.Layout;
using Microsoft.Extensions.DependencyInjection;

namespace Bookside.UI.Tests.Components;

public sealed class MainLayoutTests : ComponentTestBase
{
    [Fact]
    public void Vocabulary_submenu_links_to_review_and_toggles_on_tap()
    {
        var layout = Render<MainLayout>();
        var toggle = layout.Find(".nav-toggle");

        Assert.Equal("words/review", layout.Find(".submenu a").GetAttribute("href"));
        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));

        toggle.Click();
        Assert.Equal("true", layout.Find(".nav-toggle").GetAttribute("aria-expanded"));
        Assert.Contains("open", layout.Find(".submenu").ClassList);

        layout.Find(".nav-backdrop").Click();
        Assert.Equal("false", layout.Find(".nav-toggle").GetAttribute("aria-expanded"));
        Assert.Empty(layout.FindAll(".nav-backdrop"));
    }

    [Fact]
    public void Header_has_the_global_search_box()
    {
        var layout = Render<MainLayout>();

        Assert.Equal("search", layout.Find(".site-header .global-search").GetAttribute("role"));
    }

    [Fact]
    public void Footer_shows_copyright_and_dev_version_without_a_link()
    {
        var footer = Render<MainLayout>().Find(".site-footer");

        Assert.Contains(
            "© " + AppInfo.CopyrightYears(TimeProvider.System.GetLocalNow().Year),
            footer.TextContent,
            StringComparison.Ordinal);
        Assert.Equal("n7e", footer.QuerySelector(".company")!.TextContent);
        Assert.Equal(
            "https://github.com/cwn-dev/bookside",
            footer.QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal("dev", footer.QuerySelector(".version")!.TextContent);
        Assert.Null(footer.QuerySelector("a.version"));
    }
}

public sealed class MainLayoutReleaseTests : ComponentTestBase
{
    public MainLayoutReleaseTests() => Services.AddSingleton(new AppInfo("1.6.1"));

    [Fact]
    public void Footer_links_a_release_version_to_its_release_page()
    {
        var footer = Render<MainLayout>().Find(".site-footer");
        var version = footer.QuerySelector("a.version")!;

        var text = string.Join(
            ' ',
            footer.TextContent.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));

        Assert.EndsWith(" n7e · Bookside v1.6.1", text, StringComparison.Ordinal);

        Assert.Equal("v1.6.1", version.TextContent);
        Assert.Equal(
            "https://github.com/cwn-dev/bookside/releases/tag/v1.6.1",
            version.GetAttribute("href"));
    }
}
