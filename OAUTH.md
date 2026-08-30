# M4/M5 — OAuth: техническое описание

> Дополняет `PLAN.md`. Собрано в рамках обсуждения архитектуры auth, до
> начала реализации — фиксирует найденные факты и открытые вопросы, чтобы не
> пересматривать их заново в следующей сессии.
>
> Провайдер поменялся местами после обсуждения: **M4 = WorkOS AuthKit**
> (первый, проще стартовать, есть официальный MCP-специфичный quickstart),
> **M5 = Entra ID / Microsoft.Identity.Web** (второй, тяжелее в настройке,
> но ценен для .NET-резюме). Изначально в этом файле был описан только
> Entra ID под старой нумерацией (было "M4-OAUTH.md") — детальный раздел
> ниже про Entra ID теперь относится к M5, WorkOS ещё предстоит
> исследовать так же подробно, когда дойдём до M4.

## Общая архитектура — одинакова для обоих провайдеров (OAuth 2.1)

- **Authorization Server (AS) = внешний IdP** (WorkOS или Entra ID —
  неважно, какой). Единственный, кто видит пароли, показывает форму
  логина/согласия, выпускает и отзывает токены.
- **Resource Server (RS) = наш MCP-сервер.** Только проверяет входящий
  токен (подпись/issuer/audience) и обслуживает запрос. Не хранит пароли,
  не занимается логином, не выпускает токены.

Это принципиально отличается от "сервер сам реализует OAuth" — мы просто
доверяем внешнему IdP и умеем валидировать его подписи. Один и тот же
принцип работает для обоих провайдеров — конкретные endpoints/claims
отличаются, сама роль сервера — нет.

## Алгоритм сопряжения клиента и сервера (пошагово, провайдер-агностично)

1. Клиент (Claude Desktop/Claude.ai/Inspector) стучится в MCP-сервер без
   токена.
2. Сервер отвечает `401 Unauthorized` + заголовок `WWW-Authenticate`,
   указывающий на **Protected Resource Metadata**.
3. Сервер обязан публиковать `/.well-known/oauth-protected-resource`
   (обязательное требование ревизии спеки 2026-07-28, RFC 9728) — JSON:
   "я ресурс, мне доверяет вот этот Authorization Server, вот мои scopes".
4. Клиент читает этот документ → узнаёт адрес конкретного IdP (WorkOS-
   проекта или Entra ID tenant'а).
5. Клиент запрашивает у IdP его собственный discovery-документ
   (`/.well-known/openid-configuration`) → узнаёт endpoints авторизации и
   обмена токена.
6. Клиент открывает пользователю браузер с логин-формой IdP (Authorization
   Code + PKCE, обязателен по OAuth 2.1). Логин/согласие происходят целиком
   на стороне IdP — наш сервер это не видит.
7. IdP выпускает access token (JWT), привязанный к нашему ресурсу (Resource
   Indicators, RFC 8707 — токен нельзя переиспользовать против другого API).
8. Клиент прикладывает `Authorization: Bearer <token>` к каждому MCP-запросу.
9. Сервер валидирует JWT **локально** — подпись через JWKS IdP, issuer,
   audience — без похода к IdP на каждый запрос.

## Что это значит в коде (провайдер-агностично)

- `ModelContextProtocol.AspNetCore.Authentication` (namespace внутри пакета
  `ModelContextProtocol.AspNetCore`, отдельного NuGet-пакета с таким именем
  нет) → `.AddMcp(...)`, `McpAuthenticationDefaults`,
  `McpAuthenticationOptions` — добавляет MCP-специфичную обвязку: публикацию
  Protected Resource Metadata, корректный формат `401`/`WWW-Authenticate`.
  Одинаково для обоих провайдеров.
- Обе схемы регистрируются вместе в `AddAuthentication(...)`: MCP-схема как
  challenge scheme, JWT Bearer — как схема валидации токена.
- Tool-методы могут принимать `ClaimsPrincipal` как специальный параметр
  (SDK биндит автоматически) — читаем claims/scope из провалидированного
  токена внутри метода.
- Меняется только конфигурация валидации (Authority/audience/claim-мэппинг)
  — в идеале через `Authentication:Provider = WorkOS | EntraId` в конфиге,
  чтобы показать, что auth-слой не захардкожен под одного вендора.

## Идея для демонстрации auth (из исходного PLAN.md, остаётся в силе)

Scope-gating tools по чувствительности, одинаково для обоих провайдеров:
- **`Mcp.Write`** — 4 write-tools: `add_transaction`, `categorize_transaction`,
  `reverse_transaction`, `set_budget`.
- **`Mcp.Read`** — 14 read/analytics tools + Resources + Prompts.

Открытый вопрос (общий для обоих): как именно scope/role из токена
превращаются в **видимость** tools в `tools/list` (fully hidden) vs. просто
**отказ при вызове** (tool виден всем, но `tools/call` для write без
нужного scope возвращает ошибку) — это разное поведение и разный UX, нужно
решить осознанно при реализации M4.

---

## M4 — WorkOS AuthKit (следующий шаг, детали ещё не исследованы)

Официальный .NET SDK — `WorkOS.net` (NuGet). У WorkOS есть готовая
MCP-специфичная документация (AuthKit для MCP, `auth.md`) и пример
MCP-server template — путь под наш кейс уже описан вендором.

**Ещё предстоит перед реализацией:**
- Завести WorkOS-аккаунт/проект с нуля (подтверждено: у пользователя пока
  нет ни WorkOS, ни Entra ID — оба начинаются с нуля).
- Изучить конкретно, как `WorkOS.net` стыкуется с `AddAuthentication()
  .AddJwtBearer(...)` — обычный JWT bearer против JWKS WorkOS, или
  какой-то другой паттерн через `WorkOSClient`/`PublicWorkOSClient`.
- Уточнить формат claims (scope/role) в токене WorkOS — по аналогии с
  вопросом №3 у Entra ID ниже.
- Найти официальный MCP + AuthKit quickstart и свериться с ним перед
  написанием кода (не полагаться на предположения).

## M5 — Entra ID / Microsoft.Identity.Web (детально исследовано)

### Версии пакетов (проверено 2026-08-30)

| Пакет | Установлено | Последняя на NuGet | Комментарий |
|---|---|---|---|
| `ModelContextProtocol` | 2.2.0 | 2.2.0 | Уже последняя — апгрейд не нужен |
| `ModelContextProtocol.AspNetCore` | 2.2.0 | 2.2.0 | Уже последняя |
| `Microsoft.Identity.Web` | не подключён | 4.14.2 | Понадобится добавить для M5 |

**Важный факт:** начиная с `csharp-sdk` v2.0.0, SDK уже приведён в
соответствие со спецификацией **MCP 2026-07-28** (крупнейшая ревизия
протокола на данный момент — переписала именно авторизацию). Обратная
совместимость сохранена вплоть до `2024-11-05` — сервер/клиент на нашем SDK
сам согласовывает более старую версию протокола, если её просит собеседник
(это объясняет, почему в наших ручных curl-тестах M0-M3 сервер послушно
отвечал `"protocolVersion": "2025-06-18"` — мы сами это указывали в
`initialize`, и SDK просто подтвердил запрошенную версию).

Вывод: **апгрейдить пакеты перед M5 не требуется** — они уже целятся в
актуальную спеку с переписанной авторизацией. Нужно только добавить
`Microsoft.Identity.Web`.

### Регистрация клиента — нюанс для Entra ID

Ревизия 2026-07-28 официально депрекейтит Dynamic Client Registration (DCR)
в пользу Client ID Metadata Documents (CIMD) как предпочтительного способа.
Entra ID поддерживает DCR/CIMD ограниченно — реалистичный путь для нас:
**заранее зарегистрированное приложение** (App Registration) в портале
Entra ID, а не динамическая регистрация на лету. Это нужно перепроверить
конкретно на момент реализации — предположение, не подтверждённый факт.

### Что это значит в коде

- `Microsoft.Identity.Web` → `AddMicrosoftIdentityWebApi(...)` — валидация
  JWT против конкретного Entra ID tenant'а.

### Что нужно сделать руками (не код)

**App Registration в портале Entra ID (Azure Portal)** — администрирование,
не код:
- Задать identifier URI (`api://<client-id>`) для нашего MCP-сервера как API.
- Объявить scopes: `Mcp.Read`, `Mcp.Write`.

### Открытые вопросы перед стартом реализации M5

1. Подтвердить на практике (не только по блогам), что `.AddMcp(...)` и
   `McpAuthenticationDefaults` действительно присутствуют в установленной
   `ModelContextProtocol.AspNetCore` 2.2.0 — найдено через блоги/статьи
   (Mitchell Barry, N+1 blog, devleader.ca), не через официальную SDK-доку
   напрямую.
2. Проверить реальную поддержку DCR/CIMD в Entra ID на момент реализации,
   либо подтвердить путь с заранее зарегистрированным App Registration.

## Источники

- [The biggest MCP spec update ships July 28 — WorkOS](https://workos.com/blog/mcp-2026-spec-agent-authentication)
- [Understanding Authorization in MCP - Model Context Protocol](https://modelcontextprotocol.io/docs/2026-07-28/tutorials/security/authorization)
- [.NET MCP Server OAuth with Microsoft Entra ID - Mitchell Barry](https://blog.mitchbarry.com/net-mcp-server-oauth-with-microsoft-entra-id/)
- [Add Authentication to MCP Servers using Microsoft Entra ID | N+1 Blog](https://nikiforovall.blog/dotnet/2025/09/02/mcp-auth.html)
- [Securing Remote MCP Servers in C#: OAuth 2.0, JWT, and Protected Resource Metadata — devleader.ca](https://www.devleader.ca/2026/07/23/securing-remote-mcp-servers-in-c-oauth-20-jwt-and-protected-resource-metadata)
- [Releases · modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk/releases)
- [.NET SDK – WorkOS Docs](https://workos.com/docs/sdks/dotnet)
- [NuGet Gallery | WorkOS.net](https://www.nuget.org/packages/WorkOS.net)
