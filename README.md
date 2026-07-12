# studentloans-mcp

An MCP (Model Context Protocol) server that lets an AI agent check your federal student loan
balances via `studentaid.gov` — the same NSLDS database that aggregates every loan across
every servicer (Nelnet, MOHELA, Edfinancial, Aidvantage, etc.) behind a single Federal
Student Aid login.

Because that login is MFA-gated and there's no public API, the server drives a real,
visible browser (via Playwright) for the first login on each account and captures the
resulting session cookies. After that, the session is reused silently in the background —
no browser needed until it expires, at which point you'll be prompted to log in again.

## MCP tools

| Tool               | Description                                                                     |
| ------------------ | ------------------------------------------------------------------------------- |
| `list_accounts`    | List configured accounts.                                                       |
| `add_account`      | Register a new account (server generates the account id).                      |
| `update_account`   | Update an existing account's display name.                                      |
| `remove_account`   | Remove an account and its cached session.                                       |
| `get_balance`      | Get the current balance for one account (triggers interactive login if needed). |
| `get_all_balances` | Get balances for every configured account in one call.                          |

## Requirements

- .NET 10 SDK
- Playwright browser binaries (installed automatically on first interactive login, or run
  `pwsh src/Mcp/bin/Debug/net10.0/playwright.ps1 install chromium` after building)

## Running

```bash
dotnet run --project src/Mcp
```

The server communicates over stdio, so point your MCP client (Claude Desktop, VS Code, etc.)
at the built executable. Logs go to stderr — stdout is reserved for the MCP protocol.

## Connecting an AI client

All MCP clients need the same two things: a command to launch the server, and its
arguments. Use `dotnet` with `run --project` (pointing at the absolute path of this repo)
so you don't need to publish a standalone binary first.

### Claude Desktop

Add to `claude_desktop_config.json` (Settings → Developer → Edit Config):

```json
{
    "mcpServers": {
        "studentloans": {
            "command": "dotnet",
            "args": [
                "run",
                "--project",
                "/absolute/path/to/studentloans-mcp/src/Mcp"
            ]
        }
    }
}
```

### Claude Code

```bash
claude mcp add studentloans -- dotnet run --project /absolute/path/to/studentloans-mcp/src/Mcp
```

### GitHub Copilot (VS Code)

Add to `.vscode/mcp.json` in your workspace, or your user `mcp.json` (Command Palette →
"MCP: Open User Configuration"):

```json
{
    "servers": {
        "studentloans": {
            "type": "stdio",
            "command": "dotnet",
            "args": [
                "run",
                "--project",
                "/absolute/path/to/studentloans-mcp/src/Mcp"
            ]
        }
    }
}
```

### GitHub Copilot CLI / Copilot app

```bash
copilot mcp add studentloans -- dotnet run --project /absolute/path/to/studentloans-mcp/src/Mcp
```

Or edit `~/.copilot/mcp-config.json` directly:

```json
{
    "mcpServers": {
        "studentloans": {
            "type": "local",
            "command": "dotnet",
            "args": [
                "run",
                "--project",
                "/absolute/path/to/studentloans-mcp/src/Mcp"
            ],
            "tools": ["*"]
        }
    }
}
```

### Codex CLI

Add to `~/.codex/config.toml`:

```toml
[mcp_servers.studentloans]
command = "dotnet"
args = ["run", "--project", "/absolute/path/to/studentloans-mcp/src/Mcp"]
```

### Other clients

Any client that supports stdio-based MCP servers works the same way: run `dotnet` with
`run --project /absolute/path/to/studentloans-mcp/src/Mcp` as the command/args.

## Adding an account

Use `add_account` with a display name (this is just a label — one Federal Student Aid login
covers every loan across every servicer, so there's no provider/subdomain to configure). The
first `get_balance` call for a new account will open a visible browser window for you to log
in (including any MFA step); subsequent calls reuse the session automatically. The browser
window shows a banner with the account's display name (and uses it as the tab title) so you
can tell which account you're signing into if you have more than one open or configured.

## Local storage

All state lives under `~/.studentloanmcp/`:

```
~/.studentloanmcp/
├── accounts.json          # registered accounts
├── tokens/{accountId}.json  # encrypted session cookies per account
├── keys/                    # ASP.NET Core Data Protection key ring
└── profiles/{accountId}/    # persistent Playwright browser profile per account
```

Token files hold your studentaid.gov session cookies, so they're encrypted at rest with
`Microsoft.AspNetCore.DataProtection` (the key ring lives separately under `keys/`).

Nothing is sent anywhere except studentaid.gov's own endpoints.
