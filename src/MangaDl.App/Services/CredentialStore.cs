using Windows.Security.Credentials;

namespace MangaDl.Services;

/// <summary>Access/refresh tokens live in Windows Credential Manager via PasswordVault,
/// never in the plain-JSON settings.json alongside everything else.</summary>
public static class CredentialStore
{
    private const string Resource = "manga-dl";
    private const string AccessTokenUser = "access_token";
    private const string RefreshTokenUser = "refresh_token";

    public static void Save(string accessToken, string refreshToken)
    {
        var vault = new PasswordVault();
        Clear();
        vault.Add(new PasswordCredential(Resource, AccessTokenUser, accessToken));
        vault.Add(new PasswordCredential(Resource, RefreshTokenUser, refreshToken));
    }

    public static (string? AccessToken, string? RefreshToken) Load()
    {
        var vault = new PasswordVault();
        string? access = null, refresh = null;
        try
        {
            var cred = vault.Retrieve(Resource, AccessTokenUser);
            cred.RetrievePassword();
            access = cred.Password;
        }
        catch { /* not present */ }

        try
        {
            var cred = vault.Retrieve(Resource, RefreshTokenUser);
            cred.RetrievePassword();
            refresh = cred.Password;
        }
        catch { /* not present */ }

        return (access, refresh);
    }

    public static void Clear()
    {
        var vault = new PasswordVault();
        foreach (var user in new[] { AccessTokenUser, RefreshTokenUser })
        {
            try
            {
                vault.Remove(vault.Retrieve(Resource, user));
            }
            catch { /* not present */ }
        }
    }

    public static void SaveTrackerToken(string trackerKey, string token)
    {
        var vault = new PasswordVault();
        ClearTrackerToken(trackerKey);
        vault.Add(new PasswordCredential(Resource, trackerKey, token));
    }

    public static string? LoadTrackerToken(string trackerKey)
    {
        var vault = new PasswordVault();
        try
        {
            var cred = vault.Retrieve(Resource, trackerKey);
            cred.RetrievePassword();
            return cred.Password;
        }
        catch { return null; }
    }

    public static void ClearTrackerToken(string trackerKey)
    {
        var vault = new PasswordVault();
        try
        {
            vault.Remove(vault.Retrieve(Resource, trackerKey));
        }
        catch { /* not present */ }
    }
}
