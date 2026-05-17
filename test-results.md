# OpenBridge Infrastructure Test Results

Date: 2026-05-17

| Test | Result | Details |
|------|--------|---------|
| 1 | PASS | Basic PS echo returned `TEST_001_OK`. |
| 2 | PASS | `Get-Location` returned `D:\projects\open-browser`. |
| 3 | PASS | `HST_HELP` returned help text containing `Open Bridge Runtime Environment`, `PS`, and `HST_HELP`. |
| 4 | PASS | Unsupported command returned `[OpenBridge] Command not supported: INVALID_CMD. Only PS and HST_HELP are accepted.` |
| 5 | FAIL | `exit 7` did not return the expected error feedback containing exit code 7 or `EXIT_CODE_7`; user reported the test failed. |
| 6 | PASS | Working directory test returned `D:\projects`. |
| 7 | PASS | stderr test returned `TEST_007_STDERR`. |
| 8 | PASS | File write test returned `file written`; `test-output.txt` was created. |
| 9 | PASS | File read test returned `TEST_008_FILE_OK`. |
| 10 | PASS | `payload64` decoded and executed correctly, returning `TEST_010_BASE64_OK`. |
| 11 | PASS | Combined `payload` and `payload64` returned both `TEST_011_PREFIX` and `TEST_011_BASE64_OK`. |
| 12 | PASS | Empty PS envelope returned `[OpenBridge] Prompt is empty. Provide a payload or payload64 in the envelope.` |

Summary: 11 PASS, 1 FAIL.
