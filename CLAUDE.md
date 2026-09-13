# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Read first

Before proposing or making changes, read:

1. **`PLAN.md`** — milestones, domain decisions, what's already been
   decided and why. Written as a starting-context file for a new chat —
   treat decisions in it as settled, not open for re-litigation without a
   reason.
2. **`OAUTH.md`** — full OAuth 2.1 architecture and the WorkOS AuthKit
   implementation deep dive (M4, done), plus the Entra ID plan (M5, not
   started). Contains hard-won diagnostic findings — read it before
   touching `Program.cs`'s auth wiring or debugging an OAuth issue from
   scratch.
3. **`README.md`** — tech stack, solution layout, how to run and verify
   locally.

## What this project is

A remote MCP (Model Context Protocol) server — a personal finance
tracker exposed as 18 tools, 3 resources, and 2 prompts — served over
Streamable HTTP and protected by OAuth 2.1 via WorkOS AuthKit. .NET 10 /
ASP.NET Core, `ModelContextProtocol`/`ModelContextProtocol.AspNetCore`
SDK, raw `Microsoft.Data.Sqlite` (no ORM).

## Current status

- M0–M4 done: skeleton, SQLite + 18 tools, resources, prompts, WorkOS
  AuthKit OAuth. Verified end-to-end through both MCP Inspector and
  Claude Desktop.
- M5 (Entra ID / Microsoft.Identity.Web) — planned, not started.
- M6 (client integrations), M7 (v2 agent loop) — not started.

## Known limitations / gotchas (see `OAUTH.md` for full detail)

- Fine-grained `Mcp.Read`/`Mcp.Write` scope-gating was planned but isn't
  enforced: WorkOS RBAC permissions land only in WorkOS's own internal
  session JWT, not in the OAuth access token issued to third-party (DCR)
  clients like this MCP server's callers. Both authorization policies in
  `Program.cs` currently just require an authenticated caller. Worth
  re-checking on Entra ID (M5) — Azure AD App Roles are claimed to land
  directly in the `roles` claim of tokens issued to any client.
- `app.MapMcp()` alone does **not** protect the HTTP endpoint — it must
  be chained with `.RequireAuthorization()`, or unauthenticated requests
  get a soft `200 OK` (empty results / JSON-RPC errors) instead of a real
  `401` that MCP clients need to see to trigger their OAuth flow.
- WorkOS AuthKit's `/oauth2/authorize` only accepts its own fixed OIDC
  scopes (`openid`, `profile`, `email`, `offline_access`) — putting
  custom scope names in `ResourceMetadata.ScopesSupported` breaks the
  login flow with `invalid_scope`.
- MCP Inspector and `mcp-remote` (the stdio bridge Claude Desktop uses)
  both cache OAuth discovery state / tokens on disk
  (`~/.mcp-inspector/storage/oauth.json`,
  `~/.mcp-auth/mcp-remote-v1/`), keyed by server URL, independent of
  browser state. A stale cache after a server-side auth config change
  needs manual deletion — reconnecting through the UI alone doesn't
  refresh it.
- The current Claude Desktop build has no UI to invoke MCP *prompts*
  manually (tools work fine, invoked automatically by the model). Verify
  prompts through MCP Inspector instead.

## Secrets

WorkOS credentials (`WorkOS:ApiKey`, `WorkOS:ClientId`) live in .NET User
Secrets, never in `appsettings.json` or git. Never print secret values
into command output or chat — when inspecting stored secrets, use
`dotnet user-secrets list --json` piped through a parser that extracts
just the needed key, not a raw `list` fallback that echoes everything.

## Working style notes

- Prefer verifying claims about third-party client UI (Claude Desktop,
  MCP Inspector) against what the user actually sees on screen rather
  than asserting from memory or search results — client UIs here have
  changed between sessions.
- When debugging OAuth/token issues, prefer direct verification (decode
  a real token, curl the actual endpoints, query the WorkOS Admin API)
  over inferring behavior from documentation alone — several real
  findings in `OAUTH.md` contradicted what the docs implied.
