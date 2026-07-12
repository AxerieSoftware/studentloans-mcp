using System.Text.Json;
using Axerie.StudentLoans.Mcp.Models;

namespace Axerie.StudentLoans.Mcp.Storage;

public sealed class AccountStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public IReadOnlyList<Account> Load()
    {
        AppPaths.EnsureDirs();
        if (!File.Exists(AppPaths.AccountsFile))
            return [];

        var json = File.ReadAllText(AppPaths.AccountsFile);
        return JsonSerializer.Deserialize<List<Account>>(json) ?? [];
    }

    public void Save(IReadOnlyList<Account> accounts)
    {
        AppPaths.EnsureDirs();
        var json = JsonSerializer.Serialize(accounts, JsonOptions);
        var tmp = AppPaths.AccountsFile + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, AppPaths.AccountsFile, overwrite: true);
    }

    public Account? Find(string accountId) =>
        Load().FirstOrDefault(a => a.Id.Equals(accountId, StringComparison.OrdinalIgnoreCase));

    public Account Add(string id, string provider, string displayName)
    {
        var accounts = Load().ToList();
        if (accounts.Any(a => a.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Account '{id}' already exists.");

        var account = new Account(id, provider.Trim().ToLowerInvariant(), displayName);
        accounts.Add(account);
        Save(accounts);
        return account;
    }

    public bool Remove(string id)
    {
        var accounts = Load().ToList();
        var removed = accounts.RemoveAll(a => a.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) > 0;
        if (removed)
        {
            Save(accounts);
            var tokenFile = AppPaths.TokenFile(id);
            if (File.Exists(tokenFile)) File.Delete(tokenFile);
        }
        return removed;
    }
}
