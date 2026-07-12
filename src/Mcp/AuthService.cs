using System.Text.Json;
using Microsoft.Playwright;
using Axerie.StudentLoans.Mcp.Models;
using Axerie.StudentLoans.Mcp.Storage;

namespace Axerie.StudentLoans.Mcp;

public sealed class AuthService(SessionStore sessionStore)
{
    private readonly SessionStore sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));

    private const string HomeUrl = "https://studentaid.gov/";
    private const string CookieDomain = "studentaid.gov";

    // The dashboard calls this endpoint automatically right after a successful login (with MFA),
    // so watching for it succeed is a reliable, servicer-agnostic signal that the session is ready.
    private const string LoansEndpointFragment = "/app/api/nslds/student/loans";

    private static readonly TimeSpan InteractiveLoginTimeout = TimeSpan.FromMinutes(10);

    public async Task<WebSession> EnsureSessionAsync(Account account, CancellationToken cancellationToken)
    {
        var session = await this.sessionStore.GetSessionAsync(account.Id, cancellationToken);
        if (session?.IsValid() == true)
            return session;

        session = await InteractiveLoginAsync(account, cancellationToken);
        await this.sessionStore.SetSessionAsync(account.Id, session, cancellationToken);
        return session;
    }

    // Called when the server itself rejects a cached session (e.g. a 401), since that's the only
    // reliable signal that a session has actually died - there's no client-side expiry we can trust.
    public Task InvalidateSessionAsync(Guid accountId, CancellationToken cancellationToken) =>
        this.sessionStore.DeleteSessionAsync(accountId, cancellationToken);


    private async Task<WebSession> InteractiveLoginAsync(Account account, CancellationToken cancellationToken)
    {
        AppPaths.EnsureDirs();
        var profileDir = AppPaths.ProfileDir(account.Id);
        Directory.CreateDirectory(profileDir);

        using var playwright = await Playwright.CreateAsync();
        await using var context = await playwright.Chromium.LaunchPersistentContextAsync(profileDir, new()
        {
            Headless = false,
            ViewportSize = new() { Width = 1280, Height = 900 }
        });

        // Injected into every page/popup in this context so the user can tell which configured
        // account they're logging into, since studentaid.gov gives no such hint on its own.
        await context.AddInitScriptAsync(BuildAccountBannerScript(account));

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Response += (_, response) =>
        {
            if (!tcs.Task.IsCompleted && response.Status == 200 && response.Url.Contains(LoansEndpointFragment))
                tcs.TrySetResult();
        };

        // Persistent contexts start with a blank tab already open; reuse it instead of opening
        // a second "about:blank" tab alongside the login page.
        var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
        await page.GotoAsync(HomeUrl);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(InteractiveLoginTimeout);

        await using var reg = timeoutCts.Token.Register(() =>
            tcs.TrySetException(new TimeoutException($"Interactive login for '{account.Id}' timed out after {InteractiveLoginTimeout.TotalMinutes} minutes.")));

        try
        {
            await tcs.Task;
            return await CaptureSessionAsync(context, page);
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    private static async Task<WebSession> CaptureSessionAsync(IBrowserContext context, IPage page)
    {
        var cookies = await context.CookiesAsync();
        var relevant = cookies
            .Where(c => c.Domain.TrimStart('.').Equals(CookieDomain, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var xsrfToken = relevant.FirstOrDefault(c => c.Name.Equals("XSRF-TOKEN", StringComparison.OrdinalIgnoreCase))?.Value
            ?? throw new InvalidOperationException("Login succeeded but no XSRF-TOKEN cookie was captured.");

        var userAgent = await page.EvaluateAsync<string>("() => navigator.userAgent");

        return new WebSession
        {
            Cookies = relevant.Select(c => new SessionCookie(c.Name, c.Value, c.Domain, c.Path)).ToList(),
            XsrfToken = xsrfToken,
            UserAgent = userAgent
        };
    }

    private static string BuildAccountBannerScript(Account account)
    {
        var label = JsonSerializer.Serialize($"Signing in: {account.DisplayName}");
        return $$"""
            (() => {
                const label = {{label}};
                document.title = label;
                const showBanner = () => {
                    if (document.getElementById('studentloans-mcp-banner')) return;
                    const bar = document.createElement('div');
                    bar.id = 'studentloans-mcp-banner';
                    bar.textContent = label;
                    Object.assign(bar.style, {
                        position: 'fixed', top: '0', left: '0', right: '0', zIndex: '2147483647',
                        background: '#1a73e8', color: '#fff', padding: '6px 12px',
                        font: '14px sans-serif', textAlign: 'center'
                    });
                    document.body?.prepend(bar);
                };
                document.addEventListener('DOMContentLoaded', showBanner);
                showBanner();
            })();
            """;
    }
}
