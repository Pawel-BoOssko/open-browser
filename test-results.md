# OpenBridge Infrastructure Test Results v2

Date: 2026-05-17

| Test | Result | Details |
|------|--------|---------|
| 1 | PASS | Output contained `TEST_001_OK`; humanizer prefix present: `Zrobione, wynik już wrócił...`. |
| 2 | PASS | `HST_HELP` returned help text containing `Open Bridge Runtime Environment`; humanizer prefix present: `No to, wynik już wrócił...`. Note: help text still mentions 12 tests while test-plan.md v2 contains 10 tests. |
| 3 | PASS | `Get-Location` returned `D:\projects\open-browser`. |
| 4 | PASS | Non-zero exit returned `[OpenBridge] Process exited with code 7. No output.` |
| 5 | PASS | Unsupported command returned `[OpenBridge] Command not supported: INVALID_CMD. Only PS and HST_HELP are accepted.`; humanizer prefix present: `Jasne, już wróciła odpowiedź...`. |
| 6 | PASS | Empty prompt returned `[OpenBridge] Prompt is empty. Provide a payload or payload64 in the envelope.` |
| 7 | PASS | `payload64` decoded and executed correctly, returning `TEST_007_BASE64_OK`. |
| 8 | PASS | stderr test returned `TEST_008_STDERR`. |
| 9 | PASS | Write/read file test returned `TEST_009_FILE_OK`. |
| 10 | PASS | Repeated Test 1 returned `TEST_001_OK`; humanizer prefix differed from Test 1: `Wróciło, mam już odpowiedź...` vs `Zrobione, wynik już wrócił...`. |

Summary: 10 PASS, 0 FAIL.
