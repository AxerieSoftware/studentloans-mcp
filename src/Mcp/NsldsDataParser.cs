using Axerie.StudentLoans.Mcp.Models;

namespace Axerie.StudentLoans.Mcp;

// Parses the flat "Key:Value" NSLDS text export (studentaid.gov's /app/api/nslds/student/download
// endpoint), which carries far more per-loan detail than the JSON /loans endpoint.
internal static class NsldsDataParser
{
    private const string LoanBlockStartKey = "Loan Type Code";
    private const string ContactBlockStartKey = "Loan Contact Type";
    private const string ContactNameKey = "Loan Contact Name";
    private const string MostRelevantKey = "Most Relevant";

    public static IReadOnlyList<LoanDetail> ParseLoans(string text)
    {
        var lines = text
            .Split('\n')
            .Select(ParseLine)
            .Where(l => l is not null)
            .Select(l => l!.Value)
            .ToList();

        var loans = new List<LoanDetail>();
        var blockStart = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Key != LoanBlockStartKey) continue;

            if (blockStart >= 0)
                loans.Add(ParseLoanBlock(lines, blockStart, i));
            blockStart = i;
        }

        if (blockStart >= 0)
            loans.Add(ParseLoanBlock(lines, blockStart, lines.Count));

        return loans;
    }

    private static (string Key, string Value)? ParseLine(string line)
    {
        var trimmed = line.TrimEnd('\r');
        var separatorIndex = trimmed.IndexOf(':');
        return separatorIndex < 0 ? null : (trimmed[..separatorIndex].Trim(), trimmed[(separatorIndex + 1)..].Trim());
    }

    private static LoanDetail ParseLoanBlock(List<(string Key, string Value)> lines, int start, int end)
    {
        // Some keys repeat within a block (status/disbursement history); "current state" is
        // already surfaced via dedicated single-value keys, so just keep the first occurrence.
        var fields = new Dictionary<string, string>();
        for (var i = start; i < end; i++)
            fields.TryAdd(lines[i].Key, lines[i].Value);

        var principal = GetMoney(fields, "Loan Outstanding Principal Balance");
        var interest = GetMoney(fields, "Loan Outstanding Interest Balance");

        return new LoanDetail(
            LoanId: GetString(fields, "Loan Award ID"),
            LoanType: GetString(fields, "Loan Type Description"),
            Servicer: ResolveServicer(lines, start, end),
            InterestRate: GetPercent(fields, "Loan Interest Rate"),
            Principal: principal,
            Interest: interest,
            TotalBalance: principal + interest,
            Status: GetString(fields, "Current Loan Status Description"),
            BalanceAsOfDate: GetDate(fields, "Loan Outstanding Principal Balance as of Date"),
            NextPaymentDueDate: GetDate(fields, "Loan Next Payment Due Date"),
            RepaymentPlanType: GetString(fields, "Loan Repayment Plan Type Code Description"),
            RepaymentPlanScheduledAmount: GetMoneyOrNull(fields, "Loan Repayment Plan Scheduled Amount"),
            DelinquencyDate: GetDate(fields, "Loan Delinquency Date"),
            PslfCumulativeMatchedMonths: GetInt(fields, "Loan PSLF Cumulative Matched Months"));
    }

    // A loan can list multiple servicing contacts (current + historical); prefer the one flagged
    // most relevant, falling back to the first contact block if none is flagged.
    private static string? ResolveServicer(List<(string Key, string Value)> lines, int start, int end)
    {
        string? fallback = null;
        string? currentName = null;
        for (var i = start; i < end; i++)
        {
            var (key, value) = lines[i];
            if (key == ContactBlockStartKey)
                currentName = null;
            else if (key == ContactNameKey)
                currentName = value;
            else if (key == MostRelevantKey && value.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                return currentName;

            fallback ??= currentName;
        }

        return fallback;
    }

    private static string? GetString(Dictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) && value.Length > 0 ? value : null;

    private static decimal GetMoney(Dictionary<string, string> fields, string key) =>
        GetMoneyOrNull(fields, key) ?? 0m;

    private static decimal? GetMoneyOrNull(Dictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) && decimal.TryParse(value.Replace("$", "").Replace(",", ""), out var parsed)
            ? parsed
            : null;

    private static decimal? GetPercent(Dictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) && decimal.TryParse(value.Replace("%", ""), out var parsed)
            ? parsed
            : null;

    private static DateOnly? GetDate(Dictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) && DateOnly.TryParseExact(value, "MM/dd/yyyy", out var parsed)
            ? parsed
            : null;

    private static int? GetInt(Dictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : null;
}
