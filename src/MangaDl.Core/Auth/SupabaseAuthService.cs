using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MangaDl.Core.Http;

namespace MangaDl.Core.Auth;

public sealed record AuthSignUpResult(AuthSession? Session, string? Message);

/// <summary>
/// Thin REST client for Supabase Auth (GoTrue). Mirrors what the web app does with
/// supabase-js and what manga-dl-android does with supabase-kt — same endpoints,
/// same user-metadata shape for sign-up, same friendly-error mapping.
/// </summary>
public sealed class SupabaseAuthService
{
    private readonly HttpService _http;
    private readonly string _url;
    private readonly string _anonKey;

    public SupabaseAuthService(HttpService http, string supabaseUrl, string anonKey)
    {
        _http = http;
        _url = supabaseUrl.TrimEnd('/');
        _anonKey = anonKey;
    }

    public async Task<AuthSession> SignInWithPasswordAsync(string email, string password)
    {
        var body = JsonSerializer.Serialize(new { email, password });
        var result = await PostAsync("/auth/v1/token?grant_type=password", body, null);
        return ParseSession(result) ?? throw new AuthException("Something went wrong. Try again.");
    }

    // ----------------------------------------------------
    // OAuth providers (Google, etc) — hosted flow
    // ----------------------------------------------------
    // Supabase's own /auth/v1/authorize endpoint does the whole provider exchange
    // server-side, using whatever client id/secret is already configured under
    // Authentication > Providers in the Supabase dashboard (the same one the web
    // app's "Continue with Google" button relies on). Windows never needs its own
    // Google OAuth client or secret — it only needs to capture the redirect (via
    // the loopback listener, same as the AniList/MAL tracker flows) and hand the
    // resulting code back to Supabase. This is exactly what supabase-js's
    // signInWithOAuth() + exchangeCodeForSession() do under the hood.

    public static (string Verifier, string Challenge) GeneratePkceS256()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        var verifier = Base64UrlEncode(bytes);
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    /// <summary>Builds the URL to open in the system browser. <paramref name="redirectUri"/>
    /// should point at the loopback listener (e.g. http://localhost:5678/google-callback).</summary>
    public string GetOAuthAuthorizeUrl(string provider, string redirectUri, string codeChallenge)
    {
        return $"{_url}/auth/v1/authorize?provider={Uri.EscapeDataString(provider)}" +
               $"&redirect_to={Uri.EscapeDataString(redirectUri)}" +
               $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
               "&code_challenge_method=S256";
    }

    /// <summary>Exchanges the code the loopback listener captured for a Supabase session.</summary>
    public async Task<AuthSession> ExchangeOAuthCodeAsync(string code, string codeVerifier)
    {
        var body = JsonSerializer.Serialize(new { auth_code = code, code_verifier = codeVerifier });
        var result = await PostAsync("/auth/v1/token?grant_type=pkce", body, null);
        return ParseSession(result) ?? throw new AuthException("Sign-in didn't return a session.");
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public async Task<AuthSignUpResult> SignUpAsync(string username, string email, string password)
    {
        var body = JsonSerializer.Serialize(new
        {
            email,
            password,
            data = new { username }
        });
        var result = await PostAsync("/auth/v1/signup", body, null);
        var session = ParseSession(result);
        if (session != null) return new AuthSignUpResult(session, null);

        // No tokens back usually means email confirmation is required — not a failure.
        return new AuthSignUpResult(null, "Check your email to confirm your account, then sign in.");
    }

    public async Task RequestPasswordResetAsync(string email)
    {
        var body = JsonSerializer.Serialize(new { email });
        await PostAsync("/auth/v1/recover", body, null);
    }

    public async Task SignOutAsync(string accessToken)
    {
        try
        {
            await PostAsync("/auth/v1/logout", "{}", accessToken);
        }
        catch
        {
            // Best-effort: the user is signing out locally regardless of server state.
        }
    }

    private async Task<HttpResponseResult> PostAsync(string path, string body, string? accessToken)
    {
        var headers = new Dictionary<string, string>
        {
            ["apikey"] = _anonKey,
            ["Content-Type"] = "application/json",
            ["Authorization"] = $"Bearer {accessToken ?? _anonKey}"
        };

        var result = await _http.FetchAsync($"{_url}{path}", "POST", headers, body);
        if (!result.IsSuccess)
        {
            throw new AuthException(FriendlyError(ExtractErrorMessage(result.Text)), result.Text);
        }
        return result;
    }

    private static AuthSession? ParseSession(HttpResponseResult result)
    {
        try
        {
            using var doc = JsonDocument.Parse(result.Text);
            var root = doc.RootElement;
            if (!root.TryGetProperty("access_token", out var accessTokenEl)) return null;

            var accessToken = accessTokenEl.GetString() ?? string.Empty;
            var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? string.Empty : string.Empty;

            string userId = string.Empty;
            string? email = null;
            if (root.TryGetProperty("user", out var userEl))
            {
                userId = userEl.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty;
                email = userEl.TryGetProperty("email", out var emailEl) ? emailEl.GetString() : null;
            }

            return new AuthSession(accessToken, refreshToken, userId, email);
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
            foreach (var key in new[] { "error_description", "msg", "error", "message" })
            {
                if (root.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String)
                    return el.GetString() ?? body;
            }
        }
        catch (JsonException)
        {
            // Not JSON — fall through to raw body.
        }
        return body;
    }

    /// <summary>Ported from manga-dl-android's AuthViewModel.friendlyError — same raw-string matches,
    /// so the message a user sees is consistent across platforms.</summary>
    private static string FriendlyError(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "Something went wrong. Try again.";
        if (raw.Contains("Invalid login credentials")) return "Incorrect email or password.";
        if (raw.Contains("Email not confirmed")) return "Check your email and confirm your account first.";
        if (raw.Contains("User already registered")) return "An account with this email already exists.";
        if (raw.Contains("Password should be at least")) return "Password must be at least 6 characters.";
        if (raw.Contains("Unable to validate")) return "Session expired. Please sign in again.";
        if (raw.ToLowerInvariant().Contains("network")) return "No internet connection.";
        return raw.Length > 120 ? raw[..120] : raw;
    }
}
