# Build/Run + Playwright MCP UI Tests (FoundryAgent)

You are GitHub Copilot acting as a repo-local coding agent on Windows.

## Context
- Repo root: `d:\Repos\foundryAgent`
- Web app project: `d:\Repos\foundryAgent\src\FoundryAgent.Web`
- Playwright test harness: repo root (`package.json`, `test-chat.js`)
- Dev URL: `http://localhost:5109` (see `src/FoundryAgent.Web/Properties/launchSettings.json`)

## Constraints / requirements
- Use the correct working folder per command:
  - `dotnet` commands should run from `src/FoundryAgent.Web` (preferred) OR from repo root using `--project src/FoundryAgent.Web`.
  - `npm`/Playwright commands should run from repo root.
- MCP GitHub tool calls require an auth token header.
  - The server code checks `COPILOT_MCP_TOKEN` (or `GITHUB_COPILOT_MCP_TOKEN`) to add a Bearer token when calling `https://api.githubcopilot.com/mcp/`.
  - If missing, the app returns an error payload from tool execution; the UI tests should detect that and fail with a clear message.
- Keep selectors aligned with the existing UI:
  - `#mcpButton`, `#mcpPanel`, `.mcp-tool`, `.mcp-tool-name`, `#messageInput`, `#sendButton`, `#loadingIndicator`, `.message.agent .message-content`

## Task
1) Determine the exact PowerShell commands to build and run the app AND the correct folders to run them from.
   - Include both:
     - One-terminal workflow (run app foreground)
     - Two-terminal workflow (run app in a separate terminal / background)
   - Include how to stop the app.

2) Create or update Playwright UI tests to validate MCP + chat UI end-to-end against `BASE_URL=http://localhost:5109`.
   - Prefer keeping the existing `test-chat.js` but refactor if needed for reliability.
   - Add **reliable waits** (avoid flakiness), and **strong assertions**.

### Tests to implement
A) MCP panel smoke test
- Navigate to `BASE_URL`
- Click `#mcpButton`
- Assert `/api/mcp/discover` returns 200
- Assert MCP panel shows and at least one tool card renders
- Assert tool list contains a tool name exactly `search_repositories`

B) “List repos” via MCP tool through the UI
- Fill input with a message that forces a tool call:
  - `Use the search_repositories tool with query "user:sayedimac" and return top 10 repositories with name and url.`
- Click Send
- Wait for `#loadingIndicator` to appear then disappear
- Assert agent response contains evidence of repo results:
  - either JSON containing `full_name` and/or `html_url`, OR
  - several lines that look like repo names + URLs

C) Two additional MCP-driven questions
1. `Use get_file_contents to fetch README.md from owner "sayedimac", repo "foundryAgent", path "README.md". Summarize in 3 bullets.`
2. `Use list_issues for owner "sayedimac", repo "foundryAgent". If none, say "No issues found".`

D) Keep/validate existing tests
- Markdown rendering request
- File upload flow (if already present)

## Developer-friendly test behavior
- Log useful info:
  - status codes for `/api/mcp/discover` and `/api/chat`
  - last agent message snippet on each test
- Save screenshots on failure
- Exit with non-zero code on failure (so CI fails)
- Make `headless` configurable (default headless true unless `HEADLESS=false`)

## Important checks
- The project currently targets `net10.0` in `src/FoundryAgent.Web/FoundryAgent.Web.csproj`.
  - If that means I need a preview SDK installed, call it out clearly and suggest the exact fix (e.g., install the matching .NET 10 preview SDK OR adjust TargetFramework if the repo intends .NET 8/9).

## Output format
- Implement the code changes (edit/add files as needed).
- Then provide a short “How to run” section with exact PowerShell commands, including:
  - `dotnet restore/build/run`
  - `npm ci` (or `npm install`)
  - how to run Playwright tests
  - how to set `BASE_URL` and `COPILOT_MCP_TOKEN`
