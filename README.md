# Open Browser

A local desktop runtime that gives an LLM controlled access to your system through a real browser session.

**LLM decides → executor executes → LLM receives the result.** No human in the loop. No cloud middleware. Everything runs locally.

## How it works

1. An LLM (ChatGPT, Claude, etc.) runs in a real browser session inside a WebView2 window
2. The LLM includes an **execution envelope** in its response — a JSON with a PowerShell command
3. Open Browser detects the envelope, executes the command via PowerShell, and injects the result back into the chat
4. The LLM sees the result and can act on it in its next turn
5. The cycle continues autonomously

## Quick start

### Prerequisites

- Windows 10/11
- .NET 8 SDK
- WebView2 Runtime (included in Windows 11, installable on Windows 10)

### Build and run

```powershell
dotnet build releases\BridgeBrowserAlpha0\src\BridgeBrowserAlpha0\BridgeBrowserAlpha0.csproj
dotnet run --project releases\BridgeBrowserAlpha0\src\BridgeBrowserAlpha0\BridgeBrowserAlpha0.csproj
```

### Run tests

```powershell
dotnet run --project tests\OpenBridgeProtocolSmoke\OpenBridgeProtocolSmoke.csproj
dotnet run --project tests\OpenBridgeHostSmoke\OpenBridgeHostSmoke.csproj
```

## Sending commands

The LLM sends commands via execution envelopes in its response:

```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"Get-Location"}
@@OPENBRIDGE_EXEC_END@@
```

Available commands: `PS` (PowerShell), `HST_HELP`, `HST_TOOLS`, `HST_STATUS`.

The full runtime contract for the model is in [docs/runtime/environment.md](docs/runtime/environment.md).

## Tools

Built-in toolkits for external services:

| Tool | Description |
|------|-------------|
| `tools/n8n/` | n8n workflow management via Public API + Supabase queries |
| `tools/github/` | GitHub file publishing via Contents API |
| `tools/linkedin/` | LinkedIn posting via OAuth 2.0 + REST API |
| `tools/claude/` | Claude Code launcher via DeepSeek API |

## Architecture

```
LLM (browser tab)
  → ResponseExtractor (detects envelopes)
  → OpenBridgeHost (routing + concurrency)
  → GeneralCommandExecutor (PowerShell process)
  → result injected back to chat
```

Key properties:
- Shared Host instance with concurrency lock — one command at a time
- 360s cycle timeout — model always gets a response
- Output truncated at 50,000 characters
- Human-like response delay with randomized text wrapping

## Status

**Alpha / technical preview.** Under active development. APIs, envelope format, and command names may change.

For developers working on the project, see [docs/development/](docs/development/).

## License

MIT
