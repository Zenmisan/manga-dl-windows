using Jint;
using MangaDl.Core.Extensions;
using Xunit;

namespace MangaDl.Core.Tests;

public class DomShimTests
{
    [Fact]
    public void DomElement_RemoveMethod_StripsElementsLikeAdRemover()
    {
        var html = @"<div class=""chapter-content"">
            <div class=""ads banner"">SPAM ADVERTISEMENT</div>
            <script>var x = 1;</script>
            <p>Real novel chapter text line 1.</p>
            <div class=""ads popup"">ANOTHER AD</div>
            <p>Real novel chapter text line 2.</p>
        </div>";

        var engine = new Engine();
        engine.SetValue("__domParse", new Func<string, string?, DomDocument>(DomShim.ParseFromString));
        engine.Execute(@"
            var doc = __domParse(`" + html + @"`, 'text/html');
            var ads = doc.querySelectorAll('.ads, script');
            for (var i = 0; i < ads.length; i++) {
                ads[i].remove();
            }
            var root = doc.querySelector('.chapter-content');
            var remainingAds = doc.querySelectorAll('.ads');
            var text = root.textContent.trim();
        ");

        var remainingAdsCount = engine.GetValue("remainingAds").AsArray().Length;
        var text = engine.GetValue("text").AsString();

        Assert.Equal(0, remainingAdsCount);
        Assert.Contains("Real novel chapter text line 1.", text);
        Assert.Contains("Real novel chapter text line 2.", text);
        Assert.DoesNotContain("SPAM ADVERTISEMENT", text);
    }

    [Fact]
    public void DomElement_ClassListAndAttributes_WorkAccurately()
    {
        var html = @"<div id=""hero"" class=""active primary highlighted"" data-id=""123""><span>Title</span></div>";

        var engine = new Engine();
        engine.SetValue("__domParse", new Func<string, string?, DomDocument>(DomShim.ParseFromString));
        engine.Execute(@"
            var doc = __domParse(`" + html + @"`, 'text/html');
            var el = doc.querySelector('#hero');
            var hasActive = el.classList.contains('active');
            var hasMissing = el.classList.contains('inactive');
            el.setAttribute('data-new', 'applied');
            var newVal = el.getAttribute('data-new');
            var hasNewAttr = el.hasAttribute('data-new');
        ");

        Assert.True(engine.GetValue("hasActive").AsBoolean());
        Assert.False(engine.GetValue("hasMissing").AsBoolean());
        Assert.Equal("applied", engine.GetValue("newVal").AsString());
        Assert.True(engine.GetValue("hasNewAttr").AsBoolean());
    }

    [Fact]
    public void DomElement_LeadingCombinatorSelector_ResolvesCleanly()
    {
        var html = @"<div class=""list"">
            <div class=""item""><a href=""/item1"">Item 1</a></div>
            <div class=""item""><a href=""/item2"">Item 2</a></div>
        </div>";

        var doc = DomShim.ParseFromString(html);
        var item = doc.querySelector(".item");
        Assert.NotNull(item);

        // Leading combinator '> a'
        var directLink = item.querySelector("> a");
        Assert.NotNull(directLink);
        Assert.Equal("/item1", directLink.getAttribute("href"));
    }

    [Fact]
    public void DomElement_NextElementSibling_NavigatesCorrectly()
    {
        var html = @"<div class=""parent"">
            <h1 id=""first"">First</h1>
            <p id=""second"">Second</p>
            <span id=""third"">Third</span>
        </div>";

        var doc = DomShim.ParseFromString(html);
        var first = doc.querySelector("#first");
        Assert.NotNull(first);

        var second = first.nextElementSibling;
        Assert.NotNull(second);
        Assert.Equal("P", second.tagName);
        Assert.Equal("Second", second.textContent);

        var third = second.nextElementSibling;
        Assert.NotNull(third);
        Assert.Equal("SPAN", third.tagName);
        Assert.Null(third.nextElementSibling);
    }
}
