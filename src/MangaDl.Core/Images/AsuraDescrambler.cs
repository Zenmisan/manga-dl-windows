using System.IO;
using System.Text.Json;
using System.Web;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace MangaDl.Core.Images;

/// <summary>
/// On-device image tile descrambler for scrambled manga sources (e.g. Asura Scans).
/// Reassembles shuffled rectangular image tiles in memory without external server proxies.
/// </summary>
public static class AsuraDescrambler
{
    public static bool TryParse(
        string url,
        out string actualUrl,
        out int[] tiles,
        out int tileCols,
        out int tileRows)
    {
        actualUrl = string.Empty;
        tiles = Array.Empty<int>();
        tileCols = 0;
        tileRows = 0;

        if (string.IsNullOrWhiteSpace(url)) return false;

        // Pattern 1: Proxy URL "/manga/asura-descramble?url=...&tiles=...&tileCols=...&tileRows=..."
        if (url.Contains("asura-descramble", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var queryIdx = url.IndexOf('?');
                if (queryIdx >= 0)
                {
                    var queryString = url[(queryIdx + 1)..];
                    var query = HttpUtility.ParseQueryString(queryString);

                    var rawUrl = query["url"];
                    var rawTiles = query["tiles"];
                    var rawCols = query["tileCols"] ?? query["tile_cols"];
                    var rawRows = query["tileRows"] ?? query["tile_rows"];

                    if (!string.IsNullOrEmpty(rawUrl) && !string.IsNullOrEmpty(rawTiles) &&
                        int.TryParse(rawCols, out tileCols) && int.TryParse(rawRows, out tileRows) &&
                        tileCols > 0 && tileRows > 0)
                    {
                        actualUrl = rawUrl;
                        var parsedTiles = JsonSerializer.Deserialize<int[]>(rawTiles);
                        if (parsedTiles != null && parsedTiles.Length > 0)
                        {
                            tiles = parsedTiles;
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Ignore parse failures and fall through
            }
        }

        // Pattern 2: Fragment-encoded metadata "https://...#{"tiles":[...],"tileCols":5,"tileRows":5}"
        var hashIdx = url.IndexOf('#');
        if (hashIdx >= 0 && hashIdx < url.Length - 1)
        {
            var fragment = url[(hashIdx + 1)..];
            if (fragment.StartsWith('{') && fragment.Contains("\"tiles\""))
            {
                try
                {
                    using var doc = JsonDocument.Parse(fragment);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("tiles", out var tilesProp) && tilesProp.ValueKind == JsonValueKind.Array)
                    {
                        var parsedTiles = JsonSerializer.Deserialize<int[]>(tilesProp.GetRawText());
                        tileCols = root.TryGetProperty("tileCols", out var cProp) ? cProp.GetInt32() : (root.TryGetProperty("tile_cols", out var cProp2) ? cProp2.GetInt32() : 5);
                        tileRows = root.TryGetProperty("tileRows", out var rProp) ? rProp.GetInt32() : (root.TryGetProperty("tile_rows", out var rProp2) ? rProp2.GetInt32() : 5);

                        if (parsedTiles != null && parsedTiles.Length > 0 && tileCols > 0 && tileRows > 0)
                        {
                            actualUrl = url[..hashIdx];
                            tiles = parsedTiles;
                            return true;
                        }
                    }
                }
                catch
                {
                    // Ignore parse failures and fall through
                }
            }
        }

        return false;
    }

    public static byte[] Descramble(byte[] rawBytes, int[] tiles, int cols, int rows)
    {
        if (rawBytes == null || rawBytes.Length == 0) return Array.Empty<byte>();
        if (tiles == null || tiles.Length == 0 || cols <= 0 || rows <= 0) return rawBytes;

        using var source = Image.Load<Rgba32>(rawBytes);
        int w = source.Width;
        int h = source.Height;

        int tileW = w / cols;
        int tileH = h / rows;

        if (tileW <= 0 || tileH <= 0) return rawBytes;

        using var output = new Image<Rgba32>(tileW * cols, tileH * rows);

        for (int dstIdx = 0; dstIdx < tiles.Length; dstIdx++)
        {
            int srcIdx = tiles[dstIdx];
            int srcCol = srcIdx % cols;
            int srcRow = srcIdx / cols;
            int dstCol = dstIdx % cols;
            int dstRow = dstIdx / cols;

            int srcX = srcCol * tileW;
            int srcY = srcRow * tileH;
            int dstX = dstCol * tileW;
            int dstY = dstRow * tileH;

            for (int y = 0; y < tileH; y++)
            {
                for (int x = 0; x < tileW; x++)
                {
                    output[dstX + x, dstY + y] = source[srcX + x, srcY + y];
                }
            }
        }

        using var outStream = new MemoryStream();
        output.Save(outStream, new JpegEncoder { Quality = 92 });
        return outStream.ToArray();
    }
}
