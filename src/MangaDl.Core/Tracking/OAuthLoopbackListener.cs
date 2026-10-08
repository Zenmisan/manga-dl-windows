using System.Net;
using System.Text;

namespace MangaDl.Core.Tracking;

/// <summary>
/// A lightweight local HTTP loopback server that listens for OAuth2 redirection
/// callbacks from services like AniList and MyAnimeList.
/// </summary>
public sealed class OAuthLoopbackListener : IDisposable
{
    private readonly int _port;
    private HttpListener? _listener;

    public OAuthLoopbackListener(int port = 5678)
    {
        _port = port;
    }

    /// <summary>
    /// Starts listening on http://localhost:{port}/ and waits for an incoming OAuth callback request.
    /// Returns the authorization code extracted from the query parameters, or null if timed out or failed.
    /// </summary>
    public async Task<string?> WaitForAuthCodeAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var prefix = $"http://localhost:{_port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(prefix);

        try
        {
            _listener.Start();
        }
        catch (Exception ex)
        {
            // Port might be in use or permission denied
            throw new InvalidOperationException($"Could not start OAuth loopback listener on {prefix}: {ex.Message}", ex);
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            // Wait for context with cancellation support
            var contextTask = _listener.GetContextAsync();
            var completed = await Task.WhenAny(contextTask, Task.Delay(timeout, cts.Token));

            if (completed != contextTask)
            {
                return null; // Timed out
            }

            var context = await contextTask;
            var req = context.Request;
            var resp = context.Response;

            // Extract code and error from query string
            var code = req.QueryString["code"];
            var error = req.QueryString["error"];

            // Format a friendly response page for the user's browser
            var html = GenerateResponseHtml(string.IsNullOrEmpty(error) && !string.IsNullOrEmpty(code));
            var buffer = Encoding.UTF8.GetBytes(html);

            resp.StatusCode = (int)HttpStatusCode.OK;
            resp.ContentType = "text/html; charset=utf-8";
            resp.ContentLength64 = buffer.Length;

            await resp.OutputStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
            resp.OutputStream.Close();

            return code;
        }
        finally
        {
            Stop();
        }
    }

    public void Stop()
    {
        try
        {
            if (_listener != null && _listener.IsListening)
            {
                _listener.Stop();
            }
        }
        catch { }

        try
        {
            _listener?.Close();
        }
        catch { }
        finally
        {
            _listener = null;
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private static string GenerateResponseHtml(bool success)
    {
        var title = success ? "Connected to manga-dl" : "Authorization Error";
        var heading = success ? "Authorization Successful!" : "Connection Failed";
        var message = success
            ? "Your account was connected successfully. You can close this tab and return to the manga-dl app."
            : "An error occurred during authentication. Please return to the app and try again.";
        var accentColor = success ? "#3fb950" : "#f85149";

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{title}}</title>
              <style>
                * { box-sizing: border-box; margin: 0; padding: 0; }
                body {
                  background-color: #0d1117;
                  color: #e6edf3;
                  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
                  display: flex;
                  align-items: center;
                  justify-content: center;
                  min-height: 100vh;
                  padding: 24px;
                }
                .card {
                  background: #161b22;
                  border: 1px solid #30363d;
                  border-radius: 16px;
                  padding: 40px;
                  max-width: 440px;
                  width: 100%;
                  text-align: center;
                  box-shadow: 0 16px 32px rgba(0,0,0,0.4);
                }
                .icon {
                  width: 48px;
                  height: 48px;
                  border-radius: 50%;
                  background: {{accentColor}}22;
                  color: {{accentColor}};
                  display: inline-flex;
                  align-items: center;
                  justify-content: center;
                  font-size: 24px;
                  font-weight: bold;
                  margin-bottom: 20px;
                }
                h1 {
                  font-size: 20px;
                  font-weight: 800;
                  margin-bottom: 10px;
                  color: #ffffff;
                }
                p {
                  font-size: 14px;
                  line-height: 1.6;
                  color: #8b949e;
                }
              </style>
            </head>
            <body>
              <div class="card">
                <div class="icon">{{(success ? "✓" : "✕")}}</div>
                <h1>{{heading}}</h1>
                <p>{{message}}</p>
              </div>
            </body>
            </html>
            """;
    }
}
