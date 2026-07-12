using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using Axerie.StudentLoans.Mcp.Api;
using Axerie.StudentLoans.Mcp.Auth;
using Axerie.StudentLoans.Mcp.Storage;

namespace Axerie.StudentLoans.Mcp.Tools;

[McpServerToolType]
public sealed class StudentLoanTools(AccountStore accounts, AuthService auth, LoanApiService loanApi)
{
    [McpServerTool, Description("List configured student loan accounts (e.g. Nelnet logins for each spouse, Edfinancial parent PLUS loan).")]
    public string ListAccounts()
    {
        var list = accounts.Load();
        if (list.Count == 0)
            return "No accounts configured. Use add_account to register one (e.g. id='andrew-nelnet', provider='nelnet').";

        var sb = new StringBuilder();
        foreach (var a in list)
            sb.AppendLine($"- {a.Id} ({a.DisplayName}) — provider: {a.Provider}");
        return sb.ToString();
    }

    [McpServerTool, Description("Register a new loan servicer account. Provider must match the studentaid.gov subdomain, e.g. 'nelnet' or 'edfinancial'.")]
    public string AddAccount(
        [Description("Unique short id, e.g. 'andrew-nelnet', 'kylie-nelnet', 'parent-edfinancial'.")] string id,
        [Description("Servicer subdomain: 'nelnet' or 'edfinancial'.")] string provider,
        [Description("Human-friendly label.")] string displayName)
    {
        var account = accounts.Add(id, provider, displayName);
        return $"Added account '{account.Id}' for provider '{account.Provider}'.";
    }

    [McpServerTool, Description("Remove a configured account and its cached tokens.")]
    public string RemoveAccount([Description("Account id to remove.")] string id) =>
        accounts.Remove(id) ? $"Removed account '{id}'." : $"No account named '{id}' found.";

    [McpServerTool, Description("Get the current balance for one configured student loan account. Forces an interactive browser login if no valid session exists.")]
    public async Task<string> GetBalance(
        [Description("Account id, e.g. 'andrew-nelnet'.")] string accountId,
        CancellationToken ct)
    {
        var account = accounts.Find(accountId)
            ?? throw new ArgumentException($"No account named '{accountId}'. Use list_accounts to see configured accounts.");

        var token = await auth.EnsureAccessTokenAsync(account, ct);
        var summary = await loanApi.GetBorrowerSummaryAsync(account, token, ct);
        return Format(summary);
    }

    [McpServerTool, Description("Get balances for every configured account (all Nelnet + Edfinancial logins) in one call.")]
    public async Task<string> GetAllBalances(CancellationToken ct)
    {
        var list = accounts.Load();
        if (list.Count == 0)
            return "No accounts configured. Use add_account first.";

        var sb = new StringBuilder();
        decimal grandTotal = 0m;
        foreach (var account in list)
        {
            try
            {
                var token = await auth.EnsureAccessTokenAsync(account, ct);
                var summary = await loanApi.GetBorrowerSummaryAsync(account, token, ct);
                grandTotal += summary.TotalBalance;
                sb.AppendLine(Format(summary));
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{account.DisplayName} ({account.Id}): failed — {ex.Message}");
                sb.AppendLine();
            }
        }
        sb.AppendLine($"Combined total across all accounts: {grandTotal:C}");
        return sb.ToString();
    }

    private static string Format(Models.LoanSummary summary)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{summary.DisplayName} ({summary.AccountId}): {summary.LoanCount} loan(s), total {summary.TotalBalance:C}");
        foreach (var loan in summary.Loans)
            sb.AppendLine($"  - {loan.LoanId} [{loan.LoanType}] principal {loan.Principal:C}, interest {loan.Interest:C}, total {loan.TotalBalance:C}");
        return sb.ToString();
    }
}
