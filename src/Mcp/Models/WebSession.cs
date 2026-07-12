namespace Axerie.StudentLoans.Mcp.Models;

public sealed class WebSession
{
    public List<SessionCookie> Cookies { get; set; } = [];
    public string XsrfToken { get; set; } = "";

    // Some anti-bot protections tie a session to the User-Agent that established it; replaying
    // requests with a different (or absent) one can get them rejected outright, so we capture the
    // real browser's User-Agent at login and reuse it for API calls.
    public string UserAgent { get; set; } = "";

    // No client-side expiry: a 401 from the server is the only reliable signal that a cached
    // session has actually died, and that's handled by re-authenticating and retrying the call.
    public bool IsValid() =>
        this.Cookies.Count > 0
        && !string.IsNullOrEmpty(this.XsrfToken)
        && !string.IsNullOrEmpty(this.UserAgent);
}

public sealed record SessionCookie(string Name, string Value, string Domain, string Path);
