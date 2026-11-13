using Bookside.UI.Layout;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Bookside.UI.Tests.Components;

public sealed class MainLayoutTests : ComponentTestBase
{
    [Theory]
    [InlineData(0, "books/series")]
    [InlineData(1, "words/review")]
    public void Submenus_link_to_sub_pages_and_toggle_on_tap(int group, string href)
    {
        var layout = Render<MainLayout>();
        var toggle = layout.FindAll(".nav-toggle")[group];

        Assert.Equal(href, layout.FindAll(".submenu a")[group].GetAttribute("href"));
        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));

        toggle.Click();
        Assert.Equal("true", layout.FindAll(".nav-toggle")[group].GetAttribute("aria-expanded"));
        Assert.Contains("open", layout.FindAll(".submenu")[group].ClassList);

        layout.Find(".nav-backdrop").Click();
        Assert.Equal("false", layout.FindAll(".nav-toggle")[group].GetAttribute("aria-expanded"));
        Assert.Empty(layout.FindAll(".nav-backdrop"));
    }

    [Fact]
    public void Nav_links_to_the_activity_calendar()
    {
        var layout = Render<MainLayout>();

        Assert.Contains(
            layout.FindAll("nav .nav-link"),
            a => a.GetAttribute("href") == "activity" && a.TextContent == "Activity");
    }

    [Fact]
    public void Burger_menu_opens_and_closes_on_backdrop_and_escape()
    {
        var layout = Render<MainLayout>();

        layout.Find(".menu-toggle").Click();
        Assert.Equal("true", layout.Find(".menu-toggle").GetAttribute("aria-expanded"));
        Assert.Contains("open", layout.Find("nav").ClassList);

        layout.Find(".menu-backdrop").Click();
        Assert.Equal("false", layout.Find(".menu-toggle").GetAttribute("aria-expanded"));
        Assert.DoesNotContain("open", layout.Find("nav").ClassList);

        layout.Find(".menu-toggle").Click();
        layout.Find(".site-header-inner").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.DoesNotContain("open", layout.Find("nav").ClassList);
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
