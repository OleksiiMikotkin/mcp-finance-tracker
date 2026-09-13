using Mcp.FinanceTracker.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using ModelContextProtocol.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

var databasePath = Path.Combine(builder.Environment.ContentRootPath, "finance.db");
builder.Services.AddFinanceData(databasePath);

var authKitDomain = builder.Configuration["WorkOS:AuthKitDomain"]
    ?? throw new InvalidOperationException("WorkOS:AuthKitDomain is not configured.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddMcp(options =>
    {
        options.ResourceMetadata = new()
        {
            AuthorizationServers = { authKitDomain },
            // WorkOS AuthKit's /authorize endpoint only accepts its own OIDC scopes here —
            // custom scopes like "Mcp.Read"/"Mcp.Write" are rejected with invalid_scope.
            ScopesSupported = ["openid", "profile", "email", "offline_access"],
        };
    })
    .AddJwtBearer(options =>
    {
        options.Authority = authKitDomain;
        // Confirmed via a real decoded WorkOS access token: aud = "http://localhost:5094/"
        // (the resource indicator registered in the WorkOS dashboard).
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidAudience = "http://localhost:5094/";
    });

// WorkOS's RBAC permissions only land in its own internal session JWT, not in the OAuth
// access token issued to third-party (MCP) clients via DCR — so per-scope gating based on
// token claims isn't available here. Both policies just require a valid, authenticated caller.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Mcp.Read", policy => policy.RequireAuthenticatedUser())
    .AddPolicy("Mcp.Write", policy => policy.RequireAuthenticatedUser());

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly()
    .WithPromptsFromAssembly()
    .AddAuthorizationFilters();

var app = builder.Build();

await app.Services.EnsureFinanceDataSeededAsync();

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp().RequireAuthorization();

app.Run();
