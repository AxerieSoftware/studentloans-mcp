using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Axerie.StudentLoans.Mcp.Models;

namespace Axerie.StudentLoans.Mcp.Storage;

public sealed class SessionStore(IDataProtectionProvider dataProtectionProvider)
{
    private readonly IDataProtector protector = (dataProtectionProvider ?? throw new ArgumentNullException(nameof(dataProtectionProvider))).CreateProtector("Axerie.StudentLoans.Mcp.SessionStore");

    public async Task<WebSession?> GetSessionAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var path = AppPaths.SessionFile(accountId);
        if (!File.Exists(path)) return null;
        try
        {
            var encrypted = await File.ReadAllTextAsync(path, cancellationToken);
            var json = this.protector.Unprotect(encrypted);
            return JsonSerializer.Deserialize<WebSession>(json, JsonSerializerOptions.Web);
        }
        catch (Exception ex) when (ex is JsonException or CryptographicException or FormatException)
        {
            return null;
        }
    }

    public async Task SetSessionAsync(Guid accountId, WebSession session, CancellationToken cancellationToken)
    {
        AppPaths.EnsureDirs();
        var json = JsonSerializer.Serialize(session, JsonSerializerOptions.Web);
        var encrypted = this.protector.Protect(json);
        var path = AppPaths.SessionFile(accountId);
        var tmp = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(tmp, encrypted, cancellationToken);

        File.Move(tmp, path, overwrite: true);
    }

    public Task DeleteSessionAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var path = AppPaths.SessionFile(accountId);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}
