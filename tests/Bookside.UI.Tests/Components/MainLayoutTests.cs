using Bookside.UI.Layout;

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
}
