# PS Help

Purpose: Defines model-facing help for the PowerShell executor.

Depends on:
- docs/runtime/hst-help_new.md
- docs/executors/PS/rules_new.md
- docs/agent/coding-invariants_new.md

Impacts:
- PS_HELP output
- PowerShell command usage
- model operator behavior
- text/file operations through PS

Update rule: Update this file when PS executor behavior, command usage, encoding behavior, RAW guidance, or PowerShell anti-patterns change.

Latest:
- PS is preferred for simple Windows, filesystem, and process operations.
- Simple commands are more reliable than large defensive blocks.
- Python should not be invoked with Bash syntax inside PS.
- For long or fragile payloads, use RAW as described in HST_HELP.

## What PS is for

Use PS for simple Windows and filesystem work: `Get-Location`, `Get-ChildItem`, `Get-Item`, `Get-Content`, `Set-Content`, `New-Item`, `Copy-Item`, `Move-Item`, `Remove-Item`, process checks.

PS is not automatically better than PY for text parsing, Unicode verification, JSON/CSV/log processing, or larger structured data work.

## Simple command rule

Prefer simple commands over long defensive scripts when the task is simple.

Long commands increase the chance of model typo, quoting error, JSON escaping error, transport failure, and harder diagnosis.

Use this sequence by default:

```text
inspect -> simple action -> verify
```

## Do not use Bash syntax in PS

Do not use Bash heredoc or Bash-only syntax inside PS payloads.

Wrong pattern:

```powershell
python - <<'PY'
```

PowerShell is not Bash.

If Python is the right tool, use the PY executor or a PowerShell-compatible Python invocation.

## Text and encoding

PowerShell can write UTF-8 files when explicit encoding is used.

Returned PS stdout may still be unsafe for non-ASCII verification in the current OpenBridge path. Mojibake in returned output does not automatically prove that the file is corrupted.

For Unicode-sensitive verification, consider PY or another controlled method.

## RAW

For long, quote-heavy, markdown-heavy, JSON-heavy, backslash-heavy, Polish-text-heavy, or OpenBridge-sensitive payloads, use RAW as described in `HST_HELP`.

Do not solve fragile quoting by building a larger PowerShell string escape puzzle.
