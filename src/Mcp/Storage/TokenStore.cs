using System.Text.Json;
using Axerie.StudentLoans.Mcp.Models;

namespace Axerie.StudentLoans.Mcp.Storage;

public sealed class TokenStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public TokenSet? Load(string accountId)
    {
        var path = AppPaths.TokenFile(accountId);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<TokenSet>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Save(string accountId, TokenSet tokens)
    {
        AppPaths.EnsureDirs();
        if (tokens.ObtainedAtUnix == 0)
            tokens.ObtainedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var json = JsonSerializer.Serialize(tokens, JsonOptions);
        var path = AppPaths.TokenFile(accountId);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }
}
