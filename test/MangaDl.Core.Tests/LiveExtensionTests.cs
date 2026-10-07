using MangaDl.Core.Extensions;
using MangaDl.Core.Http;
using Xunit;

namespace MangaDl.Core.Tests;

public class LiveExtensionTests
{
    [Fact]
    public async Task TestRealMangaDexExtensionSearchAndDetail()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MangaDl.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var extensionPath = Path.Combine(dir.FullName, "src", "MangaDl.App", "Assets", "extensions", "mangadex.js");

        Assert.True(File.Exists(extensionPath), $"Extension file not found at: {extensionPath}");

        var jsSource = await File.ReadAllTextAsync(extensionPath);
        using var http = new HttpService();
        using var bridge = new ExtensionBridge(http);

        await bridge.LoadScriptAsync(jsSource);
        Assert.True(bridge.IsLoaded);

        // 1. Test live search
        var searchResults = await bridge.SearchAsync("Chainsaw Man", 1);
        Assert.NotNull(searchResults);
        Assert.NotEmpty(searchResults);

        var first = searchResults[0];
        Assert.False(string.IsNullOrWhiteSpace(first.Id));
        Assert.False(string.IsNullOrWhiteSpace(first.Title));
        Assert.Equal("mangadex", first.Provider);

        // 2. Test live manga detail
        var detail = await bridge.GetMangaDetailAsync(first.Id);
        Assert.NotNull(detail);
        Assert.Equal(first.Id, detail.Id);
        Assert.False(string.IsNullOrWhiteSpace(detail.Title));
        Assert.NotEmpty(detail.Chapters);

        // 3. Test chapter pages
        var firstChapter = detail.Chapters[0];
        var pages = await bridge.GetPagesAsync(firstChapter.Id);
        Assert.NotNull(pages);
        Assert.NotEmpty(pages);
        Assert.StartsWith("https://", pages[0]);
    }

    [Fact]
    public async Task TestRealNovelExtensionContract()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MangaDl.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var extensionPath = Path.Combine(dir.FullName, "src", "MangaDl.App", "Assets", "extensions", "royalroad.js");
        Assert.True(File.Exists(extensionPath), $"Extension file not found at: {extensionPath}");

        var jsSource = await File.ReadAllTextAsync(extensionPath);
        using var http = new HttpService();
        using var bridge = new ExtensionBridge(http);

        await bridge.LoadScriptAsync(jsSource);
        Assert.True(bridge.IsLoaded);

        // Verify exported methods on bridge
        Assert.True(bridge.Engine.Evaluate("typeof extension.getChapterText === 'function'").AsBoolean());
        Assert.True(bridge.Engine.Evaluate("typeof extension.search === 'function'").AsBoolean());
        Assert.True(bridge.Engine.Evaluate("typeof extension.getMangaDetail === 'function'").AsBoolean());
    }
}
