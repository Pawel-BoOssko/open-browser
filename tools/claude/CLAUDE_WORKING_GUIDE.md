# Working with Claude Code from Open Browser

Claude Code is a separate AI coding assistant running via the DeepSeek API. Use it to delegate complex file analysis, code generation, or multi-step reasoning tasks that are cumbersome to do via individual PS commands.

## Session management (critical)

Claude Code saves conversation state as session files. Each session is a persistent context.

**First run — start a new session:**
```powershell
D:\projects\open-browser\tools\claude\claude-deepseek.ps1 -p "Your prompt here"
```
Claude Code prints the session ID in its output. Look for a line like:
```
Session ID: abc123def456
```
**Save this ID.** You will need it for every subsequent call to this session.

**Resume an existing session:**
```powershell
D:\projects\open-browser\tools\claude\claude-deepseek.ps1 --resume <session-id> -p "Follow-up prompt"
```
The session ID is the same one from the first run.

**List all past sessions for this project:**
```powershell
Get-ChildItem "$env:USERPROFILE\.claude\projects\D--projects-open-browser\" -Name
```
The filenames (without `.jsonl`) are the session IDs.

**Run from a specific working directory:**
```powershell
cd D:\projects\open-browser
D:\projects\open-browser\tools\claude\claude-deepseek.ps1 -p "Analyze this repo"
```

**Interactive session (no -p flag):**
```powershell
D:\projects\open-browser\tools\claude\claude-deepseek.ps1
```
Opens an interactive Claude Code session. The model can communicate via chat.

## Session file location

Sessions are stored in:
```
%USERPROFILE%\.claude\projects\<encoded-dir>\<session-id>.jsonl
```

For `D:\projects\open-browser`, the encoded directory name is `D--projects-open-browser`.

To list existing sessions:
```powershell
Get-ChildItem "$env:USERPROFILE\.claude\projects\D--projects-open-browser\"
```

## How Claude Code sees the project

When launched from `D:\projects\open-browser`, Claude Code has full access to the project files. It reads the documentation in `docs/`, the tools in `tools/`, and the source code. Use it for tasks that require deep understanding of the codebase.

## Important notes

1. **Always save the session ID.** Without it, you lose context between calls.
2. **First run = new session.** Do not use `--resume` on the first call.
3. **Session files grow.** Long sessions produce large `.jsonl` files.
4. **Not a replacement for PS.** Use PS for simple file operations, `git` commands, and tool execution. Use Claude Code for complex reasoning and code analysis.
5. **API key required.** The environment must have `DEEPSEEK_API_KEY` set.
