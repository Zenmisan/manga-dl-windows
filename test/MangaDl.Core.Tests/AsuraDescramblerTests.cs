using MangaDl.Core.Http;
using MangaDl.Core.Images;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace MangaDl.Core.Tests;

public class AsuraDescramblerTests
{
    [Fact]
    public void TryParse_ProxyQueryString_ParsesSuccessfully()
    {
        var proxyUrl = "/manga/asura-descramble?url=https%3A%2F%2Fcdn.asuracomic.net%2Fpage1.jpg&tiles=%5B3%2C1%2C0%2C2%5D&tileCols=2&tileRows=2";

        var ok = AsuraDescrambler.TryParse(proxyUrl, out var actualUrl, out var tiles, out var cols, out var rows);

        Assert.True(ok);
        Assert.Equal("https://cdn.asuracomic.net/page1.jpg", actualUrl);
        Assert.Equal(new[] { 3, 1, 0, 2 }, tiles);
        Assert.Equal(2, cols);
        Assert.Equal(2, rows);
    }

    [Fact]
    public void TryParse_FragmentJson_ParsesSuccessfully()
    {
        var fragmentUrl = "https://cdn.asuracomic.net/page2.jpg#{\"tiles\":[4,2,0,1,3],\"tileCols\":5,\"tileRows\":1}";

        var ok = AsuraDescrambler.TryParse(fragmentUrl, out var actualUrl, out var tiles, out var cols, out var rows);

        Assert.True(ok);
        Assert.Equal("https://cdn.asuracomic.net/page2.jpg", actualUrl);
        Assert.Equal(new[] { 4, 2, 0, 1, 3 }, tiles);
        Assert.Equal(5, cols);
        Assert.Equal(1, rows);
    }

    [Fact]
    public void TryParse_StandardUrl_ReturnsFalse()
    {
        var normalUrl = "https://cdn.asuracomic.net/media/cover.jpg";

        var ok = AsuraDescrambler.TryParse(normalUrl, out _, out _, out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void Descramble_ReassemblesTilesCorrectly()
    {
        // Create a 20x20 image with 4 tiles of 10x10 each
        // Scrambled layout:
        // Top-Left (0): Red
        // Top-Right (1): Green
        // Bottom-Left (2): Blue
        // Bottom-Right (3): White
        using var img = new Image<Rgba32>(20, 20);

        // Fill Tile 0 (Red)
        for (int y = 0; y < 10; y++)
            for (int x = 0; x < 10; x++)
                img[x, y] = new Rgba32(255, 0, 0, 255);

        // Fill Tile 1 (Green)
        for (int y = 0; y < 10; y++)
            for (int x = 10; x < 20; x++)
                img[x, y] = new Rgba32(0, 255, 0, 255);

        // Fill Tile 2 (Blue)
        for (int y = 10; y < 20; y++)
            for (int x = 0; x < 10; x++)
                img[x, y] = new Rgba32(0, 0, 255, 255);

        // Fill Tile 3 (White)
        for (int y = 10; y < 20; y++)
            for (int x = 10; x < 20; x++)
                img[x, y] = new Rgba32(255, 255, 255, 255);

        using var mem = new MemoryStream();
        img.Save(mem, new JpegEncoder());
        var rawBytes = mem.ToArray();

        // Shuffled tiles mapping:
        // dst 0 takes src 3 (White)
        // dst 1 takes src 2 (Blue)
        // dst 2 takes src 1 (Green)
        // dst 3 takes src 0 (Red)
        var tiles = new[] { 3, 2, 1, 0 };

        var descrambled = AsuraDescrambler.Descramble(rawBytes, tiles, 2, 2);
        Assert.NotEmpty(descrambled);

        using var result = Image.Load<Rgba32>(descrambled);
        Assert.Equal(20, result.Width);
        Assert.Equal(20, result.Height);

        // Verify dst 0 (top-left) now has White (from src 3)
        var px0 = result[5, 5];
        Assert.True(px0.R > 200 && px0.G > 200 && px0.B > 200, "Expected white at top-left");

        // Verify dst 3 (bottom-right) now has Red (from src 0)
        var px3 = result[15, 15];
        Assert.True(px3.R > 200 && px3.G < 50 && px3.B < 50, "Expected red at bottom-right");
    }

    [Fact]
    public void HttpService_ResolveUrl_ExtractsAsuraUrl()
    {
        using var http = new HttpService();
        var proxy = "/manga/asura-descramble?url=https%3A%2F%2Fcdn.example.com%2Fchapter1%2F001.jpg&tiles=%5B0%5D&tileCols=1&tileRows=1";

        var resolved = http.ResolveUrl(proxy);

        Assert.Equal("https://cdn.example.com/chapter1/001.jpg", resolved);
    }
}
