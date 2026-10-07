using Jint;
using MangaDl.Core.Extensions;
using Xunit;

namespace MangaDl.Core.Tests;

public class JintAsyncTests
{
    [Fact]
    public void TestAsyncFunctionExecution()
    {
        var engine = new Engine();

        // Register synchronous native fetch
        engine.SetValue("__nativeFetch", new Func<string, object>((url) =>
        {
            return new Dictionary<string, object>
            {
                ["status"] = 200,
                ["text"] = "{\"hello\":\"world\"}",
                ["json"] = new Dictionary<string, object> { ["hello"] = "world" },
                ["hello"] = "world"
            };
        }));

        engine.Execute(@"
            globalThis.apiFetch = function(url) {
                return Promise.resolve(__nativeFetch(url));
            };

            var extension = {
                async search(query) {
                    var res = await apiFetch('https://example.com/test?q=' + encodeURIComponent(query));
                    return 'found: ' + res.hello;
                }
            };

            var __output = null;
            (async () => {
                __output = await extension.search('sololeveling');
            })();
        ");

        var outputVal = engine.GetValue("__output");
        Assert.Equal("found: world", outputVal.AsString());
    }

    [Fact]
    public void TestDomParserWithAngleSharp()
    {
        var engine = new Engine();

        engine.SetValue("__domParse", new Func<string, string?, DomDocument>(DomShim.ParseFromString));

        engine.Execute(@"
            globalThis.DOMParser = function() {};
            globalThis.DOMParser.prototype.parseFromString = function(html, mime) {
                return __domParse(html, mime || 'text/html');
            };

            var html = '<div class=""chapter-item""><a href=""/chapter-1"">Chapter 1: The Beginning</a><span class=""date"">Oct 7</span></div>';
            var doc = new DOMParser().parseFromString(html, 'text/html');
            var a = doc.querySelector('.chapter-item a');
            var title = a.textContent;
            var href = a.getAttribute('href');
            var date = doc.querySelector('.date').textContent;
        ");

        Assert.Equal("Chapter 1: The Beginning", engine.GetValue("title").AsString());
        Assert.Equal("/chapter-1", engine.GetValue("href").AsString());
        Assert.Equal("Oct 7", engine.GetValue("date").AsString());
    }
}
