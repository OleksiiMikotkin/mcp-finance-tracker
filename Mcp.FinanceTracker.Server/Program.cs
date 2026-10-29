using Mcp.FinanceTracker.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using ModelContextProtocol.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

var databasePath = Path.Combine(builder.Environment.ContentRootPath, "finance.db");
builder.Services.AddFinanceData(databasePath);

var authKitDomain = builder.Configuration["WorkOS:AuthKitDomain"]
    ?? throw new InvalidOperationException("WorkOS:AuthKitDomain is not configured.");
var organizationId = builder.Configuration["WorkOS:OrganizationId"]
    ?? throw new InvalidOperationException("WorkOS:OrganizationId is not configured.");
var audiences = builder.Configuration.GetSection("WorkOS:Audiences").Get<string[]>();
if (audiences is not { Length: > 0 })
{
    throw new InvalidOperationException("WorkOS:Audiences is not configured.");
}

// The Protected Resource Metadata URL is built from the incoming request; behind a
// TLS-terminating tunnel (ngrok) the public https scheme only arrives in X-Forwarded-Proto.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

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
        // One entry per resource indicator registered in WorkOS (localhost, tunnel URL).
        // The token's aud carries a trailing slash, unlike the PRM "resource" value.
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidAudiences = audiences;
    });

// WorkOS's RBAC permissions only land in its own internal session JWT, not in the OAuth
// access token issued to third-party (MCP) clients via DCR — so per-scope gating isn't
// available. org_id is the one membership claim that does reach the token: requiring it
// stops anyone who self-signs-up on AuthKit from reaching the data once the server is public.
var organizationMember = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .RequireClaim("org_id", organizationId)
    .Build();

builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(organizationMember)
    .AddPolicy("Mcp.Read", organizationMember)
    .AddPolicy("Mcp.Write", organizationMember);

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly()
    .WithPromptsFromAssembly()
    .AddAuthorizationFilters();

var app = builder.Build();

await app.Services.EnsureFinanceDataSeededAsync();

app.UseForwardedHeaders();
app.UseAuthentication();
app.UseAuthorization();

app.MapMcp().RequireAuthorization();

app.Run();
