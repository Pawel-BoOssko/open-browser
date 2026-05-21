<!-- LEGACY_DOCUMENT_NOTICE -->
> Legacy document notice: this is an older documentation file. Its current operational content has been migrated or superseded by: docs/agent/current-state_new.md and docs/feedback/field-notes_new.md.
>
> Before using this file as source of truth, read: docs/agent/READ_BEFORE_WORK_new.md.
>
> Treat this file as historical or secondary unless the new documentation explicitly points back to it as an active operational reference.
<!-- /LEGACY_DOCUMENT_NOTICE -->

# Open Browser Test Plan v4

Run all tests in order. Save results to `test-results-v4.md`.

## Prompt for model

> Read `D:\projects\open-browser\test-plan-v4.md` via `Get-Content -Encoding UTF8`. Execute all tests. Save results to `test-results-v4.md`.

## Tests

### S1 — HST_STATUS with time and downloads
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"HST_STATUS"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: `Time:` field present, `Your ID:` present.

### U1 — PS Polish characters (UTF-8 fix)
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"Write-Output 'Zażółć gęślą jaźń'"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains `Zażółć gęślą jaźń` exactly, no replacement chars.

### U2 — PY Polish characters
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PY","payload":"import sys; print('Zażółć gęślą jaźń')"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: output contains `Zażółć gęślą jaźń` exactly.

### F1 — [PASSED LIVE TEST] Create sandbox file with loop command
Live ChatGPT sandbox download verified end-to-end on one real generated file.
Ask me to create a file in the sandbox. Tell me: "Create a small text file in the sandbox with a short Polish poem. Include a PS command in the same response to keep the loop alive."

```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"Write-Output 'waiting_for_file'"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: sandbox download link (button) appears, file downloads automatically.

### F2 — [PASSED LIVE TEST] Verify file in HST_STATUS
Live ChatGPT sandbox download verified end-to-end on one real generated file.
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"HST_STATUS"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: `Recent downloads:` lists the file from F1.

### F3 — [PASSED LIVE TEST] Verify file content
Live ChatGPT sandbox download verified end-to-end on one real generated file.
```
@@OPENBRIDGE_EXEC_BEGIN@@
{"version":"001","command":"PS","payload":"Get-Content downloads\\*.txt -Encoding UTF8 | Select-Object -First 5"}
@@OPENBRIDGE_EXEC_END@@
```
Expected: content of the sandbox file visible.

---

## After all tests

Save results to `D:\projects\open-browser\test-results-v4.md`:

```
# Test Results v4

Date: ...

| Test | Result | Details |
|------|--------|---------|
| S1 | PASS/FAIL | ... |
| U1 | PASS/FAIL | ... |
| U2 | PASS/FAIL | ... |
| F1 | PASS/FAIL | ... |
| F2 | PASS/FAIL | ... |
| F3 | PASS/FAIL | ... |

Summary: X PASS, Y FAIL.
```
