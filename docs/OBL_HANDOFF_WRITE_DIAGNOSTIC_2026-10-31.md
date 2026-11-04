# OBL HANDOFF WRITE DIAGNOSTIC | 2026-10-31 SATANAMD
Scope: scratch-only write tests, existing HANDOFF read-only.
Probe path: diagnostics\handoff-io-probe-20261031
Test1 Set-Content UTF8 Polish Unicode PASS, 59 bytes, U+FFFD absent.
Test2 File.WriteAllText 1500 lines PASS, exact UTF8 readback, 21025 bytes.
Test3 Move-Item -Force scratch overwrite PASS, target exact, temp absent.
Test4 File.Replace using fully qualified scratch paths PASS, destination current, backup previous.
Existing original HANDOFF readback PASS: 19426 bytes; OBL_HANDOFF_SNAPSHOT_V2, wake219->220, saved 2026-10-31T18:06:36Z.
Earlier File.Replace exception: Sciezka ma niedozwolony format. Not reproduced on verified paths. Failed original input arguments unknown. No claim of root cause.
Tool-security refusals are independent from filesystem API results. No forbidden request retried via alternate route.
Journal audit 160..222: 63 IDs, 59 visible ACK, 2 receipt_resolved, 1 chat_started/chat_finished-only (210), 1 intentionally unsent 219; no duplicate reservation/send/ACK.
No LIVE modification, deployment or restart. 24h/7d and 229->230 remain open.
