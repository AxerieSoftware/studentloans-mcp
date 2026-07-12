namespace Axerie.StudentLoans.Mcp.Storage;

public static class AppPaths
{
    public static string RootDir { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".studentloanmcp");
    public static string AccountsFile => Path.Combine(RootDir, "accounts.json");
    public static string SessionsDir => Path.Combine(RootDir, "tokens");
    public static string ProfilesDir => Path.Combine(RootDir, "profiles");
    public static string KeysDir => Path.Combine(RootDir, "keys");

    public static string SessionFile(Guid accountId) => Path.Combine(SessionsDir, $"{accountId}.json");
    public static string ProfileDir(Guid accountId) => Path.Combine(ProfilesDir, accountId.ToString());

    public static void EnsureDirs()
    {
        CreateOwnerOnlyDir(RootDir);
        CreateOwnerOnlyDir(SessionsDir);
        CreateOwnerOnlyDir(ProfilesDir);
        CreateOwnerOnlyDir(KeysDir);
    }

    // Session data/keys are sensitive; restrict them to the owning user (no-op on Windows, which
    // already scopes the user profile directory via ACLs).
    private static void CreateOwnerOnlyDir(string path)
    {
        Directory.CreateDirectory(path);

        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}
