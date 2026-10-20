using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MangaDl.Core.Http;

namespace MangaDl.Core.Auth;

/// <summary>
/// REST client for Firebase Authentication.
/// Authenticates users against Firebase Identity Toolkit REST APIs, yielding
/// a Firebase JWT (idToken) and User ID (localId) that are passed downstream
/// to Supabase PostgREST for cross-platform cloud sync and RLS authorization.
/// </summary>
public sealed class FirebaseAuthService
{
    private const string IdentityToolkitBaseUrl = "https://identitytoolkit.googleapis.com/v1";
    private const string SecureTokenBaseUrl = "https://securetoken.googleapis.com/v1";

    private readonly HttpService _http;
    private readonly string _apiKey;

    public FirebaseAuthService(HttpService http, string apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

    public async Task<AuthSession> SignInWithPasswordAsync(string email, string password)
    {
        var url = $"{IdentityToolkitBaseUrl}/accounts:signInWithPassword?key={Uri.EscapeDataString(_apiKey)}";
        var payload = JsonSerializer.Serialize(new
        {
            email,
            password,
            returnSecureToken = true
        });

        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json"
        };

        var response = await _http.FetchAsync(url, "POST", headers, payload);
        if (!response.IsSuccess)
        {
            throw new AuthException(FriendlyError(ExtractErrorMessage(response.Text)), response.Text);
        }

        return ParseFirebaseSession(response.Text) 
            ?? throw new AuthException("Authentication succeeded but failed to parse session.");
    }

    public async Task<AuthSignUpResult> SignUpAsync(string username, string email, string password)
    {
        var url = $"{IdentityToolkitBaseUrl}/accounts:signUp?key={Uri.EscapeDataString(_apiKey)}";
        var payload = JsonSerializer.Serialize(new
        {
            email,
            password,
            returnSecureToken = true
        });

        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json"
        };

        var response = await _http.FetchAsync(url, "POST", headers, payload);
        if (!response.IsSuccess)
        {
            throw new AuthException(FriendlyError(ExtractErrorMessage(response.Text)), response.Text);
        }

        var session = ParseFirebaseSession(response.Text);
        if (session == null)
        {
            return new AuthSignUpResult(null, "Registration succeeded. Please sign in.");
        }

        // If a username was supplied, update the Firebase profile displayName
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(session.AccessToken))
        {
            try
            {
                var updateUrl = $"{IdentityToolkitBaseUrl}/accounts:update?key={Uri.EscapeDataString(_apiKey)}";
                var updatePayload = JsonSerializer.Serialize(new
                {
                    idToken = session.AccessToken,
                    displayName = username,
                    returnSecureToken = true
                });
                await _http.FetchAsync(updateUrl, "POST", headers, updatePayload);
            }
            catch
            {
                // Non-fatal: user account is already created
            }
        }

        return new AuthSignUpResult(session, null);
    }

    public async Task RequestPasswordResetAsync(string email)
    {
        var url = $"{IdentityToolkitBaseUrl}/accounts:sendOobCode?key={Uri.EscapeDataString(_apiKey)}";
        var payload = JsonSerializer.Serialize(new
        {
            requestType = "PASSWORD_RESET",
            email
        });

        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json"
        };

        var response = await _http.FetchAsync(url, "POST", headers, payload);
        if (!response.IsSuccess)
        {
            throw new AuthException(FriendlyError(ExtractErrorMessage(response.Text)), response.Text);
        }
    }

    public async Task<AuthSession> RefreshTokenAsync(string refreshToken)
    {
        var url = $"{SecureTokenBaseUrl}/token?key={Uri.EscapeDataString(_apiKey)}";
        var body = $"grant_type=refresh_token&refresh_token={Uri.EscapeDataString(refreshToken)}";

        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/x-www-form-urlencoded"
        };

        var response = await _http.FetchAsync(url, "POST", headers, body);
        if (!response.IsSuccess)
        {
            throw new AuthException("Session expired. Please sign in again.", response.Text);
        }

        using var doc = JsonDocument.Parse(response.Text);
        var root = doc.RootElement;
        var idToken = root.GetProperty("id_token").GetString() ?? string.Empty;
        var newRefreshToken = root.GetProperty("refresh_token").GetString() ?? refreshToken;
        var userId = root.GetProperty("user_id").GetString() ?? string.Empty;

        return new AuthSession(idToken, newRefreshToken, userId, null);
    }

    public Task SignOutAsync(string accessToken)
    {
        // Firebase Auth is stateless on client-side REST. Discarding tokens locally handles sign-out.
        return Task.CompletedTask;
    }

    public static (string Verifier, string Challenge) GeneratePkceS256()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        var verifier = Base64UrlEncode(bytes);
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    public string GetOAuthAuthorizeUrl(string provider, string redirectUri, string codeChallenge)
    {
        return $"https://accounts.google.com/o/oauth2/v2/auth?" +
               $"client_id=912012648755.apps.googleusercontent.com" +
               $"&response_type=code" +
               $"&scope=openid%20email%20profile" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
               $"&code_challenge_method=S256";
    }

    public async Task<AuthSession> ExchangeOAuthCodeAsync(string code, string codeVerifier)
    {
        // For Google OAuth, if the user signs in with password/email this flow is optional,
        // but provided here for compatibility.
        throw new NotSupportedException("Google sign-in via code exchange is not configured for Firebase desktop.");
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static AuthSession? ParseFirebaseSession(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var idToken = root.TryGetProperty("idToken", out var idTokenEl) 
                ? idTokenEl.GetString() ?? string.Empty 
                : string.Empty;

            var refreshToken = root.TryGetProperty("refreshToken", out var refreshEl) 
                ? refreshEl.GetString() ?? string.Empty 
                : string.Empty;

            var userId = root.TryGetProperty("localId", out var localIdEl) 
                ? localIdEl.GetString() ?? string.Empty 
                : string.Empty;

            var email = root.TryGetProperty("email", out var emailEl) 
                ? emailEl.GetString() 
                : null;

            if (string.IsNullOrEmpty(idToken)) return null;

            return new AuthSession(idToken, refreshToken, userId, email);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ExtractErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var errorEl))
            {
                if (errorEl.TryGetProperty("message", out var msgEl) && msgEl.ValueKind == JsonValueKind.String)
                {
                    return msgEl.GetString() ?? body;
                }
            }
        }
        catch (JsonException)
        {
        }
        return body;
    }

    private static string FriendlyError(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "Something went wrong. Try again.";
        if (raw.Contains("EMAIL_NOT_FOUND") || raw.Contains("INVALID_PASSWORD") || raw.Contains("INVALID_LOGIN_CREDENTIALS"))
            return "Incorrect email or password.";
        if (raw.Contains("EMAIL_EXISTS"))
            return "An account with this email already exists.";
        if (raw.Contains("USER_DISABLED"))
            return "This account has been disabled.";
        if (raw.Contains("TOO_MANY_ATTEMPTS_TRY_LATER"))
            return "Too many unsuccessful attempts. Please try again later.";
        if (raw.Contains("WEAK_PASSWORD"))
            return "Password must be at least 6 characters.";
        if (raw.Contains("INVALID_EMAIL"))
            return "Please enter a valid email address.";
        if (raw.ToLowerInvariant().Contains("network"))
            return "No internet connection.";
        return raw.Length > 120 ? raw[..120] : raw;
    }
}
