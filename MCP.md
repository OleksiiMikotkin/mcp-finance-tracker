# MCP.md

Термины протокола MCP, применительно к тому, как устроен этот проект
(OAuth-хендшейк с WorkOS AuthKit). Дополняет `OAUTH.md`.

## Кто есть кто

| Роль | Что это у нас | Реальный пример |
|---|---|---|
| **Host** | Приложение, в котором сидит пользователь | Claude Desktop, Claude.ai, MCP Inspector |
| **MCP Client** | Компонент **внутри** host'а — отдельный на каждое подключение к серверу. Именно он говорит по протоколу MCP и ведёт OAuth-хендшейк (открывает браузер, ловит редирект, меняет code на токен, хранит токен) | В Claude Desktop это мост `mcp-remote`; в Inspector — его встроенный OAuth-модуль |
| **MCP Server** (наш) | **Resource Server** — только проверяет JWT в заголовке, отдаёт tools/resources/prompts. В OAuth-хендшейке не участвует вообще | `Mcp.FinanceTracker.Server/Program.cs` |
| **Authorization Server / IdP** | Внешний сервис, который знает пароли, показывает форму логина, выпускает токены. **AuthKit — это и есть WorkOS**: AuthKit — фирменное название hosted-продукта логина внутри WorkOS, не отдельный сервис | WorkOS AuthKit (домен `*.authkit.app`, см. `WorkOS:AuthKitDomain` в `appsettings.json`) |

Частая путаница: "клиент открывает пользователю браузер с формой логина" —
это делает **MCP Client** (мост внутри Host'а), не наш сервер и не сам
Host целиком. А сама форма логина в браузере — это WorkOS AuthKit
(домен `authkit.app`, а не `workos.com`), просто у него отдельный бренд.
