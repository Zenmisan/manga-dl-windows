using System.IO;
using System.Web;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace MangaDl.Core.Images;

public sealed record ComixtoParams(
    string ImageUrl,
    int Seed,
    string? Algo,
    int EncSeed,
    int EncLen,
    string? EncAlgo,
    bool Valid);

/// <summary>
/// Native on-device descrambler for Comix.to pages (ported from comixto.js and Linux engine).
/// Implements 32-bit signed LCG and xorshift32 header XOR decryption and 5x5 tile descrambling.
/// </summary>
public static class ComixToDescrambler
{
    private const int EncMult = 1000005;
    private const int EncInc = 1234567891;
    private const int LcgMult = 1664525;
    private const int LcgInc = 1013904223;

    private static int LcgNext(int state, int mult, int inc)
    {
        return unchecked((int)((long)state * mult + inc));
    }

    private static int XorShift32(int s)
    {
        s ^= s << 13;
        s ^= (int)((uint)s >> 17);
        s ^= s << 5;
        return s;
    }

    public static bool TryParse(string url, out ComixtoParams parameters)
    {
        parameters = new ComixtoParams(string.Empty, 0, null, 0, 0, null, false);
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("comixto://img?", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var queryIdx = url.IndexOf('?');
            if (queryIdx < 0) return false;

            var qs = url[(queryIdx + 1)..];
            var q = HttpUtility.ParseQueryString(qs);

            var rawUrl = q["url"];
            if (string.IsNullOrEmpty(rawUrl)) return false;

            int.TryParse(q["seed"], out var seed);
            var algo = q["algo"];
            int.TryParse(q["encSeed"], out var encSeed);
            int.TryParse(q["encLen"], out var encLen);
            var encAlgo = q["encAlgo"];

            parameters = new ComixtoParams(
                rawUrl,
                seed,
                algo,
                encSeed,
                encLen,
                encAlgo,
                true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] XorLcg(byte[] bytes, int seed, int length)
    {
        var result = (byte[])bytes.Clone();
        var limit = Math.Min(result.Length, length);
        var state = seed;
        for (var i = 0; i < limit; i++)
        {
            state = LcgNext(state, EncMult, EncInc);
            var key = (byte)((uint)state >> 24);
            result[i] ^= key;
        }
        return result;
    }

    private static byte[] XorXorShift(byte[] bytes, int seed, int length, bool highByte)
    {
        var result = (byte[])bytes.Clone();
        var limit = Math.Min(result.Length, length);
        var state = seed;
        for (var i = 0; i < limit; i++)
        {
            state = XorShift32(state);
            var key = highByte ? (byte)((uint)state >> 24) : (byte)((uint)state & 0xFF);
            result[i] ^= key;
        }
        return result;
    }

    private static bool HasImageSignature(byte[] b)
    {
        if (b == null || b.Length < 12) return false;
        if (b[0] == 0xFF && b[1] == 0xD8) return true; // JPEG
        if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return true; // PNG
        if (b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 &&
            b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50) return true; // WEBP
        return false;
    }

    private static int[] BuildOrderLcg(int seed, int n)
    {
        var arr = new int[n];
        for (var k = 0; k < n; k++) arr[k] = k;
        var state = seed;
        for (var k = n - 1; k >= 1; k--)
        {
            state = LcgNext(state, LcgMult, LcgInc);
            var j = (int)((uint)state % (uint)(k + 1));
            (arr[k], arr[j]) = (arr[j], arr[k]);
        }
        var inv = new int[n];
        for (var k = 0; k < n; k++) inv[arr[k]] = k;
        return inv;
    }

    private static int[] BuildOrderXorShift(int seed, int n)
    {
        var arr = new int[n];
        for (var k = 0; k < n; k++) arr[k] = k;
        var state = seed | 1;
        for (var k = n - 1; k >= 1; k--)
        {
            state = XorShift32(state);
            var j = (int)((uint)state % (uint)(k + 1));
            (arr[k], arr[j]) = (arr[j], arr[k]);
        }
        var inv = new int[n];
        for (var k = 0; k < n; k++) inv[arr[k]] = k;
        return inv;
    }

    public static byte[] Descramble(byte[] rawBytes, ComixtoParams p)
    {
        if (rawBytes == null || rawBytes.Length == 0 || !p.Valid) return rawBytes ?? Array.Empty<byte>();

        var bytes = rawBytes;

        // Step 1: XOR decryption of image header
        if (p.EncSeed != 0 && p.EncLen > 0)
        {
            if (p.EncAlgo == "2")
            {
                var s32 = p.EncSeed;
                var candidates = new[]
                {
                    XorXorShift(bytes, s32 | 1, p.EncLen, false),
                    XorXorShift(bytes, s32, p.EncLen, false),
                    XorXorShift(bytes, s32 | 1, p.EncLen, true),
                    XorLcg(bytes, s32, p.EncLen)
                };
                var found = false;
                foreach (var c in candidates)
                {
                    if (HasImageSignature(c))
                    {
                        bytes = c;
                        found = true;
                        break;
                    }
                }
                if (!found) bytes = candidates[0];
            }
            else
            {
                bytes = XorLcg(bytes, p.EncSeed, p.EncLen);
            }
        }

        // Step 2: 5x5 tile descramble
        if (p.Seed == 0) return bytes;

        const int cols = 5;
        const int rows = 5;
        const int n = cols * rows;

        try
        {
            using var source = Image.Load<Rgba32>(bytes);
            var w = source.Width;
            var h = source.Height;
            var tileW = w / cols;
            var tileH = h / rows;

            if (tileW <= 0 || tileH <= 0) return bytes;

            var order = p.Algo == "3" ? BuildOrderXorShift(p.Seed, n) : BuildOrderLcg(p.Seed, n);
            using var output = new Image<Rgba32>(tileW * cols, tileH * rows);

            for (var dst = 0; dst < n; dst++)
            {
                var srcIdx = order[dst];
                var srcCol = srcIdx % cols;
                var srcRow = srcIdx / cols;
                var dstCol = dst % cols;
                var dstRow = dst / cols;

                var srcX = srcCol * tileW;
                var srcY = srcRow * tileH;
                var dstX = dstCol * tileW;
                var dstY = dstRow * tileH;

                for (var y = 0; y < tileH; y++)
                {
                    for (var x = 0; x < tileW; x++)
                    {
                        output[dstX + x, dstY + y] = source[srcX + x, srcY + y];
                    }
                }
            }

            using var outStream = new MemoryStream();
            output.Save(outStream, new JpegEncoder { Quality = 92 });
            return outStream.ToArray();
        }
        catch
        {
            // If image decoding fails, return decrypted bytes
            return bytes;
        }
    }
}
