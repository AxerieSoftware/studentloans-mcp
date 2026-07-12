using System.Text.Json.Serialization;

namespace Axerie.StudentLoans.Mcp.Models;

/// <summary>A configured loan servicer login. Provider maps to the studentaid.gov subdomain (e.g. "nelnet", "edfinancial").</summary>
public sealed record Account(string Id, string Provider, string DisplayName)
{
    [JsonIgnore]
    public string AuthBase => $"https://auth.{this.Provider}.studentaid.gov";
    [JsonIgnore]
    public string TokenUrl => $"{this.AuthBase}/connect/token";
    [JsonIgnore]
    public string LoginUrl => $"https://{this.Provider}.studentaid.gov/account/login";
}
