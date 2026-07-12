using System.ComponentModel;
using ModelContextProtocol.Server;
using Axerie.StudentLoans.Mcp.Models;
using Axerie.StudentLoans.Mcp.Storage;

namespace Axerie.StudentLoans.Mcp;

[McpServerToolType]
public sealed class StudentLoanTools(AccountStore accountStore, LoanApiService loanApiService)
{
    private readonly AccountStore accountStore = accountStore ?? throw new ArgumentNullException(nameof(accountStore));
    private readonly LoanApiService loanApiService = loanApiService ?? throw new ArgumentNullException(nameof(loanApiService));

    [McpServerTool(Name = "list_accounts"), Description("List configured student loan accounts.")]
    public async Task<AccountsResult> ListAccountsAsync(CancellationToken cancellationToken)
    {
        return await TryAsync(
            async () => new AccountsResult(true, await this.accountStore.GetAccountsAsync(cancellationToken), null),
            error => new AccountsResult(false, [], error));
    }

    [McpServerTool(Name = "add_account"), Description("Register a new Federal Student Aid (studentaid.gov) login. One login covers every loan across every servicer.")]
    public async Task<AccountResult> AddAccountAsync(
        [Description("Human-friendly label.")] string displayName,
        CancellationToken cancellationToken)
    {
        return await TryAsync(
            async () => new AccountResult(true, await this.accountStore.AddAccountAsync(displayName, cancellationToken), null),
            error => new AccountResult(false, null, error));
    }

    [McpServerTool(Name = "remove_account"), Description("Remove a configured account and its cached session.")]
    public async Task<RemoveAccountResult> RemoveAccountAsync([Description("Account id to remove.")] Guid id, CancellationToken cancellationToken)
    {
        return await TryAsync(
            async () => new RemoveAccountResult(true, id, await this.accountStore.DeleteAccountAsync(id, cancellationToken), null),
            error => new RemoveAccountResult(false, id, false, error));
    }

    [McpServerTool(Name = "update_account"), Description("Update the display name of an existing account.")]
    public async Task<AccountResult> UpdateAccountAsync(
        [Description("Account id to update.")] Guid id,
        [Description("Human-friendly label.")] string displayName,
        CancellationToken cancellationToken)
    {
        return await TryAsync(
            async () => new AccountResult(true, await this.accountStore.UpdateAccountAsync(id, displayName, cancellationToken), null),
            error => new AccountResult(false, null, error));
    }

    [McpServerTool(Name = "get_balance"), Description("Get the current balance for one configured student loan account. Forces an interactive browser login if no valid session exists.")]
    public async Task<BalanceResult> GetBalanceAsync(
        [Description("Account id.")] Guid accountId,
        CancellationToken cancellationToken)
    {
        return await TryAsync(async () =>
        {
            var account = await this.accountStore.GetAccountAsync(accountId, cancellationToken)
                ?? throw new InvalidOperationException($"No account named '{accountId}'. Use list_accounts to see configured accounts.");

            var summary = await this.loanApiService.GetBorrowerSummaryAsync(account, cancellationToken);
            return new BalanceResult(true, account.Id, account.DisplayName, summary, null);
        },
        error => new BalanceResult(false, accountId, null, null, error));
    }

    [McpServerTool(Name = "get_all_balances"), Description("Get balances for every configured account in one call.")]
    public async Task<AllBalancesResult> GetAllBalancesAsync(CancellationToken cancellationToken)
    {
        return await TryAsync(async () =>
        {
            var accounts = await this.accountStore.GetAccountsAsync(cancellationToken);
            var results = await Task.WhenAll(accounts.Select(account => TryAsync(
                async () =>
                {
                    var summary = await this.loanApiService.GetBorrowerSummaryAsync(account, cancellationToken);
                    return new BalanceResult(true, account.Id, account.DisplayName, summary, null);
                },
                error => new BalanceResult(false, account.Id, account.DisplayName, null, error))));

            var grandTotal = results.Sum(r => r.Summary?.TotalBalance ?? 0m);
            var totalPrincipal = results.Sum(r => r.Summary?.TotalPrincipal ?? 0m);
            var allLoans = results.Where(r => r.Summary is not null).SelectMany(r => r.Summary!.Loans).ToList();
            var weightedAverageRate = LoanApiService.WeightedAverageInterestRate(allLoans);
            return new AllBalancesResult(true, results, grandTotal, totalPrincipal, weightedAverageRate, null);
        },
        error => new AllBalancesResult(false, [], 0m, 0m, null, error));
    }

    private static async Task<TResult> TryAsync<TResult>(Func<Task<TResult>> action, Func<string, TResult> onError)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return onError(ex.Message);
        }
    }
}

public sealed record AccountsResult(bool Success, IReadOnlyList<Account> Accounts, string? Error);
public sealed record AccountResult(bool Success, Account? Account, string? Error);
public sealed record RemoveAccountResult(bool Success, Guid AccountId, bool Removed, string? Error);
public sealed record BalanceResult(bool Success, Guid AccountId, string? DisplayName, LoanSummary? Summary, string? Error);
public sealed record AllBalancesResult(bool Success, IReadOnlyList<BalanceResult> Accounts, decimal GrandTotal, decimal TotalPrincipal, decimal? WeightedAverageInterestRate, string? Error);
