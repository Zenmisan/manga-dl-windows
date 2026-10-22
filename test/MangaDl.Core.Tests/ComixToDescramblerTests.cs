using MangaDl.Core.Http;
using MangaDl.Core.Images;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace MangaDl.Core.Tests;

public class ComixToDescramblerTests
{
    [Fact]
    public void TryParse_ValidComixtoUrl_ParsesSuccessfully()
    {
        var url = "comixto://img?url=https%3A%2F%2Fimg.comix.to%2Fpage1.jpg&seed=12345&algo=3&encSeed=67890&encLen=512&encAlgo=2";

        var ok = ComixToDescrambler.TryParse(url, out var p);

        Assert.True(ok);
        Assert.True(p.Valid);
        Assert.Equal("https://img.comix.to/page1.jpg", p.ImageUrl);
        Assert.Equal(12345, p.Seed);
        Assert.Equal("3", p.Algo);
        Assert.Equal(67890, p.EncSeed);
        Assert.Equal(512, p.EncLen);
        Assert.Equal("2", p.EncAlgo);
    }

    [Fact]
    public void TryParse_StandardUrl_ReturnsFalse()
    {
        var normalUrl = "https://img.comix.to/cover.jpg";

        var ok = ComixToDescrambler.TryParse(normalUrl, out var p);

        Assert.False(ok);
        Assert.False(p.Valid);
    }

    [Fact]
    public void HttpService_ResolveUrl_ExtractsComixToUrl()
    {
        using var http = new HttpService();
        var comixto = "comixto://img?url=https%3A%2F%2Fimg.comix.to%2Fchapter1%2F001.jpg&seed=1&algo=3";

        var resolved = http.ResolveUrl(comixto);

        Assert.Equal("https://img.comix.to/chapter1/001.jpg", resolved);
    }

    [Fact]
    public void Descramble_5x5Tiles_ReassemblesImage()
    {
        // Create a 50x50 test image (5x5 grid of 10x10 tiles)
        using var img = new Image<Rgba32>(50, 50);

        for (int r = 0; r < 5; r++)
        {
            for (int c = 0; c < 5; c++)
            {
                var color = new Rgba32((byte)(c * 50), (byte)(r * 50), 100, 255);
                for (int y = 0; y < 10; y++)
                    for (int x = 0; x < 10; x++)
                        img[c * 10 + x, r * 10 + y] = color;
            }
        }

        using var mem = new MemoryStream();
        img.Save(mem, new JpegEncoder());
        var rawBytes = mem.ToArray();

        var p = new ComixtoParams("https://example.com/test.jpg", 42, "3", 0, 0, null, true);
        var descrambled = ComixToDescrambler.Descramble(rawBytes, p);

        Assert.NotEmpty(descrambled);
        using var result = Image.Load<Rgba32>(descrambled);
        Assert.Equal(50, result.Width);
        Assert.Equal(50, result.Height);
    }
}
