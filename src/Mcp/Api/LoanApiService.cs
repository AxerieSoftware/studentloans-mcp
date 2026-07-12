using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Axerie.StudentLoans.Mcp.Models;

namespace Axerie.StudentLoans.Mcp.Api;

public sealed class LoanApiService(HttpClient http, ILogger<LoanApiService> logger)
{
    private const string BorrowerDetailsPath = "/api/1/borrower/details";
    private readonly Dictionary<string, string> _apiBaseCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<LoanSummary> GetBorrowerSummaryAsync(Account account, string accessToken, CancellationToken ct = default)
    {
        var apiBase = await ResolveApiBaseAsync(account, accessToken, ct);
        using var request = new HttpRequestMessage(HttpMethod.Get, apiBase + BorrowerDetailsPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return ParseSummary(account, doc.RootElement);
    }

    private async Task<string> ResolveApiBaseAsync(Account account, string accessToken, CancellationToken ct)
    {
        if (this._apiBaseCache.TryGetValue(account.Provider, out var cached))
            return cached;

        string[] candidates =
        [
            $"https://mmaapi.{account.Provider}.studentaid.gov",
            $"https://api.{account.Provider}.studentaid.gov",
            $"https://{account.Provider}.studentaid.gov",
        ];

        foreach (var candidate in candidates)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, candidate + BorrowerDetailsPath);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                using var response = await http.SendAsync(request, ct);
                if (response.StatusCode is System.Net.HttpStatusCode.OK
                    or System.Net.HttpStatusCode.Unauthorized
                    or System.Net.HttpStatusCode.Forbidden
                    or System.Net.HttpStatusCode.NotFound)
                {
                    this._apiBaseCache[account.Provider] = candidate;
                    return candidate;
                }
            }
            catch (HttpRequestException ex)
            {
                logger.LogDebug(ex, "API base candidate {Candidate} unreachable", candidate);
            }
        }

        var fallback = candidates[0];
        this._apiBaseCache[account.Provider] = fallback;
        return fallback;
    }

    private static LoanSummary ParseSummary(Account account, JsonElement root)
    {
        var loanElements = root
            .GetPropertyOrDefault("borrowerInfo")
            .GetPropertyOrDefault("edServicerLoans");

        var loans = new List<LoanDetail>();
        decimal total = 0m;

        if (loanElements.ValueKind == JsonValueKind.Array)
        {
            foreach (var loan in loanElements.EnumerateArray())
            {
                var principal = loan.GetMoney("currentPrincipalBalance");
                var interest = loan.GetMoney("currentInterest");
                var capitalized = loan.GetMoney("capitalizedInterest");
                var lateFees = loan.GetMoney("outstandingLateFees");
                var loanTotal = principal + interest + capitalized + lateFees;
                total += loanTotal;

                loans.Add(new LoanDetail(
                    LoanId: loan.GetStringOrDefault("loanId") ?? loan.GetStringOrDefault("loanAccountNumber") ?? loan.GetStringOrDefault("loanNumber"),
                    LoanType: loan.GetStringOrDefault("loanTypeDescription") ?? loan.GetStringOrDefault("loanType"),
                    Servicer: loan.GetStringOrDefault("servicerName") ?? loan.GetStringOrDefault("loanServicer"),
                    Principal: principal,
                    Interest: interest,
                    CapitalizedInterest: capitalized,
                    LateFees: lateFees,
                    TotalBalance: loanTotal));
            }
        }

        return new LoanSummary(account.Id, account.DisplayName, total, loans.Count, loans);
    }
}

internal static class JsonElementExtensions
{
    public static JsonElement GetPropertyOrDefault(this JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;

    public static string? GetStringOrDefault(this JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()
            : null;

    public static decimal GetMoney(this JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
            return 0m;

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(value.GetString()?.Replace(",", ""), out var parsed) => parsed,
            _ => 0m,
        };
    }
}
