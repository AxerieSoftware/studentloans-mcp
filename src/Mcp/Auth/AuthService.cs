using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Axerie.StudentLoans.Mcp.Models;
using Axerie.StudentLoans.Mcp.Storage;

namespace Axerie.StudentLoans.Mcp.Auth;

public sealed class AuthService(HttpClient http, TokenStore tokenStore, ILogger<AuthService> logger)
{
    private const string ClientId = "mma";
    private const string RefreshScope = "openid offline_access mma.api.read";
    private static readonly TimeSpan InteractiveLoginTimeout = TimeSpan.FromMinutes(10);

    public async Task<string> EnsureAccessTokenAsync(Account account, CancellationToken ct = default)
    {
        var tokens = tokenStore.Load(account.Id);
        if (tokens?.IsAccessTokenValid() == true)
            return tokens.AccessToken!;

        if (!string.IsNullOrEmpty(tokens?.RefreshToken))
        {
            var refreshed = await TryRefreshAsync(account, tokens.RefreshToken, ct);
            if (refreshed is not null)
            {
                tokenStore.Save(account.Id, refreshed);
                return refreshed.AccessToken!;
            }
        }

        // No valid/refreshable session: force a visible browser login for this account's
        // isolated profile so the user can complete credentials + MFA themselves.
        var fresh = await InteractiveLoginAsync(account, ct);
        tokenStore.Save(account.Id, fresh);
        return fresh.AccessToken!;
    }

    private async Task<TokenSet?> TryRefreshAsync(Account account, string refreshToken, CancellationToken ct)
    {
        try
        {
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = ClientId,
                ["scope"] = RefreshScope,
            });
            using var request = new HttpRequestMessage(HttpMethod.Post, account.TokenUrl) { Content = form };
            request.Headers.Add("Origin", $"https://{account.Provider}.studentaid.gov");
            request.Headers.Add("Referer", $"https://{account.Provider}.studentaid.gov/");

            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Token refresh failed for {Account}: {Status}", account.Id, response.StatusCode);
                return null;
            }

            var tokens = await response.Content.ReadFromJsonAsync<TokenSet>(cancellationToken: ct);
            if (tokens is null) return null;
            tokens.RefreshToken ??= refreshToken;
            tokens.ObtainedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return tokens;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Token refresh threw for {Account}", account.Id);
            return null;
        }
    }

    private async Task<TokenSet> InteractiveLoginAsync(Account account, CancellationToken ct)
    {
        AppPaths.EnsureDirs();
        var profileDir = AppPaths.ProfileDir(account.Id);
        Directory.CreateDirectory(profileDir);

        using var playwright = await Playwright.CreateAsync();
        await using var context = await playwright.Chromium.LaunchPersistentContextAsync(profileDir, new()
        {
            Headless = false,
            ViewportSize = new() { Width = 1280, Height = 900 },
        });

        var tcs = new TaskCompletionSource<TokenSet>(TaskCreationOptions.RunContinuationsAsynchronously);

        context.Response += async (_, response) =>
        {
            if (tcs.Task.IsCompleted || !response.Url.Contains("/connect/token")) return;
            try
            {
                if (response.Status != 200) return;
                var tokens = System.Text.Json.JsonSerializer.Deserialize<TokenSet>(await response.TextAsync());
                if (tokens?.AccessToken is not null)
                    tcs.TrySetResult(tokens);
            }
            catch
            {
                // ignore parse errors on unrelated /connect/token traffic
            }
        };

        var page = await context.NewPageAsync();
        await page.GotoAsync(account.LoginUrl);

        logger.LogInformation(
            "Interactive login opened for {Account} ({Provider}). Complete sign-in + MFA in the browser window.",
            account.Id, account.Provider);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(InteractiveLoginTimeout);
        await using var reg = timeoutCts.Token.Register(() =>
            tcs.TrySetException(new TimeoutException(
                $"Interactive login for '{account.Id}' timed out after {InteractiveLoginTimeout.TotalMinutes} minutes.")));

        try
        {
            return await tcs.Task;
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
