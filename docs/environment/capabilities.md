# System Capabilities

## Active
- PageTap (WebView HTTP interception)
- ResponseExtractor (Stream and delta assembly)
- Conversation Trimmer (Module for filtering large conversation trees)
- Diagnostics Controller (Runtime status reporting)
- OpenBridge Envelope Parser (EXEC/RAW envelope detection)
- OpenBridge Envelope Observer (Passive envelope detection in assembled responses — no command execution)
- OpenBridgeHost (Isolated Host with command validation, single-operation lock, timeout enforcement)
- ClaudeCodeExecutor (DryRun and Process execution modes behind hot-swappable contract)
- ClaudeCodeExecutorOptionsLoader (Local JSON config loading for executor options)
- OpenBridgeHostCommandMapper (Envelope-to-HostCommandRequest mapping for CC path)
- Local CC envelope E2E probe (Manual probe proving the full isolated path with real Claude Code)
- Runtime command approval UI (Operator-controlled approve/reject panel for detected CC envelopes — DryRun only)

## Planned
- Runtime-to-Host Process mode execution (requires separate approval)
- Result delivery to LLM conversation (not designed)
- HST_STATUS / HST_CAPABILITIES (Host self-discovery protocol)
- HST_HELP (Host manual)
- Result delivery design (How Host results reach the LLM conversation)
- Watchdog (Active flow supervision)
- Cross-tab communication
