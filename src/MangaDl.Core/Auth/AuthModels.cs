namespace MangaDl.Core.Auth;

public sealed record AuthSession(string AccessToken, string RefreshToken, string UserId, string? Email);

public sealed class AuthException : Exception
{
    public string? Raw { get; }

    public AuthException(string friendlyMessage, string? raw = null) : base(friendlyMessage)
    {
        Raw = raw;
    }
}
