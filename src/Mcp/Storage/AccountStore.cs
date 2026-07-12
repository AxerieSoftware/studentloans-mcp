using System.Text.Json;
using Axerie.StudentLoans.Mcp.Models;

namespace Axerie.StudentLoans.Mcp.Storage;

public sealed class AccountStore
{
    private readonly SemaphoreSlim sync = new(1, 1);

    public async Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken)
    {
        AppPaths.EnsureDirs();
        if (!File.Exists(AppPaths.AccountsFile))
            return [];

        await using var stream = File.OpenRead(AppPaths.AccountsFile);
        return await JsonSerializer.DeserializeAsync<List<Account>>(stream, JsonSerializerOptions.Web, cancellationToken) ?? [];
    }

    public async Task<Account?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        (await GetAccountsAsync(cancellationToken)).FirstOrDefault(a => a.Id == accountId);

    public async Task<Account> AddAccountAsync(string displayName, CancellationToken cancellationToken)
    {
        await this.sync.WaitAsync(cancellationToken);
        try
        {
            var accounts = (await GetAccountsAsync(cancellationToken)).ToList();
            var account = new Account(Guid.NewGuid(), displayName);
            accounts.Add(account);
            await StoreAccountsAsync(accounts, cancellationToken);

            return account;
        }
        finally
        {
            this.sync.Release();
        }
    }

    public async Task<Account> UpdateAccountAsync(Guid id, string displayName, CancellationToken cancellationToken)
    {
        await this.sync.WaitAsync(cancellationToken);
        try
        {
            var accounts = (await GetAccountsAsync(cancellationToken)).ToList();
            var index = accounts.FindIndex(a => a.Id == id);
            if (index < 0)
                throw new InvalidOperationException($"Account '{id}' does not exist.");

            var account = new Account(id, displayName);
            accounts[index] = account;
            await StoreAccountsAsync(accounts, cancellationToken);
            return account;
        }
        finally
        {
            this.sync.Release();
        }
    }

    public async Task<bool> DeleteAccountAsync(Guid id, CancellationToken cancellationToken)
    {
        await this.sync.WaitAsync(cancellationToken);
        try
        {
            var accounts = (await GetAccountsAsync(cancellationToken)).ToList();
            var removed = accounts.RemoveAll(a => a.Id == id) > 0;
            if (removed)
            {
                await StoreAccountsAsync(accounts, cancellationToken);
                var sessionFile = AppPaths.SessionFile(id);
                if (File.Exists(sessionFile)) File.Delete(sessionFile);
            }

            return removed;
        }
        finally
        {
            this.sync.Release();
        }
    }

    private async Task StoreAccountsAsync(List<Account> accounts, CancellationToken cancellationToken)
    {
        AppPaths.EnsureDirs();
        var json = JsonSerializer.Serialize(accounts, JsonSerializerOptions.Web);
        var tmp = $"{AppPaths.AccountsFile}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(tmp, json, cancellationToken);
        File.Move(tmp, AppPaths.AccountsFile, overwrite: true);
    }
}
