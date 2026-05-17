# Claude Code Executor Decisions

Status: approved implementation decisions
Created: 2026-05-16
Parent: [claude-code-executor-design.md](claude-code-executor-design.md)

---

## 1. Status

This document converts the 10 open design questions from `claude-code-executor-design.md` into approved implementation decisions — plus one additional decision on concurrency. No code is written here. These decisions are the implementation-ready direction for the first Claude Code executor milestone.

---

## 2. Decisions

| # | Topic | Decision | Reason | Implementation consequence |
|---|---|---|---|---|
| 1 | **Invocation method** | Dedicated .NET Process-based executor wrapper (Option C from design doc). Dry-run is the default mode. Process mode is implemented but disabled by default. | Full .NET control over process lifecycle, clean stdout/stderr separation via `ProcessStartInfo`, no shell interpretation of prompt content. Safer long-term than calling CLI directly from scattered call sites. | `ClaudeCodeExecutor` supports two modes via `ClaudeCodeExecutorOptions.Mode`: `DryRun` (default) and `Process`. Process mode uses `System.Diagnostics.Process` with redirected stdout/stderr, timeout via `CancellationToken`, and exit code capture. Real Claude Code is not configured as the default executable. |
| 2 | **First backend** | CLI invocation first. API-based DeepSeek/OpenRouter invocation deferred. | The CLI is already installed and tested in this environment (see `start_deepseek_open_browser.bat` pattern). API integration adds credential management and HTTP client complexity that can be a separate executor or backend later. | Executor calls `claude` (or configured command) via `Process.Start`. No HTTP client, no API key handling in the executor itself. |
| 3 | **Command source** | Exact command and flags read from one local configuration point. Not hardcoded in multiple classes. | Prevents drift between call sites. Allows switching between Claude Code, Cloud Code, or other backends by changing one config entry. | In the future: a small config class or settings file (e.g., `openbridge.local.json`, git-ignored) holds the command path and default flags. The executor reads from that single source. First milestone may use a simple constant or environment variable as placeholder. |
| 4 | **Interactive approvals** | Non-interactive only. If the tool asks for approval or blocks waiting for input, the executor times out and returns a controlled error. No permanent `--dangerously-skip-permissions` scripts in repo. | The coding agent must not block the Open Browser process waiting for human input. Non-interactive flags (`--print`, `-p`) are the contract. If the tool ignores them, timeout is the safety net. Permanent permission-bypass scripts were already deleted from the repo. | Executor passes `--print` or equivalent non-interactive flag. If the process is still running when timeout fires, Host kills it and returns `status: "timeout"` with a message indicating possible interactive block. |
| 5 | **Timeout** | Default: 720000 ms (12 minutes) for coding-agent work. Timeout belongs to the Host/executor layer, not to the LLM envelope. | Per architecture decision 14c: "tab process measures Host timeout, Host measures executor timeout." The LLM does not specify timeout in its envelope. Real coding-agent tasks need longer timeouts than simple shell commands. | `ClaudeCodeExecutor` receives `timeout_ms` from Host. Default in `ClaudeCodeExecutorOptions` is 720000. Host enforces the timeout via `CancellationToken`. On timeout, the process is killed and a structured timeout result is returned. Short timeout tests explicitly override the value. |
| 6 | **Max output** | Default max output returned upward: 50,000 characters. Larger stdout/stderr truncated with original length recorded in result fields. | Prevents enormous responses from bloating logs and overwhelming the LLM conversation. The truncation is transparent — the result flags that truncation occurred so the LLM knows it saw a preview. | `stdout_preview` and `stderr_preview` capped at `max_output_chars`. `stdout_full_truncated: true` and `stderr_full_truncated: true` set when truncation occurs. Full output logged to NDJSON if within log size limits, referenced by path otherwise. |
| 7 | **First executor type** | Dedicated `ClaudeCodeExecutor` as the first coding-agent path. Do not build a generic `SH` executor first. | A dedicated executor has a clear contract (prompt in, structured result out). A generic shell executor would need to handle arbitrary commands, increasing the safety surface before the Host layer is mature. `SH` can exist later as a separate executor. | `ClaudeCodeExecutor` implements the `IClaudeCodeExecutor` interface. Host depends on the interface, not the concrete class. |
| 8 | **Result delivery** | First milestone returns structured result to Host only. Do not inject results back into the WebView conversation until Host/result-return design is approved. | The result-to-LLM path requires decisions about system message injection, neutral linguistic wrapping (architecture decision 12), and how the tab runtime delivers Host responses to WebView. These are not designed yet. | Host receives the structured result from the executor and logs it. Host does not modify the WebView DOM or inject messages. The result is visible in NDJSON logs only. |
| 9 | **Credentials** | No credentials in repo. Use environment variables or ignored local config. | API keys and tokens must never be committed. This rule was already enforced (`.bat` launchers deleted). The executor reads credentials from the process environment, not from project files. | First implementation: executor inherits the process environment. `claude` CLI reads its own auth from `CLAUDE_CODE_*` or `ANTHROPIC_*` environment variables. No credential files in the repo. A future implementation may reference a git-ignored `openbridge.local.json` for non-sensitive config like command paths. |
| 10 | **Working directory** | Initial allowlist: `D:\projects\open-browser` only. No work outside that root unless explicitly allowed later. | Safety boundary. The coding agent must not modify files outside the project. This is enforced by Host, not trusted to the agent prompt. | Host validates `working_directory` before passing to executor. Any path outside `D:\projects\open-browser` is rejected with `error_code: "WORKING_DIRECTORY_NOT_ALLOWED"`. A configurable allowlist may be added later. |
| 11 | **Concurrency** | One `ClaudeCodeExecutor` operation at a time. No parallel operations in the first implementation. | Simplifies state management, log correlation, and timeout handling. Parallel coding agents on the same repo would risk file conflicts and confusing interleaved output. | If Host receives a second `CC` command while one is running, it rejects with `error_code: "EXECUTOR_BUSY"`. No operation queue. No async dispatch. |
| 12 | **Executor hot-swap** | Executors must be hot-swappable behind a minimal stable contract. Host must not depend on concrete executor implementation details. No plugin loader, registry, or service locator. | Future executor backends (real CLI, API-based, different coding agents) must be replaceable without changing Host code. The contract is a single-method interface at the executor boundary — no broader abstraction needed. | `IClaudeCodeExecutor` interface with one method: `ExecuteAsync(HostCommandRequest, CancellationToken)`. Host stores and calls the interface. Concrete executors (dry-run, delayed-test, real CLI) implement the interface. Host defaults to `new ClaudeCodeExecutor()` when no executor is injected. |
| 13 | **Local configuration** | Executor options can be loaded from a git-ignored local JSON file. A committed example config shows the safe default structure. Dry-run remains the default. | Real Claude Code / Cloud Code command must not be hardcoded in source. It must live in ignored local config. The example config provides a documented, safe starting point. | `config/local/*.local.json` is git-ignored. `config/examples/claude-code-executor.example.json` is committed with `Mode: DryRun` and empty executable path. `ClaudeCodeExecutorOptionsLoader` provides `TryLoad` (returns null for missing file) and `LoadOrThrow`. No secrets, no permanent permission bypass. |

---

## 3. First implementation boundary

The first code milestone achieved:

- **Minimal Host stub** — validates `command == "CC"`, assigns `operation_id`, validates working directory, calls executor, enforces timeout.
- **`IClaudeCodeExecutor` + `ClaudeCodeExecutor`** — hot-swappable executor behind a one-method interface. Two modes exist:
  - **DryRun** (default) — echoes the prompt, no process launched.
  - **Process** — launches a configurable local process via `System.Diagnostics.Process`, captures stdout/stderr/exit code, enforces output truncation.
- **`ClaudeCodeExecutorOptions`** — single config point for `Mode`, `ExecutablePath`, `ArgumentsTemplate`, default timeout (720000 ms / 12 min), and max output chars. Dry-run is the default mode. No real Claude Code executable is configured.
- **`OpenBridgeHostCommandMapper`** — minimal static mapper from `OpenBridgeEnvelope` to `HostCommandRequest` for the CC path only. Does not execute commands, does not route, does not accept arbitrary working directories from envelopes.
- **Smoke tests** — cover dry-run, process mode, process timeout, stderr capture, invalid executable, output truncation, config loading, envelope-to-host mapping, and timeout defaults.

**Explicitly allowed but not required in the first milestone**:
- A real `claude` CLI call behind a feature flag or config toggle, if the echo mode works and the environment is verified.
- Process mode exists for this purpose but the executable path is not hardcoded — it must come from one local config point in the future.

**Current state**: Process mode and envelope mapping are implemented and tested. The mapper is isolated — it converts data only. No execution, no routing, no WebView integration.

**Current state**: Process mode is implemented and tested with harmless local commands. Real Claude Code invocation is not configured and not enabled by default.

---

## 4. Explicit non-goals

These must NOT be implemented in the first milestone:

- No generic `SH` (shell) executor
- No registry of LLM instances or `conversation_id` mapping
- No cross-tab communication (`XTB_*`)
- No watchdog or recovery processes
- No state machine for tab or operation lifecycle
- No WebView DOM injection or system message delivery to the LLM conversation
- No parallel or queued operations
- No executor auto-discovery or plugin system
- No executor registry, plugin loader, service locator, factory hierarchy, or routing table
- No future-proof abstractions, layers, services, managers, controllers, or validators unless explicitly approved
- No dedicated Cloud Code Connector — Cloud Code is a CLI tool launched through the existing ClaudeCodeExecutor / Process mode, not a separate architectural component
- No `BridgeBrowserHelper` or any reference to the old helper
- No permanent `--dangerously-skip-permissions` scripts or `.bat` launchers
- No package installation (`dotnet add package`)
- No git push

---

## 5. Validation required after implementation

Before committing any code resulting from these decisions:

1. `git status --short` — repo must be clean before starting
2. `dotnet build -c Release` — 0 errors
3. `dotnet run --project tests/OpenBridgeProtocolSmoke -c Release` — all tests pass
4. Executor smoke test — prove echo/dry-run executor flow without calling external service
5. Safety grep — no `BridgeBrowserHelper`, `HelperCommandBus`, `watchdog`, `tab registry`, `XTB_`, `__BRIDGE_BROWSER_HELPER__` in runtime source
6. No credentials in committed files
7. No `--dangerously-skip-permissions` in committed files
8. Build and smoke tests must pass before commit
