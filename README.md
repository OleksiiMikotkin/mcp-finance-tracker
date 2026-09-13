# Mcp.FinanceTracker

A remote MCP (Model Context Protocol) server exposing a personal finance
tracker: 18 tools, 3 resources, and 2 prompts, served over Streamable HTTP
and protected by OAuth 2.1.

## Tech stack

- **.NET 10** / ASP.NET Core (`Microsoft.NET.Sdk.Web`)
- **`ModelContextProtocol` / `ModelContextProtocol.AspNetCore`** — the
  official MCP C# SDK. Tools/resources/prompts are plain classes decorated
  with `[McpServerToolType]` / `[McpServerResourceType]` /
  `[McpServerPromptType]` attributes, hosted over Streamable HTTP via
  `AddMcpServer().WithHttpTransport()` + `MapMcp()`.
- **`Microsoft.Data.Sqlite`** — raw ADO.NET (`SqliteConnection` /
  `SqliteCommand`), no ORM. A deliberate choice for a project this size:
  full control over SQL, no query-provider indirection for the
  aggregate/group-by-heavy analytics queries.
- **OAuth 2.1** via **WorkOS AuthKit** as an external Authorization
  Server — the MCP server itself is a pure OAuth 2.1 Resource Server
  (`Microsoft.AspNetCore.Authentication.JwtBearer` validates tokens
  against AuthKit's JWKS; the server never sees passwords and never
  issues tokens itself).
- **.NET User Secrets** for local credential storage (never committed).

## Solution layout

```
Mcp.FinanceTracker.Data/       Data access — repositories + analytics queries over SQLite
Mcp.FinanceTracker.DataGen/    Generates the seed transaction dataset
Mcp.FinanceTracker.Server/     The MCP server itself (ASP.NET Core)
  Protocol/Tools/              18 tools — FinanceQueryTools (read/analytics), TransactionCommandTools (write)
  Protocol/Resources/          finance://accounts, finance://account/{id}, finance://transactions/{period}
  Protocol/Prompts/            monthly_review, investigate_suspicious_charge
  Program.cs                   DI wiring, OAuth (WorkOS AuthKit), MCP hosting
```

## Architecture notes

- **Transport**: Streamable HTTP, not stdio — required because OAuth in
  MCP only applies to remote (HTTP) servers.
- **Auth model**: the MCP server is the Resource Server; WorkOS AuthKit is
  the Authorization Server. Standard OAuth 2.1 + PKCE + Dynamic Client
  Registration flow: `401` + `WWW-Authenticate` → Protected Resource
  Metadata (`/.well-known/oauth-protected-resource`) → AuthKit discovery
  → browser login/consent → token exchange → `Authorization: Bearer`
  on every MCP request, validated locally against AuthKit's JWKS
  (issuer + audience, no round-trip to the IdP per request).
- **Authorization**: every tool/resource/prompt class carries
  `[Authorize(Policy = "Mcp.Read")]` or `[Authorize(Policy = "Mcp.Write")]`,
  enforced via `AddAuthorizationFilters()`. Both policies currently just
  require an authenticated caller — see `OAUTH.md` for why the originally
  planned fine-grained read/write scope split isn't enforceable with
  WorkOS's token model.

## Running locally

### Prerequisites

- .NET 10 SDK
- Node.js (for `npx @modelcontextprotocol/inspector` and `npx mcp-remote`)
- A WorkOS account with AuthKit configured (domain, DCR enabled, a
  resource indicator for this server's URL) — see the "WorkOS Dashboard
  setup" section in `OAUTH.md` for exact fields if setting this up fresh.

### Secrets (User Secrets, never in git)

`UserSecretsId` is already set in `Mcp.FinanceTracker.Server.csproj`, but
the actual secrets file is per-machine
(`%APPDATA%\Microsoft\UserSecrets\<guid>\secrets.json` on Windows) and
does not travel with the repo. From the `Mcp.FinanceTracker.Server`
folder:

```
dotnet user-secrets set "WorkOS:ApiKey" "<value from WorkOS Dashboard -> Overview/Quickstart>"
dotnet user-secrets set "WorkOS:ClientId" "<value from WorkOS Dashboard -> Overview/Quickstart>"
```

Check what's set (this prints the actual values — avoid running it on a
shared screen):

```
dotnet user-secrets list
```

### Run

```
dotnet run --project Mcp.FinanceTracker.Server
```

Listens on `http://localhost:5094`. The SQLite database is created and
seeded automatically on first run (`finance.db` is gitignored, not part
of the repo).

### Verify with MCP Inspector

```
npx @modelcontextprotocol/inspector
```

Add a server: Streamable HTTP, `http://localhost:5094/`. Connecting
should trigger a WorkOS AuthKit login/consent flow in the browser.

Inspector persists OAuth discovery state on disk under
`~/.mcp-inspector/storage/oauth.json`, keyed by server URL. If it starts
failing with `invalid_scope` after a server-side config change, delete
that file (details in `OAUTH.md`).

### Connect from Claude Desktop

```json
"finance-tracker": {
  "command": "npx",
  "args": ["mcp-remote", "http://localhost:5094/"]
}
```

`mcp-remote` caches tokens under `~/.mcp-auth/mcp-remote-v1/` — clear
that directory if you change the server's auth configuration.

Note: the current Claude Desktop build has no UI to invoke MCP *prompts*
manually (only tools, which the model calls automatically). Use MCP
Inspector to exercise prompts.

## More context

- `PLAN.md` — milestones, domain decisions, what's done and what's next.
- `OAUTH.md` — full OAuth 2.1 architecture, the WorkOS AuthKit
  implementation deep dive, and the Entra ID plan for the next milestone.
