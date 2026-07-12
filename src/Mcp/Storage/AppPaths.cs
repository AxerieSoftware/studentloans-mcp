namespace Axerie.StudentLoans.Mcp.Storage;

/// <summary>Centralizes on-disk locations under ~/.studentloanmcp, isolated per account.</summary>
public static class AppPaths
{
    public static string RootDir { get; } =
        Environment.GetEnvironmentVariable("STUDENT_LOAN_MCP_HOME")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".studentloanmcp");

    public static string AccountsFile => Path.Combine(RootDir, "accounts.json");

    public static string TokensDir => Path.Combine(RootDir, "tokens");

    public static string ProfilesDir => Path.Combine(RootDir, "profiles");

    public static string TokenFile(string accountId) => Path.Combine(TokensDir, $"{accountId}.json");

    /// <summary>A dedicated persistent Playwright browser profile per account, so each account's
    /// "remember this device" cookie/local-storage state stays isolated (fixes Nelnet only
    /// remembering one login when multiple accounts share a browser profile).</summary>
    public static string ProfileDir(string accountId) => Path.Combine(ProfilesDir, accountId);

    public static void EnsureDirs()
    {
        Directory.CreateDirectory(RootDir);
        Directory.CreateDirectory(TokensDir);
        Directory.CreateDirectory(ProfilesDir);
    }
}
