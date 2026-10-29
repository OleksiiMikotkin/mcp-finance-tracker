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
3. **`DEVELOPMENT.md`** — tech stack, solution layout, how to run and verify
   locally.

`README.md` is the public project overview (English). This is a pet
project for learning MCP; don't frame any docs around job search,
resumes or interviews.
Keep its feature descriptions accurate when the code changes. It
deliberately has no roadmap and no item counts (they go stale); keep dev
instructions in `DEVELOPMENT.md` and never put the tunnel URL or other
infrastructure details there.

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
- M6 (client integrations) — partly done: Inspector, Claude Desktop and
  **Claude.ai** (via an ngrok tunnel,
  `https://framing-germicide-tactless.ngrok-free.dev`) verified
  2026-10-25. VS Code Copilot and own SK client remain.
- M7 (v2 agent loop) — not started.

## Known limitations / gotchas (see `OAUTH.md` for full detail)

- Fine-grained `Mcp.Read`/`Mcp.Write` scope-gating was planned but isn't
  enforced: WorkOS RBAC permissions land only in WorkOS's own internal
  session JWT, not in the OAuth access token issued to third-party (DCR)
  clients like this MCP server's callers. All policies in `Program.cs`
  (default, `Mcp.Read`, `Mcp.Write`) are one `organizationMember` policy:
  authenticated **and** `org_id` == `WorkOS:OrganizationId`. The `org_id`
  check is what keeps self-signed-up AuthKit users out now that the
  server is publicly reachable — don't drop it. Worth re-checking
  read/write split on Entra ID (M5) — Azure AD App Roles are claimed to
  land directly in the `roles` claim of tokens issued to any client.
- Two entry points, one server process: `http://localhost:5094` and the
  ngrok tunnel. Every resource indicator registered in WorkOS must also be
  listed in `WorkOS:Audiences` (`appsettings.json`, with trailing slash).
  `UseForwardedHeaders` is required behind the tunnel — the SDK builds the
  PRM `resource` URL from the request, and ngrok forwards plain `http`.
  Claude.ai connector uses CIMD ("Claude's published identity"); Claude
  clients (Desktop and Claude.ai) never call `resources/templates/list`.
  Inspect tunnel traffic at `http://127.0.0.1:4040`.
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
- Claude Desktop (verified 2026-10-25, via `claude_desktop_config.json` +
  `mcp-remote` — still works despite Anthropic docs saying remote servers
  belong in Connectors): in **Chat** mode, prompts and the static
  `finance://accounts` resource are available under "+" → Connectors →
  "Add from finance-tracker". **Resource templates are not supported** —
  Desktop never calls `resources/templates/list`. **Cowork/Code** mode
  opens a separate connection that loads tools only. On an MSIX install
  the config lives in `%LOCALAPPDATA%\Claude-Data\claude_<id>\`, not
  `%APPDATA%\Claude\`; edit it only with Desktop fully closed. Per-server
  log: `%LOCALAPPDATA%\Claude\logs\mcp-server-finance-tracker.log`. Full
  detail in `OAUTH.md`.

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
