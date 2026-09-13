# M4/M5 — OAuth: техническое описание

> Дополняет `PLAN.md`. **M4 = WorkOS AuthKit — реализовано и проверено**
> (Inspector + Claude Desktop, реальный OAuth 2.1 login через браузер).
> **M5 = Entra ID / Microsoft.Identity.Web** — следующий шаг, детально
> исследовано ниже, реализация не начата.

## Локальная настройка секретов (User Secrets, не в git)

WorkOS-аккаунт заведён (2026-08-30). Из дашборда нужны:
`WORKOS_API_KEY`, `WORKOS_CLIENT_ID` — оба секретные, никогда не в
`appsettings.json`/git. AuthKit domain (`https://dedicated-sunrise-80-
staging.authkit.app/`) не секретный — уже лежит в `appsettings.json` как
`WorkOS:AuthKitDomain`.

Хранятся через .NET User Secrets (вне репозитория,
`%APPDATA%\Microsoft\UserSecrets\<guid>\secrets.json` на Windows).
`UserSecretsId` для `Mcp.FinanceTracker.Server` уже проинициализирован
(`dotnet user-secrets init`).

Команды на будущее — при первой настройке или при ротации ключа
(выполнять в папке `Mcp.FinanceTracker.Server`):

```
dotnet user-secrets set "WorkOS:ApiKey" "<значение из WorkOS Dashboard → Quickstart>"
dotnet user-secrets set "WorkOS:ClientId" "<значение из WorkOS Dashboard → Quickstart>"
```

Проверить, что уже установлено (выводит и сами значения — не запускать при
демонстрации на экране):

```
dotnet user-secrets list
```

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

## Идея для демонстрации auth (из исходного PLAN.md) — не сработала на WorkOS

Изначальный план: scope-gating tools по чувствительности —
**`Mcp.Write`** (4 write-tools) vs **`Mcp.Read`** (14 read-tools + Resources
+ Prompts), с видимостью/отказом на основе scope/role из токена.

**Итог по факту (M4, WorkOS):** не реализуемо в исходном виде. WorkOS кладёт
RBAC permissions (`mcp.read`/`mcp.write`, назначенные через Role +
Organization membership) только в свой **внутренний session JWT**
(используется для их же hosted UI/виджетов), а не в OAuth access token,
который выдаётся стороннему клиенту через DCR + `/oauth2/token` — то есть
именно в тот токен, который получает наш Resource Server. Подтверждено
эмпирически: задеплоили временное логирование claims (`OnTokenValidated`),
получили реальный токен через полноценный OAuth-логин — в claims есть
`iss`/`aud`/`sub`/`org_id`/`scope` (`openid profile email offline_access`),
но **нет** ни `permissions`, ни `role`. Подтверждено и документацией WorkOS
(permissions описаны только в контексте "session JWT claims", не access
token для внешних клиентов).

**Текущее решение:** обе policy (`Mcp.Read`/`Mcp.Write`) требуют просто
`RequireAuthenticatedUser()` — любой валидный токен даёт полный доступ ко
всем 18 tools. Атрибуты `[Authorize(Policy = "Mcp.Read"/"Mcp.Write")]` на
классах остались (выражают намерение, на случай если появится способ
восстановить разделение), но фактически обе policy сейчас эквивалентны.

**Зачем тогда вообще нужна была Organization/Role в WorkOS**, если
permissions всё равно не доходят: чтобы в токене появился хотя бы `org_id` —
без Organization membership не появляется вообще ничего, кроме голого
`sub`. Это чисто структурная предпосылка, не решение проблемы.

**Проверить на Entra ID (M5):** у Azure AD/Entra ID App Roles архитектурно
устроены иначе — они по умолчанию **попадают прямо в `roles` claim access
token'а**, выданного любому клиенту, включая сторонние (это одна из
базовых фич App Registration + App Roles, в отличие от WorkOS). Если это
подтвердится на практике, fine-grained scope-gating может заработать именно
на Entra ID — стоит проверить в первую очередь при реализации M5, это
может стать интересным пунктом сравнения "WorkOS vs Entra ID" для резюме.

---

## M4 — WorkOS AuthKit (реализовано и проверено)

В итоге не понадобился `WorkOS.net` SDK вообще — достаточно обычного
`Microsoft.AspNetCore.Authentication.JwtBearer` (`.AddJwtBearer(...)`
с `Authority = <AuthKit domain>`), т.к. WorkOS AuthKit ведёт себя как
стандартный OAuth 2.1 / OIDC provider с JWKS-эндпоинтом — специфичный
для WorkOS SDK нужен только если вызывать их Admin API (что мы и делали
отдельно, см. ниже).

### Настройка WorkOS Dashboard (реальные названия полей, проверено вживую)

- **Overview / Quickstart** → `WORKOS_API_KEY`, `WORKOS_CLIENT_ID`
  (секретные, в User Secrets).
- **Domains** → AuthKit domain (не секретный,
  `https://dedicated-sunrise-80-staging.authkit.app/` в `appsettings.json`
  как `WorkOS:AuthKitDomain`).
- **Connect → Configuration**:
  - Включить DCR (Dynamic Client Registration) — обязателен для
    MCP-клиентов, которые не были заранее зарегистрированы (Inspector,
    `mcp-remote`).
  - "Client ID Metadata Document" (CIMD) — **не включать** для локальной
    разработки: требует, чтобы клиент сам хостил своё metadata по
    публичному HTTPS URL, недостижимо для localhost-тестирования через
    Inspector/mcp-remote. DCR полностью достаточен.
  - Поле **"MCP resource indicators"** — сюда прописывается URL нашего
    ресурса (`http://localhost:5094/`), по RFC 8707. Поле "External
    Sign-in URI" — не относится к нашему кейсу (для кастомных
    login-систем, не для AuthKit-hosted login).
- **Authorization → Roles & Permissions**: создали permissions
  `mcp.read`/`mcp.write` (WorkOS **лочит их в нижний регистр** независимо
  от того, как вводишь — вводили `Mcp.Read`/`Mcp.Write`, в базе оказалось
  `mcp.read`/`mcp.write`) и отдельную роль `financetrackeruser` (не
  переиспользовали дефолтный `Admin`, чтобы не путать системное
  администрирование с доменными правами). **По факту эти permissions
  сейчас ни на что не влияют** — см. раздел выше про ограничение WorkOS.
- **Organizations**: WorkOS сам сеет одну дефолтную "Test Organization" в
  новом окружении — ничего создавать не пришлось, только добавить
  пользователя в неё (`organization_membership`) с ролью
  `financetrackeruser`, через Admin API
  (`POST /user_management/organization_memberships` с
  `{user_id, organization_id, role_slug}`) — то же самое можно сделать и
  через Dashboard → Organizations → Members → Add member. Без этого шага
  в токене вообще нет `org_id`.
- **Users**: тестовый пользователь AuthKit создаётся не вручную через
  Dashboard, а через self-service sign-up прямо на hosted login-странице
  (`<authkit-domain>/…`, кнопка "Sign up") — это нормальное поведение
  AuthKit, не баг. Это отдельный аккаунт, никак не связанный с логином
  разработчика на `workos.com`.

### Код (`Program.cs`) — ключевые моменты, не очевидные из документации

- `ResourceMetadata.AuthorizationServers` — это `ICollection<string>`, не
  `Uri` (частая ошибка компиляции при первой попытке).
- `ResourceMetadata.ScopesSupported` **обязан** содержать только те scope,
  которые реальный `/oauth2/authorize` AuthKit готов принять —
  `["openid", "profile", "email", "offline_access"]`. Попытка положить
  туда кастомные `Mcp.Read`/`Mcp.Write` (по аналогии с "как это могло бы
  работать") ломает OAuth-логин с ошибкой `invalid_scope` — WorkOS в
  принципе не поддерживает произвольные кастомные OAuth scope на своём
  `/authorize`, только фиксированный набор из собственного
  `/.well-known/oauth-authorization-server`.
- **Критично: `app.MapMcp()` сам по себе не защищает HTTP-эндпоинт.**
  `AddAuthorizationFilters()` + `[Authorize]` на классах tools/resources/
  prompts работают только *внутри* JSON-RPC dispatch — при отсутствии
  токена это "мягкий" отказ (`200 OK` с пустым списком/JSON-RPC ошибкой),
  а не настоящий `401`. MCP-клиенты (Inspector, `mcp-remote`) стартуют
  OAuth-flow только увидев HTTP `401` + `WWW-Authenticate`. Фикс: явно
  дописать `.RequireAuthorization()` к `app.MapMcp()` — тогда обычный
  ASP.NET Core authorization middleware защищает весь эндпоинт целиком, и
  `McpAuthenticationHandler` (через `DefaultChallengeScheme`) отдаёт
  настоящий `401` с корректным `WWW-Authenticate: Bearer
  resource_metadata="…"`.
- `TokenValidationParameters.ValidateAudience` — включено (`true`) с
  `ValidAudience = "http://localhost:5094/"` (обязательно со слэшем на
  конце — именно так выглядит `aud` в реальном токене, отличается от
  `resource` в PRM-документе, где слэша нет). Без этого сервер принял бы
  токен, выпущенный для *любого* другого ресурса в том же WorkOS-проекте
  (classic confused-deputy / token-passthrough риск).

### Диагностика — на будущее, если снова "клиент не триггерит login"

1. `curl -i` без токена на `initialize`/`tools/list` — если `200 OK` без
   `WWW-Authenticate`, значит нет `.RequireAuthorization()` на `MapMcp()`.
2. Реальные claims токена смотрели через временный
   `JwtBearerOptions.Events.OnTokenValidated` с `Console.WriteLine` по
   каждому claim — быстрее и надёжнее, чем гадать по документации/блогам.
3. **И Inspector, и `mcp-remote` кэшируют OAuth-состояние на диске**, не
   только в браузере:
   - Inspector: `~/.mcp-inspector/storage/oauth.json` — там сохраняется
     **весь discovery-документ** (`resourceMetadata.scopes_supported` и
     т.д.) на момент первого подключения, привязанный к URL сервера.
     Правка PRM на сервере не подхватывается заново, даже если удалить и
     заново добавить сервер через UI — ключ кэша это сам URL, а не запись
     в UI. Лечится только ручным удалением файла.
   - `mcp-remote` (мост для Claude Desktop): `~/.mcp-auth/mcp-remote-v1/`
     — client_info/tokens/code_verifier по каждому клиенту. Чистится
     удалением файлов вручную при смене конфигурации на сервере.
4. **WorkOS RBAC Admin API** — удобно проверять напрямую, не гадая по
   Dashboard: `GET /user_management/organization_memberships?user_id=…`,
   `GET /organizations`, `GET /organizations/{id}/roles` (список ролей
   и их permissions лежит именно тут, отдельного `/roles` эндпоинта нет).

### Известное ограничение клиента (не связано с сервером)

Текущая версия Claude Desktop не имеет UI для ручного вызова MCP
**Prompts** (слэш-команды — это только встроенные команды самого Claude,
попытка набрать `/monthly_review` даёт "Unknown command"). Tools вызываются
моделью автоматически и работают отлично; Prompts проверялись и
подтверждены рабочими только через MCP Inspector (у него есть отдельная
вкладка Prompts с формой аргументов).

### Открытый security-TODO

При диагностике одна из команд (`dotnet user-secrets list` без `--json`,
как фолбэк после неудачной попытки распарсить JSON) случайно вывела
`WorkOS:ApiKey` в лог инструмента — ключ стоит считать
скомпрометированным и перевыпустить через Dashboard → API Keys →
Regenerate, затем обновить `dotnet user-secrets set "WorkOS:ApiKey" "…"`.
**Не выполнено на момент записи.**

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

1. ~~Подтвердить на практике, что `.AddMcp(...)` и
   `McpAuthenticationDefaults` действительно присутствуют~~ — **закрыто**,
   подтверждено в M4: тот же пакет/API реально использовался и работает
   (`AddMcp`, `McpAuthenticationDefaults`, `AddAuthorizationFilters`,
   `MapMcp().RequireAuthorization()`).
2. Проверить реальную поддержку DCR/CIMD в Entra ID на момент реализации,
   либо подтвердить путь с заранее зарегистрированным App Registration.
3. **Новое, из опыта M4:** проверить, действительно ли Entra ID App Roles
   попадают в `roles` claim access token'а, выданного стороннему
   (DCR/App Registration) клиенту — если да, fine-grained scope-gating
   (несбывшийся план M4) может получиться именно здесь.

## Источники

- [The biggest MCP spec update ships July 28 — WorkOS](https://workos.com/blog/mcp-2026-spec-agent-authentication)
- [Understanding Authorization in MCP - Model Context Protocol](https://modelcontextprotocol.io/docs/2026-07-28/tutorials/security/authorization)
- [.NET MCP Server OAuth with Microsoft Entra ID - Mitchell Barry](https://blog.mitchbarry.com/net-mcp-server-oauth-with-microsoft-entra-id/)
- [Add Authentication to MCP Servers using Microsoft Entra ID | N+1 Blog](https://nikiforovall.blog/dotnet/2025/09/02/mcp-auth.html)
- [Securing Remote MCP Servers in C#: OAuth 2.0, JWT, and Protected Resource Metadata — devleader.ca](https://www.devleader.ca/2026/07/23/securing-remote-mcp-servers-in-c-oauth-20-jwt-and-protected-resource-metadata)
- [Releases · modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk/releases)
- [.NET SDK – WorkOS Docs](https://workos.com/docs/sdks/dotnet)
- [NuGet Gallery | WorkOS.net](https://www.nuget.org/packages/WorkOS.net)
- [Model Context Protocol – AuthKit – WorkOS Docs](https://workos.com/docs/authkit/mcp)
- [Dynamic Client Registration (DCR) in MCP — WorkOS](https://workos.com/blog/dynamic-client-registration-dcr-mcp-oauth)
- [Roles – User Management – WorkOS Docs](https://workos.com/docs/user-management/roles)
- [Configuration – RBAC – WorkOS Docs](https://workos.com/docs/rbac/configuration)
- [Sessions – User Management – WorkOS Docs](https://workos.com/docs/user-management/sessions/integrating-sessions/access-token)
