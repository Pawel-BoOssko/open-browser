# Open Browser Test Plan v3

Run all tests in order. Record pass/fail. Save results to `D:\projects\open-browser\test-results-v3.md`.

## Prompt for model

> Read `D:\projects\open-browser\test-plan-v3.md` via `Get-Content -Encoding UTF8`. Execute all tests. Save results to `test-results-v3.md`. IMPORTANT: always use `-Encoding UTF8` when reading or writing files.

## Tests

### S1 — HST_STATUS with Your ID
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"HST_STATUS"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains `Your ID:`, `Build`, `Working directory`, `Tools:`, `Git status`.

### S2 — HST_TOOLS with new tools
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"HST_TOOLS"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: lists `claude`, `github`, `linkedin`, `n8n`, `web`.

### U1 — PS Polish characters (UTF-8 fix)
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"Write-Output 'Zażółć gęślą jaźń — ąćęłńóśźż ĄĆĘŁŃÓŚŹŻ'"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains `Zażółć gęślą jaźń` exactly, no mojibake.

### U2 — PY Polish characters
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PY","payload":"print('Zażółć gęślą jaźń — ąćęłńóśźż ĄĆĘŁŃÓŚŹŻ')"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains `Zażółć gęślą jaźń` exactly.

### P1 — PY basic
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PY","payload":"import sys; print(f'Python {sys.version}')"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains `Python 3.`.

### P2 — PY compute
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PY","payload":"print(sum(range(100)))"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains `4950`.

### W1 — web_fetch (read a page)
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"python tools\\web\\web_fetch.py https://example.com h1 2>&1"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains "Example Domain" or HTTP 200.

### W2 — Check web tools available
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"Get-ChildItem tools\\web\\*.py | Select-Object Name"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: 3 files listed: web_fetch.py, web_form.py, web_browser.py.

### R1 — RAW block inside payload
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PY","payload":"@@OPENBRIDGE_RAW_BEGIN@@
print('RAW_V3_TEST_OK')
@@OPENBRIDGE_RAW_END@@"
}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains `RAW_V3_TEST_OK`.

### N1 — n8n list workflows
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"python tools\\n8n\\list_workflows.py 2>&1"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: workflow list printed, no HTTP errors.

### G1 — GitHub whoami
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"python tools\\github\\github_whoami.py 2>&1"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: `GITHUB_USER_OK`.

### T1 — Countdown timer visible
Look at the status bar during test execution. 
Expected: "Response in Xs..." counts down, then "Sending...".

---

## After all 13 tests

Save results to: `D:\projects\open-browser\test-results-v3.md`

```
# Test Results v3

Date: ...

| Test | Result | Details |
|------|--------|---------|
| S1 | PASS/FAIL | ... |
| S2 | PASS/FAIL | ... |
| U1 | PASS/FAIL | ... |
| U2 | PASS/FAIL | ... |
| P1 | PASS/FAIL | ... |
| P2 | PASS/FAIL | ... |
| W1 | PASS/FAIL | ... |
| W2 | PASS/FAIL | ... |
| R1 | PASS/FAIL | ... |
| N1 | PASS/FAIL | ... |
| G1 | PASS/FAIL | ... |
| T1 | PASS/FAIL | ... |

Summary: X PASS, Y FAIL.
```
