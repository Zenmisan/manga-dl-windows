using MangaDl.Core.Http;
using Xunit;

namespace MangaDl.Core.Tests;

public class CloudflareDetectionTests
{
    [Fact]
    public void IsCloudflareChallenge_Status403Turnstile_ReturnsTrue()
    {
        var result = new HttpResponseResult
        {
            StatusCode = 403,
            IsSuccess = false,
            Text = "<!DOCTYPE html><html><head><title>Just a moment...</title></head><body><script src=\"https://challenges.cloudflare.com/turnstile/v0/api.js\"></script></body></html>"
        };

        Assert.True(result.IsCloudflareChallenge);
    }

    [Fact]
    public void IsCloudflareChallenge_Status503RayId_ReturnsTrue()
    {
        var result = new HttpResponseResult
        {
            StatusCode = 503,
            IsSuccess = false,
            Text = "<html><body>Checking your browser before accessing... Cloudflare Ray ID: 8931728192</body></html>"
        };

        Assert.True(result.IsCloudflareChallenge);
    }

    [Fact]
    public void IsCloudflareChallenge_Status200StandardHtml_ReturnsFalse()
    {
        var result = new HttpResponseResult
        {
            StatusCode = 200,
            IsSuccess = true,
            Text = "<!DOCTYPE html><html><body><h1>Manga List</h1></body></html>"
        };

        Assert.False(result.IsCloudflareChallenge);
    }

    [Fact]
    public void IsCloudflareChallenge_Status404NotFound_ReturnsFalse()
    {
        var result = new HttpResponseResult
        {
            StatusCode = 404,
            IsSuccess = false,
            Text = "<html><body>404 Page Not Found</body></html>"
        };

        Assert.False(result.IsCloudflareChallenge);
    }
}
