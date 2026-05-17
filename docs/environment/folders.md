# Folder Structure

- `docs/` — Project documentation. See `docs/README.md` for structure and rules.
- `docs/runtime/` — Runtime contract for the LLM inside Open Browser (commands, envelope format).
- `docs/development/` — Development journal, rules, and architectural decisions for agents.
- `docs/architecture/` — Leggacy architectural documents (being phased out).
- `docs/environment/` — Legacy environment descriptions (being phased out).
- `docs/handoff/` — One-shot context handovers between agents.
- `releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/` — C# application core (WinForms, WebView2, OpenBridgeHost, executor).
- `releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/OpenBridgeHost/` — Host, approval, command mapper, executor interface.
- `releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/OpenBridgeProtocol/` — Envelope parser, observer, parse result types.
- `releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/OpenBridgeHost/GeneralCommand/` — General command executor.
- `tests/` — Smoke test projects (OpenBridgeProtocolSmoke, OpenBridgeHostSmoke, ResponseExtractorSmoke).
- `diagnostics/pipeline-output/` — Pipeline diagnostic dumps for debugging.
- `config/` — Configuration files. `config/local/` is git-ignored.
- `logs/` — Runtime logs in NDJSON format.
- `extracted/` — Extracted assistant message frames from model responses.
- `profile/` — WebView2 user data directory.
- `modules/` — Hot-swappable JavaScript modules (e.g. conversation-trimmer).
