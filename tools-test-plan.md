# Tools Test Plan

Run these tests from inside Open Browser. After each test, record pass/fail. Save results to `D:\projects\open-browser\tools-test-results.md`.

## Part 1: n8n tools

### Test N1 — List scripts
```
Get-ChildItem tools\n8n\*.py | Select-Object Name
```
Expected: 8 Python files listed.

### Test N2 — Read the n8n working guide
```
Get-Content tools\n8n\N8N_WORKING_GUIDE.md -First 10
```
Expected: guide content visible.

### Test N3 — List workflows
```
python tools\n8n\list_workflows.py
```
Expected: workflow list printed, no HTTP errors.

### Test N4 — HST_TOOLS shows n8n
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"HST_TOOLS"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: response includes `tools/n8n/` with script names.

## Part 2: GitHub tools

### Test G1 — List scripts
```
Get-ChildItem tools\github\*.py | Select-Object Name
```
Expected: 5 Python files listed.

### Test G2 — Read the GitHub working guide
```
Get-Content tools\github\GITHUB_WORKING_GUIDE.md -First 10
```
Expected: guide content visible.

### Test G3 — Check GitHub authentication
```
python tools\github\github_whoami.py
```
Expected: `GITHUB_USER_OK <username>`.

### Test G4 — HST_TOOLS shows github
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"HST_TOOLS"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: response includes `tools/github/` with script names.

## Part 3: Python executor

### Test P1 — Simple print
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PY","payload":"print('TOOLS_TEST_PY_OK')"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains `TOOLS_TEST_PY_OK`.

### Test P2 — Import and compute
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PY","payload":"import sys; print(f'Python {sys.version}')"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains `Python 3.`.

## Part 4: RAW block

### Test R1 — RAW block with Python (multi-line)
```
@@OPENBRIDGE_EXEC_BEGIN@@
{
  "version": "001",
  "command": "PY",
  "payload64": "@@OPENBRIDGE_RAW_BEGIN@@
import sys
print(f'Python {sys.version}')
print('RAW_TEST_OK')
@@OPENBRIDGE_RAW_END@@"
}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains `RAW_TEST_OK`.

### Test R2 — RAW block placed outside JSON (must fail)
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PY","payload64":"@@OPENBRIDGE_RAW_BEGIN@@print('OK')@@OPENBRIDGE_RAW_END@@"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains `RAW_TEST_OK` (RAW inside payload64 string — correct placement).

### Test R3 — RAW block outside JSON braces (must fail)
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS"}
@@OPENBRIDGE_RAW_BEGIN@@
Write-Output "SHOULD_NOT_RUN"
@@OPENBRIDGE_RAW_END@@
@@OPENBRIDGE_EXEC_END@@
```
Expected: error message — invalid JSON. RAW block placed outside JSON object.

---

## After all tests

Save results to: `D:\projects\open-browser\tools-test-results.md`

```
# Tools Test Results

Date: ...

| Test | Result | Details |
|------|--------|---------|
| N1 | PASS/FAIL | ... |
| N2 | PASS/FAIL | ... |
| N3 | PASS/FAIL | ... |
| N4 | PASS/FAIL | ... |
| G1 | PASS/FAIL | ... |
| G2 | PASS/FAIL | ... |
| G3 | PASS/FAIL | ... |
| G4 | PASS/FAIL | ... |
| P1 | PASS/FAIL | ... |
| P2 | PASS/FAIL | ... |

Summary: X PASS, Y FAIL.
```
