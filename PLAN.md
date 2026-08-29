# MCP pet-проект — план изучения и реализации

> Этот файл — стартовый контекст для нового чата в Claude Code. Прочитай его
> целиком перед тем как предлагать шаги; он написан по итогам обсуждения
> с пользователем и фиксирует уже принятые решения, чтобы их не пересматривать
> без необходимости.

## Зачем

Портфолио-проект под поиск .NET-вакансий с уклоном на практическое изучение
MCP (Model Context Protocol). У пользователя уже есть: базовый сертификат
Anthropic по MCP и домашка с двумя простыми MCP-серверами в Claude Desktop
(редактор текста, git-помощник) — оба **stdio**, без auth. Следующий шаг —
закрыть то, чего там не было: remote-транспорт и OAuth.

Проект полностью независим — своя БД, свой код, свой (будущий) репозиторий.
Ничего из существующих проектов пользователя не переиспользуется и не
оборачивается.

## Домен: трекер личных финансов

Выбран вместо "ещё один список контактов", потому что:
- данные чувствительные → OAuth/scopes нужны по существу, не для галочки;
- естественная декомпозиция на tools/resources (транзакции, баланс, бюджет);
- реально полезно developer'у для себя — мотивация развивать дальше не для
  резюме, а по факту.

MVP-набор (можно урезать ещё сильнее для первой итерации):
- **Tools — команды (запись)**: `add_transaction`, `categorize_transaction`,
  `set_budget`. Естественные кандидаты на scope-gating в M2 (write-scope vs
  read-scope) — хороший материал для демонстрации auth.
- **Tools — аналитика (чтение)**, перенесены из Python-домашки по
  multi-agent (см. "Источники и перенос" ниже), сигнатуры и описания уже
  проверены на реальных сценариях:
  `list_transactions` / `get_transactions_in_period` (фильтр по периоду/
  категории/счёту/мерчанту), `get_balance`, `get_budget_status`,
  `get_top_categories`, `get_spending_by_category`, `get_spending_by_merchant`,
  `get_recent_transactions`, `detect_subscriptions`,
  `detect_forgotten_subscriptions`, `analyze_late_night_delivery`,
  `analyze_weekend_spending`, `analyze_coffee_habit`, `simulate_savings`,
  `project_month_end_balance`, `detect_suspicious_transactions`.
  Урезать можно, но не до одного `get_balance` — именно эти tools дают
  содержательное демо ("забытая подписка", "траты по выходным выше на 50%"),
  а не голый CRUD.
- **Resources**: `finance://account/{id}`, `finance://transactions/{period}` —
  данные, которые клиент может подгрузить без явного tool-вызова. Этот
  примитив часто пропускают в туториалах — стоит закрыть осознанно, не
  сводить MCP только к tools.
- **Prompts** — не факультатив, минимум 1 штука с содержательной ролью:
  правила тона/эскалации ("на ты", вежливый отказ на out-of-scope,
  направление к поддержке при подозрении на fraud вместо попытки решить
  самостоятельно) — то, что в Python-версии жило в system prompt отдельного
  Escalation-агента. Без своего оркестратора это переезжает в один MCP
  Prompt, который клиент подключает как guidance поверх tool-вызовов.
- Явно **не делаем в MVP**: sampling, elicitation, roots — слабо
  применимы к домену, риск сделать ради галочки.

### Источники и перенос из Python-домашек

Пользователь уже делал два релевантных pet-проекта на Python — их тематику
и наработки стоит переиспользовать по возможности, чтобы не терять уже
проделанную работу над доменом:

- `lesson-11-ai-agents-tool-orchestration/homework/` — **"Personal Finance
  Coach"**, multi-agent (LangGraph: Router → Analyst/Behavioral/Planning/
  Escalation) поверх датасета `starter/data/transactions.csv` (842 транзакции
  за 12 месяцев с намеренно зашитыми паттернами: забытая подписка Sportlife,
  поздние доставки после 21:00, всплеск трат в выходные, привычка кофе по
  будням, подозрительные foreign-транзакции для теста эскалации).
  **Переносим**: сам датасет как seed для SQLite в M1, сигнатуры и
  description'ы аналитических tools (`src/tools/finance_tool.py`,
  `src/tools/tool_schemas.py` — там уже хорошо сформулированные описания,
  ровно то, что нужно для `[McpServerTool(Description = ...)]`), поведенческие
  правила (тон, эскалация) — в один MCP Prompt.
  **Не переносим**: сам LangGraph-оркестратор (роутинг между агентами) —
  в MCP эту роль берёт клиент; Streamlit UI и LangSmith-метрики
  (`cost_breakdown_by_agent`, `inter_agent_overhead_pct`) — специфичны для
  оценки multi-agent архитектуры, в MCP-модели не применимы, так как нет
  "агентов" для сравнения.
- `lesson 16 - mcp-model-context-protocol/homework_mcp/` — два простых
  stdio MCP-сервера (`notes_server.py`, `git_server.py`) на Python SDK
  (`mcp.server.fastmcp.FastMCP`, `@mcp.tool()` / `@mcp.resource()`). Это уже
  упомянутая в начале файла домашка "без auth, stdio" — источник паттерна
  tools+resources (`notes://all`, `notes://{title}`), но не кода: transport и
  язык другие, переносится только структура мышления "что tool, что resource".

### Важное архитектурное решение: без своего оркестратора внутри

В People API уже реализован multi-agent (LangGraph: Orchestrator → Search/
Birthday → Synthesizer) — это отдельный, уже закрытый скилл. **В MCP-проекте
своего agent loop внутри сервера в MVP не строим.** Смысл MCP в том, что
оркестратором становится сам клиент (Claude Desktop/Claude.ai/Copilot) — он
сам вызывает tools в цикле и решает, когда анализировать траты. Свой
внутренний multi-agent за инструментами — тема для v2, отдельная веха, чтобы
не повторить в OAuth+MCP-проекте тот же scope creep, что обсуждался
применительно к самому OAuth.

## Транспорт и авторизация

Remote HTTP (Streamable HTTP), не stdio — иначе OAuth физически неприменим
(OAuth в MCP работает только для remote-серверов).

**Два варианта OAuth-провайдера, от простого к сложному — идти именно в этом
порядке:**

1. **Старт: Microsoft.Identity.Web / Entra ID.** Не пишем свой authorization
   server — Entra ID выступает IdP, весь фокус на MCP-специфичной обвязке
   (`ModelContextProtocol.AspNetCore.Authentication` → `.AddMcp(...)` поверх
   стандартного `.AddAuthentication().AddJwtBearer(...)`, плюс маппинг
   scope/role → видимость tools). Это резко снижает риск "весь проект уйдёт
   в написание своего OAuth-сервера" — сложность делегирована Microsoft, а
   учимся именно MCP-стороне интеграции. Дополнительный плюс для .NET-резюме:
   Entra ID — типичный enterprise-стек.
2. **Углубление (после того как первый вариант работает end-to-end):**
   полноценный OAuth 2.1 без готового IdP — authorization code + PKCE,
   осознанная работа с resource indicators (RFC 8707), может быть Dynamic
   Client Registration. Это уже про понимание протокола изнутри, не про
   скорость. Делать веткой/следующей итерацией, не блокировать MVP.

## Пакеты (.NET, актуально на момент написания — SDK v2.2.0, релиз v1.0 был
в марте 2026, совместно Anthropic + Microsoft)

- `ModelContextProtocol` — core SDK.
- `ModelContextProtocol.AspNetCore` — HTTP-хостинг MCP поверх ASP.NET Core
  (`MapMcp(...)`, namespace `ModelContextProtocol.AspNetCore.Authentication`
  с `McpAuthenticationDefaults`/`McpAuthenticationOptions`/`.AddMcp(...)`).
- `Microsoft.Extensions.Hosting` — генерик-хост, DI, конфиг, логирование
  (идиоматичный способ бутстрапить, подтверждено сторонним примером —
  см. ниже).
- `Microsoft.Identity.Web` — если идём по варианту 1 для auth.
- `Microsoft.Extensions.AI.Abstractions` — транзитивная зависимость
  ModelContextProtocol.Core; та же абстракция, которую использует Semantic
  Kernel — пригодится, если дальше делать свой клиент на SK.

### Атрибуты для tools/resources/prompts (аналог Python FastMCP)

В отличие от Python SDK, где есть низкоуровневый `mcp.server.Server` и
поверх него высокоуровневый FastMCP (`@mcp.tool()`), в C# SDK
высокоуровневый DX встроен в тот же пакет — отдельного "FastMCP для .NET"
нет и не нужно, атрибуты `ModelContextProtocol.Server` — это и есть прямой
аналог декораторов FastMCP:

- `[McpServerToolType]` на классе + `[McpServerTool]` на методе —
  аналог `@mcp.tool()`. JSON Schema параметров генерируется автоматически
  из сигнатуры метода.
- `[McpServerResourceType]` на классе + `[McpServerResource]` на методе
  (с URI-шаблоном, напр. `finance://account/{id}`) — аналог
  `@mcp.resource("...")`.
- `[McpServerPromptType]` + `[McpServerPrompt]` — аналог `@mcp.prompt()`.
- `[Description("...")]` из `System.ComponentModel` — на методе и на
  каждом параметре, для описаний, которые видит клиент/LLM (в Python это
  было текстом в docstring, как в `tool_schemas.py`).
- Методы tools могут дополнительно принимать `McpServer`,
  `IProgress<ProgressNotificationValue>`, `ClaimsPrincipal` (пригодится в
  M2 для чтения scope/role из токена) и любой сервис из DI — параметры
  распознаются автоматически, без ручной сборки контекста.
- Регистрация в `Program.cs`: `builder.Services.AddMcpServer()
  .WithHttpTransport().WithToolsFromAssembly().WithResourcesFromAssembly()
  .WithPromptsFromAssembly()`, затем `app.MapMcp()`. Можно и явно —
  `.WithTools<FinanceTools>()` — если не хочется сканировать всю сборку.

Источники (проверено веб-поиском 2026-08): [MCP C# SDK — Tools](https://csharp.sdk.modelcontextprotocol.io/concepts/tools/tools.html),
[McpServerToolAttribute](https://csharp.sdk.modelcontextprotocol.io/api/ModelContextProtocol.Server.McpServerToolAttribute.html),
[Getting Started — ASP.NET Core hosting](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/getting-started.html),
[.NET Blog — Build an MCP server in C#](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/).

## Куда потом интегрировать (проверено веб-поиском, актуально на 2026-08)

- **Claude.ai** — Settings → Connectors, OAuth проходит в UI в один клик.
  Самый показательный вариант для демо "MCP-сервер + OAuth".
- **Claude Desktop** — то же самое, Settings → Connectors, Streamable HTTP.
- **VS Code + GitHub Copilot (agent mode)** — remote-сервер в `mcp.json`,
  авторизация через "Auth" в CodeLens. Хорошо смотрится для dev-аудитории.
- **Свой клиент на .NET через Semantic Kernel** (рекомендовано отдельным
  пунктом) — SK имеет нативную поддержку MCP-клиента (v1.71+). Даёт полный
  контроль над демо на собеседовании (не зависишь от того, как сегодня
  Claude Desktop рисует OAuth-попап) и цельную .NET-историю end-to-end без
  единой Python-строчки.

Для локальной разработки remote+OAuth понадобится туннель наружу
(ngrok / dev tunnels) — OAuth redirect и Connectors ожидают публичный HTTPS
URL, localhost не подойдёт для внешних клиентов типа Claude.ai.

## Вехи (черновые, можно и нужно уточнять по ходу)

- **M0. (done)** Скелет: .NET 10, `ModelContextProtocol.AspNetCore`, 1–2 tool
  без БД и без auth. Проверено через MCP Inspector, что вызов tool работает
  end-to-end.
- **M1. (done)** SQLite + доменные tools (18 tools: 4 write + 14 read/
  аналитика). Data access — сырой `Microsoft.Data.Sqlite` (без ORM,
  `SqliteConnection`/`SqliteCommand` напрямую), осознанный выбор: максимум
  контроля над SQL и минимум зависимостей для pet-проекта такого размера;
  аналитические tools (group-by/aggregate) пишутся как обычный SQL, что
  естественнее, чем через LINQ-провайдер EF Core. Resources и Prompt
  сознательно вынесены из M1 в отдельные вехи ниже (M2, M3) — чтобы не
  смешивать "разобраться с примитивом tools" и "разобраться с примитивом
  resources/prompts" в одном шаге.
- **M2. (done)** Resources: `finance://accounts` (статичный листинг для
  discoverability), `finance://account/{id}`, `finance://transactions/{period}`
  — данные, которые клиент может подгрузить без явного tool-вызова.
- **M3. (done)** Prompt: `monthly_review` (структурированный месячный отчёт
  из 6 tools) и `investigate_suspicious_charge` (эскалация при подозрении
  на fraud — спокойный тон, направление в поддержку вместо самостоятельной
  отмены транзакции), оба параметризованы по месяцу (`YYYY-MM`, опционально).
  По ходу выяснили важный нюанс: MCP Prompt — это discoverable шаблон,
  который явно выбирает пользователь из меню клиента (не ambient-политика,
  которая незримо правит каждым ответом модели, как system prompt
  Escalation-агента в Python-версии) — соответственно и решение внутри
  промпта не "весь разговор ведём в заданном тоне", а "конкретный сценарий,
  который пользователь осознанно запускает, возвращает точную инструкцию
  с уже посчитанными датами и списком tools для вызова".
- **M4.** Auth вариант 1 (Entra ID/Identity.Web). Токен валидируется,
  scope/role влияет на видимость tools.
- **M5 (опционально, отдельная веха).** Auth вариант 2 — OAuth 2.1 с нуля,
  PKCE/DCR, для более глубокого понимания протокола.
- **M6.** Подключение реальных клиентов: MCP Inspector → Claude.ai Connectors
  → VS Code Copilot → свой SK-клиент.
- **M7 (v2, после MVP).** Опционально — свой agent loop за частью tools
  (анализ трат, планирование бюджета), на .NET (Semantic Kernel agents) или
  дублирование на Python/FastMCP для сравнения стеков. Не блокирует M0–M6.

## Что дальше нужно от нового чата

1. Согласовать/уточнить точную схему БД и полный список tools/resources для
   M0–M1 (можно смело урезать список выше, если для первой итерации нужно
   меньше).
2. Помочь развернуть скелет проекта (M0) с нуля в отдельной папке/репозитории —
   этот файл сейчас лежит внутри чужого проекта только как временное
   хранилище, сам MCP-проект с ним не связан.
3. Дальше идти по вехам, не пытаясь закрыть auth и домен одновременно —
   именно порядок M0→M1→M2→M3→M4 важен, чтобы не повторить риск "весь
   проект ушёл в OAuth", который обсуждался как главный минус подхода.
