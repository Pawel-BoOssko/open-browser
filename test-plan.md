# OpenBridge Infrastructure Test Plan

Run these tests one by one. After each test, record the result.
Execute all 10 tests, then save the full report.

## Test procedure

For each test:
1. Send an envelope with the specified command.
2. Wait for the result to appear in chat.
3. Record: test number, pass/fail, what you received, any errors.

## Tests

### Test 1 — Basic PS echo
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"Write-Output 'TEST_001_OK'"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains `TEST_001_OK`, exit code 0.

### Test 2 — Get-Location
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"Get-Location"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains `D:\projects\open-browser`.

### Test 3 — HST_HELP metacommand
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"HST_HELP"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains "Open Bridge Runtime Environment", "PS", "HST_HELP".

### Test 4 — Unsupported command (error feedback)
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"INVALID_CMD","payload":"test"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: error message containing `[OpenBridge]`, mention of "not supported" or "Only PS".

### Test 5 — Process exit code (non-zero)
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"exit 7"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: error status, exit code 7, containing `EXIT_CODE_7`.

### Test 6 — Working directory respected
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"Set-Location D:\\projects; Get-Location"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains `D:\projects`.

### Test 7 — stderr capture
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"[Console]::Error.WriteLine('TEST_007_STDERR')"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: stderr contains `TEST_007_STDERR`.

### Test 8 — Write file (real side effect)
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"Write-Output 'TEST_008_FILE_OK' | Out-File -FilePath 'D:\\projects\\open-browser\\test-output.txt' -Encoding UTF8; Write-Output 'file written'"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains "file written". File `test-output.txt` created.

### Test 9 — Read the file back
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"Get-Content 'D:\\projects\\open-browser\\test-output.txt'"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains `TEST_008_FILE_OK`.

### Test 10 — Empty prompt rejection
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: error message containing "empty" or "PROMPT_EMPTY".

---

## After all 10 tests

Save the full report to: `D:\projects\open-browser\test-results.md`

Use this PowerShell command to write the file:

```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"@'\n# OpenBridge Infrastructure Test Results\n\nDate: ...\n\n| Test | Result | Details |\n|------|--------|----------|\n| 1 | PASS/FAIL | ... |\n...\n'@ | Out-File -FilePath 'D:\\projects\\open-browser\\test-results.md' -Encoding UTF8"}
@@OPENBRIDGE_EXEC_END@@
```

Fill in the actual results for each test in the table.
