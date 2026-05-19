# Test Results v4

Date: 2026-05-19 17:11:43

| Test | Result | Details |
|------|--------|---------|
| S1 | PASS | HST_STATUS returned Time, Your ID and Recent downloads section |
| U1 | FAIL | PS returned replacement chars: Za???? g??l? ja?? |
| U2 | PASS | PY returned Polish text correctly: Zażółć gęślą jaźń |
| P1 | PASS | Python 3.10.11 returned |
| P2 | PASS | 4950 returned |
| W1 | PASS | web_fetch.py returned Example Domain and saved last_fetch.html |
| W2 | PASS | 3 web tool files listed: web_browser.py, web_fetch.py, web_form.py |
| R1 | PASS | RAW block inside payload returned RAW_V3_TEST_OK |
| N1 | PASS | n8n workflow list printed, workflows_count 68, no HTTP error |
| G1 | PASS | GITHUB_USER_OK Pawel-BoOssko returned |
| F1 | FAIL | Sandbox file was created in ChatGPT sandbox and loop command returned waiting_for_file, but OpenBridge did not download it into project downloads |
| F2 | FAIL | HST_STATUS did not list the sandbox file in Recent downloads |
| F3 | FAIL | downloads directory missing, file content unavailable |

Summary: 9 PASS, 4 FAIL.

Notes:
- PS stdout still corrupts Polish characters. PY stdout works correctly.
- Web tools are now available and web_fetch.py works.
- Sandbox download pipeline did not place the generated file in D:\projects\open-browser\downloads.
