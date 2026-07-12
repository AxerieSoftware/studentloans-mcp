using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Axerie.StudentLoans.Mcp.Api;
using Axerie.StudentLoans.Mcp.Auth;
using Axerie.StudentLoans.Mcp.Storage;
using Axerie.StudentLoans.Mcp.Tools;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<StudentLoanTools>();

// stdout is reserved for the MCP protocol; logs must go to stderr.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<HttpClient>();
builder.Services.AddSingleton<AccountStore>();
builder.Services.AddSingleton<TokenStore>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<LoanApiService>();

await builder.Build().RunAsync();
