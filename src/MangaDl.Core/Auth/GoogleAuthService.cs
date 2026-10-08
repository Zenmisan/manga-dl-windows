using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MangaDl.Core.Http;

namespace MangaDl.Core.Auth;

/// <summary>
/// Google OAuth 2.0 authorization-code + PKCE flow for a Desktop-type client,
/// using the loopback redirect (same mechanism as the AniList/MAL tracker flows —
/// see MangaDl.Core.Tracking.OAuthLoopbackListener). Unlike MAL, Google supports
/// (and expects) the S256 PKCE challenge method, not "plain".
/// </summary>
public sealed class GoogleAuthService
{
    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    private readonly HttpService _http;

    public GoogleAuthService(HttpService http)
    {
        _http = http;
    }

    public static (string Verifier, string Challenge) GeneratePkceS256()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        var verifier = Base64UrlEncode(bytes);
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    public static string GetAuthUrl(string clientId, string redirectUri, string codeChallenge)
    {
        var scope = Uri.EscapeDataString("openid email profile");
        return $"{AuthEndpoint}?client_id={Uri.EscapeDataString(clientId)}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               "&response_type=code" +
               $"&scope={scope}" +
               $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
               "&code_challenge_method=S256" +
               "&access_type=offline";
    }

    /// <summary>Exchanges the authorization code for Google's id_token. Returns null on failure.</summary>
    public async Task<string?> ExchangeCodeForIdTokenAsync(
        string code, string codeVerifier, string clientId, string clientSecret, string redirectUri)
    {
        var form = $"client_id={Uri.EscapeDataString(clientId)}" +
                   $"&client_secret={Uri.EscapeDataString(clientSecret)}" +
                   $"&code={Uri.EscapeDataString(code)}" +
                   $"&code_verifier={Uri.EscapeDataString(codeVerifier)}" +
                   "&grant_type=authorization_code" +
                   $"&redirect_uri={Uri.EscapeDataString(redirectUri)}";

        var headers = new Dictionary<string, string> { ["Content-Type"] = "application/x-www-form-urlencoded" };
        var res = await _http.FetchAsync(TokenEndpoint, "POST", headers, form);
        if (!res.IsSuccess) return null;

        using var doc = JsonDocument.Parse(res.Text);
        return doc.RootElement.TryGetProperty("id_token", out var idToken) ? idToken.GetString() : null;
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
