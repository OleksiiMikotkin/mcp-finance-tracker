# MCP pet-проект — план изучения и реализации

> Стартовый контекст для нового чата в Claude Code. Фиксирует принятые
> решения и их причины, чтобы не пересматривать их без необходимости.
> Подробности по OAuth и находки по клиентам — в `OAUTH.md`.

## Зачем

Pet-проект для практического изучения MCP (Model Context Protocol) на .NET.
До него были только простые stdio-серверы без авторизации (учебные
домашки на Python). Здесь цель — закрыть то, чего там не было:
remote-транспорт (Streamable HTTP) и OAuth 2.1, плюс подключение к реальным
клиентам.

## Домен: трекер личных финансов

Выбран, потому что:
- данные чувствительные → авторизация нужна по существу, а не для галочки;
- естественно раскладывается на все три примитива MCP;
- полезно самому — есть мотивация развивать дальше.

Что реализовано:
- **Tools (18).** Аналитика (14): `get_transactions_in_period`,
  `get_balance`, `get_budget_status`, `get_top_categories`,
  `get_spending_by_category`, `get_spending_by_merchant`,
  `detect_subscriptions`, `analyze_spending_pattern`,
  `analyze_spending_by_hour`, `analyze_weekend_spending`,
  `simulate_savings`, `project_month_end_balance`,
  `detect_suspicious_transactions`, `detect_impossible_travel`.
  Команды (4): `add_transaction`, `categorize_transaction`,
  `reverse_transaction`, `set_budget`.
- **Resources (3):** `finance://accounts`, `finance://account/{id}`,
  `finance://transactions/{period}`.
- **Prompts (2):** `monthly_review`, `investigate_suspicious_charge`.
- Сознательно **не делаем**: sampling, elicitation, roots — слабо применимы
  к домену.

Почему такой набор: аналитические tools дают содержательное демо («забытая
подписка», «по выходным тратите больше»), а не голый CRUD. Resources и
prompts в туториалах часто пропускают и сводят MCP к одним tools — здесь
все три примитива сделаны осознанно, каждый отдельной вехой, чтобы
разбираться с ними по очереди.

**Демо-данные** строит `Mcp.FinanceTracker.DataGen`: скользящие 12 месяцев
до текущей даты, с зашитыми паттернами (подписка, переставшая списываться;
доставка поздно вечером; траты по выходным; подозрительные зарубежные
операции по карте). Перегенерировать, когда данные «устарели»: удалить
`finance.db`, перезапустить сервер — он заново заполнит базу из CSV.

Идеи домена (паттерны данных, описания аналитических tools, правила тона и
эскалации для prompts) взяты из учебного Python-проекта «Personal Finance
Coach» (multi-agent на LangGraph). Код и оркестратор не переносились:
маршрутизацию между агентами в MCP берёт на себя клиент, а правила тона,
которые там жили в system prompt отдельного Escalation-агента, здесь стали
MCP Prompt.

### Решение: без своего агента внутри сервера

В MCP оркестратором выступает клиент (Claude, Copilot): он сам решает,
какие tools вызывать и в каком порядке. Поэтому своего agent loop внутри
сервера нет. Если когда-то понадобится — отдельная веха (M7), чтобы не
раздувать текущий объём.

## Транспорт и авторизация

- **Streamable HTTP**, не stdio: OAuth в MCP применим только к
  remote-серверам.
- **Сервер — только Resource Server**: проверяет JWT, сам токены не
  выпускает. Authorization Server — внешний IdP.
- **Свой OAuth-сервер с нуля не пишем** (рассматривался и отклонён): по
  объёму это отдельный проект, и противоречит принципу «не пишите свой
  authorization server».
- Провайдеры: **WorkOS AuthKit** (M4, сделано) и **Entra ID** (M5, план).

## Пакеты

Сейчас: `ModelContextProtocol` и `ModelContextProtocol.AspNetCore` (2.2.0,
официальный C# SDK), `Microsoft.AspNetCore.Authentication.JwtBearer`,
`Microsoft.Data.Sqlite` (без ORM). Для M5 добавится
`Microsoft.Identity.Web`. Транзитивно приходит
`Microsoft.Extensions.AI.Abstractions` — та же абстракция, что у Semantic
Kernel, пригодится для своего SK-клиента.

В Python SDK есть низкоуровневый сервер и поверх него FastMCP. В C# SDK
удобный высокоуровневый API встроен в тот же пакет, отдельный «FastMCP для
.NET» не нужен. Tools/resources/prompts объявляются атрибутами — прямой
аналог декораторов FastMCP: `[McpServerToolType]`/`[McpServerTool]`,
`[McpServerResourceType]`/`[McpServerResource]` (с URI-шаблоном),
`[McpServerPromptType]`/`[McpServerPrompt]`, описания — через
`[Description]`. JSON Schema параметров SDK строит из сигнатуры метода.
Методы могут принимать `ClaimsPrincipal` и сервисы из DI.

## Вехи

Порядок вех выбран намеренно: сначала домен и примитивы MCP, потом
авторизация. Главный риск такого проекта — что он целиком уйдёт в OAuth, а
до самого MCP дело так и не дойдёт.

- **M0. (done)** Скелет: .NET 10, `ModelContextProtocol.AspNetCore`, вызов
  tool проверен через MCP Inspector.
- **M1. (done)** SQLite + 18 tools. Доступ к данным — сырой
  `Microsoft.Data.Sqlite`: максимум контроля, минимум зависимостей. SQL
  фильтрует строки, агрегация — в C# (LINQ).
- **M2. (done)** Resources: `finance://accounts` (листинг для
  discoverability), `finance://account/{id}`,
  `finance://transactions/{period}`.
- **M3. (done)** Prompts `monthly_review` и `investigate_suspicious_charge`,
  оба с необязательным месяцем (`YYYY-MM`).
  Важный вывод: MCP Prompt — это шаблон, который **пользователь явно
  выбирает** из меню клиента, а не фоновая политика, влияющая на каждый
  ответ (как system prompt). Поэтому prompt описывает конкретный сценарий:
  возвращает точную инструкцию с посчитанными датами и списком tools.
- **M4. (done)** OAuth 2.1 через **WorkOS AuthKit**, проверено в Inspector и
  Claude Desktop. Обычный `JwtBearer` против JWKS AuthKit, без SDK WorkOS.
  Задуманное разделение `Mcp.Read`/`Mcp.Write` невозможно: WorkOS кладёт
  роли и permissions только в свой внутренний session JWT, а не в токен
  стороннего клиента. Вместо этого доступ ограничен членством в
  организации (claim `org_id`).
- **M5.** OAuth через **Entra ID / Microsoft.Identity.Web** — тяжелее в
  настройке (Azure portal, App Registration), но типичный enterprise
  .NET-стек. Проверить в первую очередь: попадают ли App Roles в `roles`
  claim токена стороннего клиента — если да, разделение read/write может
  заработать здесь.
- **M6. (частично done)** Реальные клиенты.
  - **Done (2026-10-25):** MCP Inspector, Claude Desktop (локально через
    `mcp-remote` и через аккаунтный Connector), **Claude.ai** через
    ngrok-туннель. Один процесс сервера обслуживает localhost и публичный
    URL без логики «по клиенту». Находки (CIMD, `server/discover`, шаблоны
    ресурсов не поддерживаются клиентами Anthropic) — в `OAUTH.md`.
  - **Осталось:**
    - VS Code + GitHub Copilot (agent mode): remote-сервер прописывается в
      `mcp.json`, вход — через «Auth» в CodeLens.
    - Свой клиент на .NET через Semantic Kernel (нативная поддержка
      MCP-клиента с v1.71). Даёт полный контроль над демо, не зависящий от
      того, как сегодня выглядит UI чужих клиентов, и цельную историю на
      .NET от клиента до сервера.
- **M7 (после MVP, опционально).** Свой agent loop за частью tools
  (Semantic Kernel agents) или версия на Python/FastMCP для сравнения
  стеков.

## Технический долг

- `DetectSuspiciousTransactions`/`DetectImpossibleTravel` грузят всю
  историю в память и агрегируют в LINQ, а не в SQL. Для демо-объёма
  нормально, на большой истории не масштабируется.
- `simulate_savings`: `reductionPct` не проверяется на диапазон 0–100,
  хотя описание параметра это обещает.
- `set_budget`: `monthlyLimit` не проверяется на отрицательное значение.
- **Погашение кредитки смоделировано неправильно.** Генератор ежемесячно
  платит по кредитке, но почти не делает по ней покупок, а платёж пишет
  одной операцией (минус с дебетовой карты). В аналитике же `credit_payment`
  числится доходом (`IncomeCategories`), поэтому
  `get_spending_by_category('credit_payment')` всегда возвращает 0.
  Правильно: часть повседневных покупок вести по кредитке, погашение писать
  как перевод двумя операциями (минус с дебетовой, плюс на кредитную) и не
  считать переводы тратами. Обычный кредит (ипотека и т.п.), если появится,
  — отдельная категория-расход вроде `loan_payment`.
- **Nullable-параметры tools** (`string? merchant = null` и т.п.) SDK
  отдаёт как `"type": ["string", "null"]`. Claude это принимает, но клиенты
  с другим диалектом схем (Gemini в Copilot или SK-клиенте) могут
  отвергнуть tool. План: при подключении Copilot переписывать такие схемы
  в `anyOf` глобальной трансформацией при регистрации tools (API сверить с
  SDK). Nullability у параметров не убирать: optional ≠ null.

## Следующие шаги

1. Перевыпустить `WorkOS:ApiKey` (см. security-TODO в `OAUTH.md`).
2. M6: VS Code Copilot, затем свой SK-клиент.
3. M5: Entra ID.
