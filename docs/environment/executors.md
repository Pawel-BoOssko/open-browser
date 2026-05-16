# Executors

## Active (isolated Host path — not runtime-connected)
- **ClaudeCodeExecutor**: Dedicated CC command executor. Two modes:
  - **DryRun** (default): Echoes the prompt, no process launched. Safe for testing.
  - **Process**: Launches a configurable local process via System.Diagnostics.Process. Available through local manual probe and ignored local config only. Not enabled from runtime.

## Planned
- HST: Host metacommands (HELP, STATUS, CAPABILITIES)
- FS: Filesystem operations (read, write, list, copy, move, mkdir, delete, stat)
- SH: Shell commands (run system command and return stdout/stderr/exit code)
- DIA: Diagnostics
- PY: Python execution (to be considered)

## Status notes

- ClaudeCodeExecutor exists and is tested with both dry-run and real Claude Code (via local manual probe only).
- Real Process mode is available only through the local manual probe (`tools/local/`) with ignored local config (`config/local/`).
- Runtime execution of CC commands is not connected. An approval layer must be implemented first.
- First runtime integration must use DryRun only. Process mode from runtime requires separate approval.
- Result delivery to the LLM conversation is not implemented.
