# OpenBridge Infrastructure Test Plan v2

Run all tests in order. Record pass/fail after each. Save the full report at the end.

## Prompt for the model

> Read `D:\projects\open-browser\test-plan.md` via `Get-Content`. Execute all tests in order. After each test, record the result. After all tests, save the report to `D:\projects\open-browser\test-results.md` using a PowerShell command. Do not skip any test. Continue until all are done.

## Tests

### Test 1 — PS echo
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"Write-Output 'TEST_001_OK'"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains `TEST_001_OK`, humanizer prefix present.

### Test 2 — HST_HELP
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"HST_HELP"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: help text with "Open Bridge Runtime Environment", humanizer prefix present.

### Test 3 — Get-Location
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"Get-Location"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains `D:\projects\open-browser`.

### Test 4 — Non-zero exit code
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"exit 7"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: message `Process exited with code 7. No output.` (no silence).

### Test 5 — Unsupported command (error feedback)
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"INVALID_CMD","payload":"test"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: error message with "not supported", humanizer prefix present.

### Test 6 — Empty prompt rejection
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: error message with "empty" or "PROMPT_EMPTY".

### Test 7 — payload64 (base64)
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload64":"V3JpdGUtT3V0cHV0ICdURVNUXzAwN19CQVNFNjRfT0sn"}
@@OPENBRIDGE_EXEC_END@@
```
Decodes to: `Write-Output 'TEST_007_BASE64_OK'`. Expected: output contains `TEST_007_BASE64_OK`.

### Test 8 — stderr capture
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"[Console]::Error.WriteLine('TEST_008_STDERR')"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: stderr contains `TEST_008_STDERR`.

### Test 9 — Write then read file
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"Write-Output 'TEST_009_FILE_OK' | Out-File -FilePath 'D:\\projects\\open-browser\\test-output.txt' -Encoding UTF8; Get-Content 'D:\\projects\\open-browser\\test-output.txt'"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains `TEST_009_FILE_OK`.

### Test 10 — Humanizer variety check
Run Test 1 again and compare the humanizer prefix to Test 1's prefix. They must be different (no same prefix twice in a row).
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"Write-Output 'TEST_001_OK'"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: `TEST_001_OK` present, humanizer prefix DIFFERENT from Test 1.

---

## Report format

Save to `D:\projects\open-browser\test-results.md`:

```
# OpenBridge Infrastructure Test Results v2

Date: ...

| Test | Result | Details |
|------|--------|---------|
| 1 | PASS/FAIL | ... |
| 2 | PASS/FAIL | ... |
...
| 10 | PASS/FAIL | ... |

Summary: X PASS, Y FAIL.
```
