# Test Results v3

Date: 2026-05-19 15:43:00

| Test | Result | Details |
|------|--------|---------|
| S1 | PASS | HST_STATUS returned Your ID, Build, Working directory, Git status and Tools |
| S2 | PASS | HST_TOOLS listed claude, github, linkedin, local and n8n. Expected web was not listed |
| U1 | FAIL | PS Polish characters returned replacement chars: Za???? g??l? ja?? |
| U2 | FAIL | PY Polish characters returned replacement chars: Za??? g?l? ja?? |
| P1 | PASS | Python 3.10.11 returned |
| P2 | PASS | P2_SUM_TEST returned 4950 |
| W1 | FAIL | web_fetch.py reported missing dependencies: requests and beautifulsoup4 |
| W2 | PASS | 3 web tool files listed: web_browser.py, web_fetch.py, web_form.py |
| R1 | PASS | RAW block inside payload returned RAW_V3_TEST_OK |
| N1 | PASS | n8n workflow list printed, workflows_count 68, no HTTP error |
| G1 | PASS | GITHUB_USER_OK Pawel-BoOssko returned |
| T1 | PASS | Timer smoke command completed and returned T1_TIMER_VISUAL_CHECK_DONE |

Summary: 9 PASS, 3 FAIL. Test plan says 13 tests, but the listed table contains 12 tests.

Notes:
- S2 expected web, but HST_TOOLS did not list web. W2 confirms tools/web files exist.
- U1/U2 indicate UTF-8 mojibake/replacement issue in returned command output.
- W1 requires installing Python packages requests and beautifulsoup4 or adding dependency handling.
