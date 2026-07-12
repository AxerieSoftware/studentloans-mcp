using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Axerie.StudentLoans.Mcp;
using Axerie.StudentLoans.Mcp.Storage;

AppPaths.EnsureDirs();

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<StudentLoanTools>();

// stdout is reserved for the MCP protocol; logs must go to stderr.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddDataProtection()
    .SetApplicationName("studentloans-mcp")
    .PersistKeysToFileSystem(new DirectoryInfo(AppPaths.KeysDir));

builder.Services.AddSingleton<AccountStore>();
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSingleton<AuthService>();

builder.Services.AddTransient<LoanApiAuthHandler>();
builder.Services.AddHttpClient<LoanApiService>()
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { UseCookies = false })
    .AddHttpMessageHandler<LoanApiAuthHandler>()
    .AddStandardResilienceHandler();

await builder.Build().RunAsync();
