using Axerie.StudentLoans.Mcp.Models;

namespace Axerie.StudentLoans.Mcp;

public sealed class LoanApiAuthHandler(AuthService authService) : DelegatingHandler
{
    private readonly AuthService authService = authService ?? throw new ArgumentNullException(nameof(authService));

    public static readonly HttpRequestOptionsKey<Account> AccountKey = new($"{nameof(LoanApiAuthHandler)}.Account");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Options.TryGetValue(AccountKey, out var account))
            return await base.SendAsync(request, cancellationToken);

        var session = await this.authService.EnsureSessionAsync(account, cancellationToken);
        ApplySessionHeaders(request, session);

        var response = await base.SendAsync(request, cancellationToken);

        // A cached session can be stale even when we think it's still valid (there's no reliable
        // client-side expiry for it); 401/403 are our real signal to drop it and log in again.
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            response.Dispose();
            await this.authService.InvalidateSessionAsync(account.Id, cancellationToken);
            session = await this.authService.EnsureSessionAsync(account, cancellationToken);

            using var retry = await CloneAsync(request);
            ApplySessionHeaders(retry, session);
            return await base.SendAsync(retry, cancellationToken);
        }

        return response;
    }

    private static void ApplySessionHeaders(HttpRequestMessage request, WebSession session)
    {
        request.Headers.Remove("Cookie");
        request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", session.Cookies.Select(c => $"{c.Name}={c.Value}")));
        request.Headers.Remove("x-xsrf-token");
        request.Headers.TryAddWithoutValidation("x-xsrf-token", session.XsrfToken);

        // studentaid.gov's WAF appears to reject API calls that don't look like they came from the
        // same browser that established the session, so replay the User-Agent captured at login.
        request.Headers.Remove("User-Agent");
        if (!string.IsNullOrEmpty(session.UserAgent))
            request.Headers.TryAddWithoutValidation("User-Agent", session.UserAgent);

        request.Headers.Remove("Referer");
        request.Headers.TryAddWithoutValidation("Referer", "https://studentaid.gov/");
    }

    // HttpRequestMessage can't be resent once sent, so rebuild an equivalent one for the retry.
    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        clone.Options.Set(AccountKey, request.Options.TryGetValue(AccountKey, out var account) ? account : null!);

        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bytes);

            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
