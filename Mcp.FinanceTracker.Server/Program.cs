using Mcp.FinanceTracker.Data;

var builder = WebApplication.CreateBuilder(args);

var databasePath = Path.Combine(builder.Environment.ContentRootPath, "finance.db");
builder.Services.AddFinanceData(databasePath);

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly()
    .WithPromptsFromAssembly();

var app = builder.Build();

await app.Services.EnsureFinanceDataSeededAsync();

app.MapMcp();

app.Run();
