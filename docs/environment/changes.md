# Environment Changes Log

- **2026-05-16**: Created OpenBridge environment documentation skeleton according to the approved architecture decisions.
- **2026-05-16**: CC envelope end-to-end proof completed. Isolated path (parser → mapper → Host → ClaudeCodeExecutor → real Claude Code) works through local manual probe. Runtime command approval design added. Automatic runtime execution remains disabled.
- **2026-05-16**: DryRun-only runtime command approval UI implemented. Detected CC envelopes now show a pending command panel with Approve/Reject buttons. Approved commands execute through OpenBridgeHost in DryRun mode only. No real Claude Code is launched from runtime. Result is shown in UI only — no WebView injection.
- **2026-05-16**: Runtime approval UI hardened. Added copy details/result buttons, improved prompt truncation, operation_id/duration display, host_execution_started logging. Manual test document created. Process mode from runtime remains disabled.
