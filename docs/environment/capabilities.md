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

## Planned
- Runtime command approval UI (Operator-controlled approve/reject for detected CC envelopes)
- Runtime-to-Host DryRun execution (First integration with DryRun only, no real Process mode from runtime)
- HST_STATUS / HST_CAPABILITIES (Host self-discovery protocol)
- HST_HELP (Host manual)
- Result delivery design (How Host results reach the LLM conversation)
- Watchdog (Active flow supervision)
- Cross-tab communication
