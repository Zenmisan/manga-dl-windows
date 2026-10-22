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

        Assert.Equal(0u, remainingAdsCount);
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

    [Fact]
    public void DomElement_AngleSharp_QuerySelectorSpecialSelectors()
    {
        var html = @"<div class=""meta"">
            <div><span>Author Name</span> <a href=""/a1"">Author Link</a></div>
            <p class=""author"">John Doe</p>
            <span class=""leading-7"">Jane Smith</span>
            <span class=""other"">Author: Bob</span>
        </div>";

        var doc = DomShim.ParseFromString(html);

        // ReadHive selector: span.leading-7, .author, span:contains("Author")
        var author1 = doc.querySelector(@"span.leading-7, .author, span:contains(""Author"")");
        
        // Pure :contains selector
        var authorContainsOnly = doc.querySelector(@"span:contains(""Author"")");

        // Pure :contains selector with single quotes
        var authorContainsSingleQuotes = doc.querySelector(@"span:contains('Author')");

        // Bato selector: .meta div:has(span) a, [class*="author"] a
        var authorLinks = doc.querySelectorAll(@".meta div:has(span) a, [class*=""author""] a");

        Assert.NotNull(author1);
        Assert.NotNull(authorContainsOnly);
        Assert.Equal("Author Name", authorContainsOnly.textContent);
        Assert.NotNull(authorContainsSingleQuotes);
        Assert.NotEmpty(authorLinks);
    }

    [Fact]
    public void DomElement_UnquotedAttributeSelectors_SanitizedAndMatched()
    {
        var html = @"<div class=""gallery"">
            <img src=""http://cdn.example.com/cover.jpg"" alt=""Cover"" />
            <a href=""/series/solo-leveling/1"">Read Chapter</a>
            <a href=""/other/info"">Other</a>
        </div>";

        var doc = DomShim.ParseFromString(html);

        // Unquoted http and unquoted /series/
        var img = doc.querySelector("img[src^=http]");
        var link = doc.querySelector("a[href*=/series/]");

        Assert.NotNull(img);
        Assert.Equal("http://cdn.example.com/cover.jpg", img.getAttribute("src"));
        Assert.NotNull(link);
        Assert.Equal("/series/solo-leveling/1", link.getAttribute("href"));
    }

    [Fact]
    public void DomDocument_CreateElement_CreatesElementWithAttributes()
    {
        var doc = DomShim.ParseFromString("<html><body></body></html>");
        var el = doc.createElement("canvas");
        Assert.NotNull(el);
        Assert.Equal("CANVAS", el.tagName);
        el.setAttribute("width", "100");
        Assert.Equal("100", el.getAttribute("width"));
    }
}
