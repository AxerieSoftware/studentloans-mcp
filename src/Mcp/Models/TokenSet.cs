using System.Text.Json.Serialization;

namespace Axerie.StudentLoans.Mcp.Models;

public sealed class TokenSet
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; } = 3600;

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("obtained_at")]
    public long ObtainedAtUnix { get; set; }

    private const int SkewSeconds = 60;

    public bool IsAccessTokenValid() =>
        !string.IsNullOrEmpty(this.AccessToken)
        && this.ObtainedAtUnix + this.ExpiresIn - SkewSeconds > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
