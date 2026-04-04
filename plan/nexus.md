---
title: Nexus — Personal Productivity Dashboard
tags:
  - project
  - dotnet
  - blazor
  - ai-agents
status: planning
created: 2026-03-30
---

# Nexus — Personal Productivity Dashboard

> [!abstract] What is this?
> A single-user personal dashboard that aggregates GitHub, Jira, Trello, and Microsoft To-Do into one interactive surface. Built with **Blazor Server** targeting **Web browser** (self-hosted). Includes write operations, local reminders, performance reporting, and an ambient AI agent layer.

## Design Principles

> [!tip] Simplification rules
> - **Single user** → no identity server, no device registration, no multi-tenant data
> - **PAT-first auth** → no OAuth backend proxy needed for most services
> - **PostgreSQL default** → Npgsql + EF Core; provider-abstracted (`IDatabaseProvider`) so switching to SQL Server or SQLite is a config-only change
> - **No push infra** → browser notifications via Web Notification API
> - **Mobile-first UI** → responsive CSS Grid/Flexbox layout; all pages usable on phone-sized screens
> - **Ambient AI** → proactive insights, scheduled agents, inline actions — no chat panel

---

## Solution Structure

```
Nexus.sln
├── Directory.Build.props
├── Directory.Packages.props
├── src/
│   ├── Nexus.Domain/              ← entities, value objects, connector interfaces
│   ├── Nexus.Application/         ← MediatR handlers, use cases, DTOs
│   ├── Nexus.Infrastructure/      ← EF Core, HTTP connectors, token store, sync engine
│   ├── Nexus.Shared.Components/   ← Blazor RCL — ALL pages & widgets
│   └── Nexus.Web/                 ← Blazor Server (self-hosted, PostgreSQL default)
└── tests/
    ├── Nexus.Domain.Tests/
    └── Nexus.Infrastructure.Tests/
```

> [!note] No `Nexus.Api` project
> The web app is **Blazor Server** (runs on localhost), accessing the database directly via EF Core. No WASM/API split needed for a personal app.

---

## Core Domain Model

### Entities (`Nexus.Domain/Entities/`)

| Entity | Purpose |
|--------|---------|
| `UnifiedTask` | Central aggregate — local or linked to an external service |
| `Project` | Groups tasks; optionally links to a GitHub repo, Jira project, Trello board |
| `Integration` | Per-service config + token key + last sync state |
| `ServiceItem` | Raw external JSON payload — allows re-mapping without re-fetching |
| `ActivityEvent` | Immutable state-change log — powers all reports |
| `Reminder` | Scheduled notification linked to a task |
| `SyncCheckpoint` | Delta cursor per integration for incremental pull |
| `ReportSnapshot` | Materialized daily/weekly aggregates for fast charts |
| `InsightRecord` | AI-generated observation with optional suggested action |
| `AgentJob` | User-configured scheduled agent (cron, prompt template, enabled) |
| `AgentRun` | Execution log per job run — output, actions taken, errors, duration |
| `ActionLog` | Every AI-executed write action — tool, payload, undo payload, timestamp |

### Key Value Objects

- `ExternalRef(ServiceType, ExternalId, Url, ProjectKey?)` — links a task to its remote source
- `ServiceType` enum: `GitHub | Jira | Trello | MicrosoftTodo`
- `TaskStatus` enum: `Open | InProgress | Done | Blocked | Cancelled`
- `AuthMode` enum: `PersonalAccessToken | OAuthPkce`

### EF Core Strategy

- **PostgreSQL by default** via `Npgsql.EntityFrameworkCore.PostgreSQL`
- `IDatabaseProvider` abstraction — swap to SQLite or SQL Server via `Database:Provider` config, no code change
- Migrations run at startup via `db.Database.MigrateAsync()`

---

## Integration Architecture

### `IServiceConnector` — the extensibility spine

```csharp
public interface IServiceConnector
{
    ServiceType ServiceType { get; }
    string DisplayName { get; }
    AuthMode[] SupportedAuthModes { get; }
    string[] RequiredScopes { get; }

    // PAT path (preferred — no backend)
    TokenSet BuildPatTokenSet(string pat);

    // OAuth path (MS To-Do only)
    Uri BuildAuthorizationUrl(OAuthPkceParams pkce);
    Task<TokenSet> ExchangeCodeAsync(string code, OAuthPkceParams pkce, CancellationToken ct);
    Task<TokenSet> RefreshTokenAsync(TokenSet current, CancellationToken ct);

    // Read
    Task<IReadOnlyList<ServiceItem>> FetchItemsAsync(
        Integration integration, DateTimeOffset? since, CancellationToken ct);

    // Write
    Task<ServiceItem> CreateItemAsync(Integration integration, CreateItemRequest req, CancellationToken ct);
    Task<ServiceItem> UpdateItemAsync(Integration integration, string externalId, UpdateItemRequest req, CancellationToken ct);
    Task AddCommentAsync(Integration integration, string externalId, string body, CancellationToken ct);
    Task CloseItemAsync(Integration integration, string externalId, CancellationToken ct);

    // Mapping
    UnifiedTask MapToUnifiedTask(ServiceItem item, Guid? projectId = null);
    ServiceItem MapFromUnifiedTask(UnifiedTask task);
}
```

> [!tip] Adding a new service
> Implement `IServiceConnector` + register in DI. `ConnectorRegistry` (keyed `IEnumerable<IServiceConnector>`) picks it up automatically — polling, sync, AI tools, and UI connector cards all just work.

### Auth Modes

**PAT (default — no backend needed)**
- User pastes token in `ConnectorAuthPage.razor`
- Stored in `ITokenStore` → `SecureStorage` on MAUI, encrypted DB column on Blazor Server
- PATs don't refresh — connector uses them as-is until user rotates

**OAuth PKCE (MS To-Do / Graph only)**
- MAUI: `WebAuthenticator.AuthenticateAsync()` → system browser → deep link intercept
- Blazor Server: localhost redirect URI, handled in-process
- `client_secret` in user secrets / env var — never on Android device

| Service | PAT | OAuth |
|---------|-----|-------|
| GitHub | ✅ Fine-grained token | ✅ |
| Jira | ✅ API token (Basic auth) | ✅ Cloud |
| Trello | ✅ API key + token | ✅ |
| MS To-Do | ❌ Graph requires OAuth | ✅ required |

### Sync Strategy — Polling Only

> [!info] No webhooks
> Webhooks require a public-facing server. Polling is simpler and sufficient for personal use.

| Service | Interval | Method |
|---------|----------|--------|
| GitHub | 5 min | Notifications API (`X-Poll-Interval`) |
| Jira | 2 min | JQL `assignee = currentUser() ORDER BY updated DESC` |
| Trello | 3 min | `/members/me/cards`, board delta |
| MS To-Do | 5 min | Graph delta queries (`/me/todo/lists/{id}/tasks/delta`) |

`RateLimitedHttpHandler` (DelegatingHandler) reads rate-limit response headers and backs off automatically.

**Background sync:**
- `IHostedService` with `PeriodicTimer` — always running

---

## UI Navigation

```
AppShell
├── Dashboard /            ← InsightsWidget, TaskSummaryWidget, GitHubPRWidget,
│                            SprintWidget, BlockedItemsWidget, SyncStatusWidget, QuickAddWidget
├── Inbox /inbox           ← unread items from all connected services
├── Tasks /tasks           ← All | Today | Overdue | By Project
├── Projects /projects     ← task list + linked service items per project
├── Connectors /connectors ← connected services + add new + per-service config
├── Reports /reports       ← VelocityReport, ActivitySummary, BlockedItemsReport
├── Agents /agents         ← AgentJobsPage + ActionLogPage
└── Settings /settings     ← AI provider, sync intervals, notification prefs, theme
```

Widgets: `IWidgetDataProvider<T>`, skeleton loader, collapse/expand. Dashboard layout is CSS Grid (single-column on mobile, multi-column on tablet/desktop) with positions persisted to the database. All interactive elements meet minimum 44px touch target size.

---

## Notifications & Reminders

No push infra. Browser notifications only.

| Platform | Mechanism |
|----------|-----------|
| Web (Blazor Server) | Browser Notification API via JSInterop |

`ReminderScheduler` (Application layer):
1. Persists `Reminder` to the database
2. Schedules via JSInterop `Notification` API
3. Background service polls `Reminders WHERE FireAt <= NOW AND IsFired = 0` every minute

---

## Performance Reporting

`ActivityEvent` records every state change via EF Core `SaveChangesInterceptor`. Nightly `IHostedService` materializes `ReportSnapshot` rows.

**Metrics tracked:**
- Velocity: tasks completed per day/week, avg cycle time (created → done)
- Activity: PRs merged, issues closed, cards completed per board
- Focus: tasks blocked > N days, overdue trend, "graveyard" tasks (never started)

Charts: lightweight SVG in `VelocityReport.razor` and `ActivitySummary.razor` — no chart library dependency.

---

## Sync Strategy

> [!note] Database is always source of truth
> Sync pulls remote → DB, then pushes local changes → remote.

```
Pull  → FetchItemsAsync(since: checkpoint) → upsert ServiceItems → map to UnifiedTasks
Push  → Tasks WHERE UpdatedAt > lastSync AND ExternalRef != null → UpdateItemAsync
Conflict → store ConflictRecord → surface in ConflictResolutionModal.razor
```

---

## AI Agent Layer

> [!important] Design decisions
> - **Pluggable provider** — swap Claude ↔ OpenAI ↔ Ollama via config, no code change
> - **Ambient AI** — no chat panel; AI acts through proactive insights, scheduled jobs, and inline actions
> - **Auto-execute + undo** — AI acts immediately; every write action is logged with a full undo payload

### `ILanguageModel` — pluggable AI provider

```csharp
public interface ILanguageModel
{
    string ProviderName { get; }    // "Claude" | "OpenAI" | "Ollama"
    string ModelId { get; }         // "claude-sonnet-4-6" etc.

    Task<string> CompleteAsync(
        string systemPrompt,
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<AgentTool>? tools = null,
        CancellationToken ct = default);

    IAsyncEnumerable<string> StreamAsync(
        string systemPrompt,
        IReadOnlyList<ChatMessage> messages,
        CancellationToken ct = default);

    // Agentic tool-use loop — handles multi-turn tool calls internally
    Task<AgentResult> RunAgentAsync(
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<AgentTool> tools,
        CancellationToken ct = default);
}
```

**Implementations:**
- `ClaudeLanguageModel.cs` — `Anthropic.SDK` NuGet (default)
- `OpenAiLanguageModel.cs` — `OpenAI` NuGet
- `OllamaLanguageModel.cs` — raw `HttpClient` to `http://localhost:11434`

`AiProviderFactory` reads `"Ai:Provider"` from config, returns the right `ILanguageModel`. Changeable from Settings UI.

### `ConnectorToolBridge` — connector actions as AI tools

Auto-generates `AgentTool` list from `ConnectorRegistry`:

```
list_tasks_{service}     → FetchItemsAsync
create_{service}_item    → CreateItemAsync
update_{service}_item    → UpdateItemAsync
add_{service}_comment    → AddCommentAsync
close_{service}_item     → CloseItemAsync
```

Every write tool call: records `ActionLog` entry with undo payload **first**, then calls connector.

### Undo System

```csharp
public sealed class ActionLog
{
    public Guid Id { get; private set; }
    public string ToolName { get; private set; }     // "github_close_issue"
    public string ExternalId { get; private set; }
    public string InputJson { get; private set; }    // what the AI sent
    public string? UndoPayload { get; private set; } // snapshot of state before action
    public bool IsUndone { get; private set; }
    public DateTimeOffset ExecutedAt { get; private set; }
    public string? AgentJobId { get; private set; }
}
```

`ActionLogPage.razor` — lists recent AI actions, "Undo" button available within 24h window. Undo calls `connector.UpdateItemAsync(undoPayload)` to restore prior state.

### Proactive Insights

Background timer (every 30 min). Rule-based detectors → AI summarization → `InsightRecord` → `InsightsWidget.razor` on dashboard.

**Built-in detectors:**

| Detector | Trigger |
|----------|---------|
| `StalePrDetector` | GitHub PRs open > N days with no activity |
| `BlockedTaskDetector` | Tasks in Blocked status > N days |
| `VelocityDropDetector` | This week's completions < 60% of last week's |
| `OverdueDetector` | Tasks past due date |
| `InboxOverloadDetector` | Unread inbox items > threshold |

`InsightRecord.SuggestedActionJson` (nullable) — if present, `InsightsWidget` shows an "Apply" button that auto-executes via `ConnectorToolBridge` and logs to `ActionLog`.

### Scheduled Agents

`AgentJobRunner` (IHostedService) uses Cronos to parse cron expressions, checks `AgentJobs WHERE NextRunAt <= NOW AND IsEnabled = true`.

**Built-in job templates:**

| Job | Schedule | What it does |
|-----|----------|--------------|
| Morning Digest | `0 8 * * *` | Yesterday's activity + today's priorities → notification + InsightRecord |
| Weekly Report | `0 9 * * MON` | Markdown performance summary → saved to `daily/` in this vault |
| Inbox Triage | `0 */2 * * *` | Auto-labels/prioritizes new inbox items |
| Stale PR Sweep | `0 18 * * FRI` | Lists stale PRs, adds "needs attention" label |

Each run: collect DB context → `RunAgentAsync` → agent uses tools → all writes auto-logged → fire notification → store `AgentRun`.

`AgentJobsPage.razor` — list, enable/disable, edit schedule/prompt, view run history.

### Inline Actions

Contextual AI buttons on `TaskDetailPage.razor` and `ServiceItemDetailPage.razor`. All **read → generate text** — no auto-write. Result appears in a slide-over panel.

| Context | Actions |
|---------|---------|
| GitHub PR | Summarize changes, Draft review comment, Suggest next steps |
| Jira ticket | Summarize thread, Write status update, Link related tasks |
| Blocked task | Diagnose blocker, Draft escalation message |

### AI Layer File Structure

```
src/
├── Nexus.Domain/Ai/
│   ├── ILanguageModel.cs
│   ├── AgentTool.cs
│   ├── AgentResult.cs
│   ├── ChatMessage.cs
│   └── IAgentToolRegistry.cs
├── Nexus.Infrastructure/Ai/
│   ├── ClaudeLanguageModel.cs
│   ├── OpenAiLanguageModel.cs
│   ├── OllamaLanguageModel.cs
│   ├── AiProviderFactory.cs
│   ├── ConnectorToolBridge.cs
│   ├── InsightEngine.cs
│   └── AgentJobRunner.cs
└── Nexus.Shared.Components/Ai/
    ├── InsightsWidget.razor
    ├── AiActionsMenu.razor
    ├── AgentJobsPage.razor
    └── ActionLogPage.razor
```

**AI NuGet packages:** `Anthropic.SDK`, `OpenAI`, `Cronos`

---

## Implementation Phases

- [ ] **Phase 1 — Foundation** (2–3 weeks)
  - Solution scaffold: 4 projects, `Directory.Build.props`, `Directory.Packages.props`
  - Domain entities, `NexusDbContext`, PostgreSQL migrations
  - `IServiceConnector`, `ConnectorRegistry`, `ITokenStore`
  - `MicrosoftTodoConnector` (Graph SDK, OAuth PKCE, `/me/todo/lists/{id}/tasks/delta`)
  - OAuth PKCE in-process redirect handler (localhost redirect URI)
  - `Nexus.Web`: Blazor Server, AppShell, routing, encrypted DB token store
  - `Nexus.Shared.Components` RCL: MainLayout, NavMenu (bottom tab bar on mobile / sidebar on desktop), DashboardPage, `ConnectorAuthPage`
  - Mobile-first CSS baseline: CSS custom properties, responsive breakpoints, touch-friendly targets
  - ==Milestone: MS To-Do tasks render in TaskListPage at `http://localhost:5000` on both desktop and mobile viewport==

- [ ] **Phase 2 — GitHub + Jira + Write Operations** (2–3 weeks)
  - `GitHubConnector` (Octokit.NET — PAT + OAuth)
  - `JiraConnector` (HttpClient + Jira REST v3 — Basic auth)
  - Write ops in `TaskDetailPage.razor` (create, update, comment)
  - `RateLimitedHttpHandler` + DB rate limit store
  - `SyncEngine.PullAsync` + `SyncEngine.PushAsync`
  - Dashboard widgets: `GitHubPRWidget`, `SprintWidget`

- [ ] **Phase 3 — Trello + MS To-Do + Reminders** (2 weeks)
  - `TrelloConnector` (API key + token)
  - `ReminderScheduler` + browser notifications (JSInterop)
  - `NotificationsWidget`, `InboxPage.razor`

- [ ] **Phase 4 — Reports + Conflict UI** (1–2 weeks)
  - `ActivityEvent` via `SaveChangesInterceptor`
  - `ReportSnapshot` nightly job
  - `VelocityReport.razor`, `ActivitySummary.razor`, `BlockedItemsReport.razor`
  - `ConflictResolver` + `ConflictResolutionModal.razor`
  - Sync status badge in NavMenu

- [ ] **Phase 5 — AI Agent Layer** (2–3 weeks)
  - `ILanguageModel` + `ClaudeLanguageModel` (default)
  - `AiProviderFactory` + settings UI
  - `ConnectorToolBridge` + `ActionLog` entity + undo flow
  - `InsightEngine` + `InsightsWidget.razor`
  - `AiActionsMenu.razor` inline on detail pages
  - `AgentJob` + `AgentRun` + `AgentJobRunner` (Cronos)
  - Built-in jobs: Morning Digest, Weekly Report, Inbox Triage, Stale PR Sweep
  - `OpenAiLanguageModel` + `OllamaLanguageModel`
  - ==Milestone: Morning Digest fires at 8am, browser notification received, ActionLog shows undo-able actions==

---

## Critical Files

| File | Why |
|------|-----|
| `src/Nexus.Domain/Connectors/IServiceConnector.cs` | Spine — everything depends on this being stable |
| `src/Nexus.Domain/Connectors/AuthMode.cs` | PAT vs OAuth enum + `TokenSet` structure |
| `src/Nexus.Domain/Ai/ILanguageModel.cs` | AI spine — all agent features depend on this |
| `src/Nexus.Infrastructure/Connectors/ConnectorRegistry.cs` | Plugin registration — lock in Phase 1 |
| `src/Nexus.Infrastructure/Data/NexusDbContext.cs` | Single EF Core context — PostgreSQL default, provider-swappable |
| `src/Nexus.Infrastructure/Sync/SyncEngine.cs` | Pull/push/conflict orchestration |
| `src/Nexus.Infrastructure/Ai/ConnectorToolBridge.cs` | Maps connector actions → AI tools; stable before Phase 6 |
| `src/Nexus.Infrastructure/Ai/AgentJobRunner.cs` | Scheduled agent orchestration (cron + tool-use loop + action logging) |
| `src/Nexus.Shared.Components/Connectors/ConnectorAuthPage.razor` | PAT input + OAuth redirect trigger |
| `src/Nexus.Shared.Components/Ai/ActionLogPage.razor` | Undo surface — critical for auto-execute safety |

---

## Verification Checklist

- [ ] **Phase 1:** MS To-Do tasks render after OAuth sign-in at `http://localhost:5000`
- [ ] **Phase 2:** Create GitHub issue from app → visible on GitHub; Jira update syncs back in < 2 min
- [ ] **Phase 3:** Set reminder → browser notification fires at scheduled time
- [ ] **Phase 4:** Complete 5 tasks in a week → VelocityReport bar chart is correct
- [ ] **Phase 5:** Morning Digest fires at 8am → browser notification → ActionLog shows closed stale PR → Undo restores it
