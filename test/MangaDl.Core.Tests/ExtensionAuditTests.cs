using System.Diagnostics;
using MangaDl.Core.Extensions;
using MangaDl.Core.Http;
using Xunit;
using Xunit.Abstractions;

namespace MangaDl.Core.Tests;

public class ExtensionAuditTests
{
    private readonly ITestOutputHelper _output;

    public ExtensionAuditTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string GetExtensionsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MangaDl.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var path = Path.Combine(dir.FullName, "src", "MangaDl.App", "Assets", "extensions");
        Assert.True(Directory.Exists(path), $"Extensions directory not found at: {path}");
        return path;
    }

    [Fact]
    public async Task AuditAllExtensionScripts_LoadAndContractCheck()
    {
        var dir = GetExtensionsDirectory();
        var jsFiles = Directory.GetFiles(dir, "*.js").OrderBy(f => f).ToList();
        Assert.NotEmpty(jsFiles);

        _output.WriteLine($"Found {jsFiles.Count} extension scripts to audit.");

        using var http = new HttpService();
        var failureCount = 0;
        var loadedCount = 0;

        foreach (var file in jsFiles)
        {
            var fileName = Path.GetFileName(file);
            try
            {
                var jsCode = await File.ReadAllTextAsync(file);
                using var bridge = new ExtensionBridge(http);
                await bridge.LoadScriptAsync(jsCode);

                Assert.True(bridge.IsLoaded, $"Bridge marked not loaded for {fileName}");
                loadedCount++;
                _output.WriteLine($"[PASS] {fileName} loaded cleanly.");
            }
            catch (Exception ex)
            {
                failureCount++;
                _output.WriteLine($"[FAIL] {fileName} failed: {ex.Message}");
            }
        }

        _output.WriteLine($"Audit complete: {loadedCount} passed, {failureCount} failed out of {jsFiles.Count}.");
        Assert.Equal(0, failureCount);
    }

    [Fact]
    public async Task AuditAllExtensionScripts_ExportedMethodsCheck()
    {
        var dir = GetExtensionsDirectory();
        var jsFiles = Directory.GetFiles(dir, "*.js").OrderBy(f => f).ToList();

        using var http = new HttpService();
        var withSearch = 0;
        var withDetail = 0;
        var withPages = 0;

        foreach (var file in jsFiles)
        {
            var fileName = Path.GetFileName(file);
            var jsCode = await File.ReadAllTextAsync(file);
            using var bridge = new ExtensionBridge(http);
            await bridge.LoadScriptAsync(jsCode);

            // Ignore templates which require configuration
            if (fileName.EndsWith(".template.js")) continue;

            var hasSearch = bridge.HasFunction("search");
            var hasDetail = bridge.HasFunction("getMangaDetail");
            var hasPages = bridge.HasFunction("getPages");
            var hasChapterText = bridge.HasFunction("getChapterText");

            if (hasSearch) withSearch++;
            if (hasDetail) withDetail++;
            if (hasPages || hasChapterText) withPages++;

            _output.WriteLine($"[{fileName}] search={hasSearch}, detail={hasDetail}, pages={hasPages}, chapterText={hasChapterText}");
            Assert.True(hasSearch, $"{fileName} should define search()");
            Assert.True(hasDetail, $"{fileName} should define getMangaDetail()");
            Assert.True(hasPages || hasChapterText, $"{fileName} should define either getPages() or getChapterText()");
        }

        _output.WriteLine($"Summary: {withSearch} sources with search, {withDetail} with detail, {withPages} with content reading (pages or novel text).");
    }
}
