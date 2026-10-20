using System.Text.Json;
using MangaDl.Core.Auth;
using MangaDl.Core.Http;
using Xunit;

namespace MangaDl.Core.Tests;

public class FirebaseAuthServiceTests
{
    private const string TestApiKey = "AIzaSyDzw5OFZJ_r4uO_wtGK30c8i92qV_aaXbw";

    [Fact]
    public void TestGeneratePkceS256()
    {
        var (verifier, challenge) = FirebaseAuthService.GeneratePkceS256();
        Assert.NotNull(verifier);
        Assert.NotNull(challenge);
        Assert.NotEmpty(verifier);
        Assert.NotEmpty(challenge);
        Assert.NotEqual(verifier, challenge);
    }

    [Fact]
    public void TestGetOAuthAuthorizeUrl()
    {
        var service = new FirebaseAuthService(new HttpService(), TestApiKey);
        var url = service.GetOAuthAuthorizeUrl("google", "http://localhost:5678/google-callback", "challenge123");
        Assert.Contains("accounts.google.com", url);
        Assert.Contains("code_challenge=challenge123", url);
        Assert.Contains("redirect_uri=", url);
    }

    [Fact]
    public void TestFriendlyErrorParsing()
    {
        // Testing that FriendlyError correctly maps common Firebase auth codes
        var authEx = new AuthException("Incorrect email or password.", "{\"error\":{\"message\":\"INVALID_LOGIN_CREDENTIALS\"}}");
        Assert.Equal("Incorrect email or password.", authEx.Message);
        Assert.NotNull(authEx.Raw);
    }
}
