using Axerie.StudentLoans.Mcp.Models;

namespace Axerie.StudentLoans.Mcp;

public sealed class LoanApiService(HttpClient httpClient)
{
    private readonly HttpClient httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    // NSLDS (the federal loan database backing studentaid.gov) aggregates every loan across every
    // servicer. The download endpoint returns a flat-text export with far more per-loan detail
    // than the JSON /loans endpoint (status, next payment due date, repayment plan, etc.).
    private const string DownloadUrl = "https://studentaid.gov/app/api/nslds/student/download";

    public async Task<LoanSummary> GetBorrowerSummaryAsync(Account account, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, DownloadUrl);
        request.Options.Set(LoanApiAuthHandler.AccountKey, account);
        request.Headers.Accept.Add(new("*/*"));

        using var response = await this.httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        var loans = NsldsDataParser.ParseLoans(text);

        var totalPrincipal = loans.Sum(l => l.Principal);
        var totalInterest = loans.Sum(l => l.Interest);
        var weightedAverageRate = WeightedAverageInterestRate(loans);

        return new LoanSummary(
            account.Id,
            account.DisplayName,
            totalPrincipal + totalInterest,
            totalPrincipal,
            totalInterest,
            weightedAverageRate,
            loans.Count,
            loans);
    }

    // Weights each loan's rate by its principal so payoff-heavy loans count more toward the average.
    internal static decimal? WeightedAverageInterestRate(IReadOnlyCollection<LoanDetail> loans)
    {
        var rated = loans.Where(l => l.InterestRate is not null && l.Principal > 0).ToList();
        if (rated.Count == 0)
            return null;

        var weightedSum = rated.Sum(l => l.InterestRate!.Value * l.Principal);
        var principalSum = rated.Sum(l => l.Principal);
        return principalSum == 0 ? null : weightedSum / principalSum;
    }
}
