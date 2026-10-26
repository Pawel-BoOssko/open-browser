# Open Browser Lite: CURRENT STATUS
Updated: 2026-10-26 14:08 Europe/Warsaw
Host: SATANAMD ONLY. LC MultiHost target=satamad. PBOX1 excluded.
LIVE: v1.72.96, PID 67152, one responsive interactive WebView2 instance (not yet restarted).
SOURCE/STAGED/QUEUED: v1.72.97, SHA256 1b31e574eb3f1b36a25e0e20116863747a8022d752f4fb1158a68a4878c38124.
Candidate path: D:\Apps\OpenBrowserLite\releases\v1.72.97_20261026\BridgeBrowserAlpha0.exe
Release manager: queued v1.72.97, previous immutable release v1.72.96. Stale release PID was reconciled by matching SHA and a fresh WebView2 heartbeat.
Tests: dotnet publish 0 errors (preexisting WindowsBase conflict warning), JS syntax PASS, PS parser PASS, manager selftest 18, 7/7 staged EXE CLI tests EXIT0. Interactive WebView2 candidate E2E not yet verified.
New lead: https://chatgpt.com/c/6adf463c-4b80-83eb-9a4c-777a89f74068. Scheduled every 15 minutes, Enabled=true; old lead ...fff6ef7fcdfd archived and wake Enabled=false. First NEW lead engine visible_ack still pending verification.
Deployment watcher: Windows task OBL Promote 17297 On Idle, 1 minute intervals (up to 2h), interactive session1. Requires actual UIA stop-button absent, empty editor, no recent generation, two stable idle observations; normal UI close only. First run deferred: generation_stop_visible. No forced kill.
DOM: recent real LIVE status <=10 turns with safe hard prune; renderer resources still monitored.
Next: after normal close and promotion, verify single LIVE 1.72.97, live-preflight.json, UI, DOM and first new-lead wake/ACK. Rollback package v1.72.96 retained.
