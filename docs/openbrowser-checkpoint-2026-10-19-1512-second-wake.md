# Open Browser Lite: drugi cykl wznowienia i dalsze wdrożenie

Purpose:
Potwierdzić drugi realny cykl DevWake w właściwym czacie, a następnie zrealizować kolejne punkty 86-elementowego backlogu.

Depends on:
- docs/design/openbrowser-master-roadmap-v3-2026-10-19.md
- docs/openbrowser-checkpoint-2026-10-19-1504-resources.md
- docs/agent/READ_BEFORE_WORK_new.md

Impacts:
- docs/agent/current-state_new.md
- docs/design/openbrowser-master-roadmap-v3-2026-10-19.md
- new/source files documented in subsequent checkpoints

Update rule:
Dopisywać wyniki testów i wdrożenia w kolejnych sekcjach, nie zastępować ich założeniami.

Latest:
- 2026-10-19 15:12 CEST: second real DevWake verified; further tasks in progress.

## OB3-018: two real successful chat wake cycles

PBOX1, current v1.72.6.0 PID21332, Responding=True.
Encrypted app metadata, displayed only without secrets or full URL:
- Pinned correct chat suffix: b74f0fc1f389.
- First actual wake: 14:45:23 CEST; correct user message arrived in active conversation, followed by assistant project work.
- Second actual wake: 15:10:55 CEST; this user instruction is present in the SAME current conversation, and project work resumed.
- Interval between sends: 25 minutes and 32 seconds.
- Direct sender state: direct_submission_requested, AttemptsInWindow=2.
- Next app due: 15:35:55 CEST.
- No evidence of a second duplicate within either cycle.
- The application does not have server ACK in the simplified sender. User-visible receipt in two turns and response are stronger external end-to-end evidence than the internal submission_requested state.
- Completion scope: OB3-018 core two-cycle real-life handoff and follow-up work LIVE_VERIFIED; strict in-app ACK remains independent OB3-009/025 instrumentation item. This checkpoint does not claim a built-in server receipt detector.
- No manual git commit/push or ChatGPT scheduled reminders.

## New scope

Prioritize OB3-021: readable production technical identity (version/build time/source SHA/channel and WebView2 details) without content interception or sensitive credentials.

## 15:19 CEST: OB3-021 release and OB3-023 analysis

### LIVE v1.72.7
- SATANAMD SOURCE commit synchronized via existing Autosync: 671ed94dac64183307bd73cc99bed4ed5c0aec8e (no manual commit/push).
- New `LiteTechnicalInfoUi.cs`: operator button `Techniczne` displays embedded build version/time, release channel, source SHA, EXE SHA256, WebView2 runtime and local profile/install paths. Reads the *installed* manifest; missing/malformed manifest gets explicit UNKNOWN/MANIFEST_ERROR.
- SATANAMD: Release build 0 errors, old MSB3277 warning; sender 18 PASS, HighGuard 18 PASS, HighSlider 5 PASS, DevWake selftest exit0. STAGED publish + manifest verified + promotion gate PASS.
- PBOX1: independent publish 1.72.7.0, LIVE release manifest created and validated. EXE SHA256 628b3c802ef6171b6ff43ccf605c7bb2e0deb147dd09f584cfb0a961af7445c7, source SHA in live manifest 671ed94dac64183307bd73cc99bed4ed5c0aec8e, channel live.
- 15:15:20 controlled deployment with fallback 1.72.6 via existing recovery script, exit 0, PID20956 healthy 15:15:28.
- 15:15:41 UIAutomation verified the new `Techniczne` toolbar Button alongside `Autopraca: WŁ. 25m`, `Przypnij ten czat` and correct target/status. Encrypted next due 15:35:55 retained after deployment, no SendNow.
- 15:16:58 independently updated and verified Desktop + Start Menu launch shortcuts to 1.72.7.
- Automated modal-dialog click test did not yield a visible dialog in UIAutomation. Operator panel *button availability and manifest data* LIVE VERIFIED, actual message dialog text NOT YET verified. No claim of complete OB3-021 GUI E2E.

### OB3-023 memory trends (SOURCE+TEST)
- Updated `scripts/analyze-lite-resource-trends.ps1`: keeps raw cross-restart WebView2 RAM delta for compatibility but explicitly marks it as raw, and reports a separate stable-tail RAM delta only where a single host PID, identical scoped WebView2 PID set, and sample gaps <=300 seconds persist.
- Reports `stableHostSampleCount`, `stableWebviewSampleCount`, `stableWebviewDurationMinutes`, `stableWebviewPrivateDeltaMiB` or null, `hasWebviewChurnOrRestart`.
- Added `tests/test-lite-resource-trends.ps1`, 9 checks PASS on unchanged-PID fixture and WebView2 PID change. Both PowerShell files AST syntax 0 errors. Production sample verification pending.
- No changes to running browser, resource sampler or existing ChatGPT/Windows task schedules for the analyzer change.

## 15:26 CEST: 5m/1h/24h trend windows and local HTML dashboard

- `scripts/analyze-lite-resource-trends.ps1` now returns aggregated `windows.5m`, `windows.1h`, `windows.24h`: actual sample count/coverage, min free RAM, max host/WebView2 private MiB and max observed WebView2 CPU single-core percentage. The report exposes actual observed minutes separately from requested window minutes.
- Added `scripts/export-lite-resource-dashboard.ps1`: generates local static HTML with three time-window rows, resource status, timestamp ranges and a stable-PID RAM summary. UTF-8 file, no JavaScript, no external resources, no chat text/cookies/session URLs.
- Updated `tests/test-lite-resource-trends.ps1`: 16 PASS, including fixture with WebView2 PID churn, windows, HTML output/no active scripts. Agent documentation gate PASS.
- PBOX1 fetched Autosync snapshot 9d8266b and installed only local diagnostics scripts, not a new EXE. First dashboard run failed due to missing local `analyze-lite-resource-trends.ps1`; corrected by writing the synchronized script next to exporter, rerun PASS. No browser restart or task creation.
- PBOX1 live numeric report at 15:22:58 using 404 samples: window5m 3 samples/4min minFree 2435MiB peakWeb 1068.4MiB; window1h 33 samples/58min peakWeb 1068.4MiB; window24h 404 samples/800min peakWeb 1103.7MiB. Stable-tail last 3 WebView2 samples had -305.2MiB private RAM, process churn detected. 24h window has only 800m coverage, NOT 24h.
- At 15:25:48, local static report created and validated at `%LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\diagnostics\resources-dashboard.html`, 1993 bytes, all three windows present, no active scripts.
- OB3-029 PARTIAL: local machine-readable/offline dashboard exists, but no inside-app navigation or event timelines yet. OB3-023 PARTIAL: 5m/1h/24h summaries exist, but sustained monitoring/peak commit accuracy still under development.
- No manual commit/push, no ChatGPT Tasks. Existing blocked Windows startup task OB3-081 was not touched.

## 15:31 CEST: OB3-029 event timeline and automatic local refresh

- Enhanced `scripts/export-lite-resource-dashboard.ps1` to read a bounded tail of today's `lite-events-YYYY-MM-DD.jsonl`, extract only timestamp, controlled event name and PID for up to 25 relevant events; explicitly excludes event details/contents. Event names include wake, renderer failures, selected High-mode issues and timeouts. Malformed JSON and invalid timestamps skipped.
- `scripts/monitor-lite-resources.ps1` now calls the exporter after writing a successful sample, if exporter exists; the refresh is deliberately noncritical, and its failure cannot stop the resource-monitor task.
- Test fixture checks event inclusion and deliberate exclusion of a fake private details field, plus resource windows and PID churn. `tests/test-lite-resource-trends.ps1`: 18 PASS. Four PowerShell files AST parse errors: 0.
- On PBOX1 after Autosync snapshot 60fb43e, backed up and atomically updated existing LOCAL diagnostic scripts: monitor, analyzer, exporter. Did NOT update or create Windows Tasks, only script content used by the already-existing Resource Monitor Task.
- 15:31:08 PBOX1 direct monitor test: resource sample appended and dashboard refreshed to 4060 bytes; `Wazne zdarzenia diagnostyczne` visible in generated HTML, `dev_wake_` events present; no active script tag, no external resources. All scripts live only in local diagnostics, no browser restart.
- OB3-029 upgraded but PARTIAL: static local dashboard auto-refreshes from existing Task, no built-in Open Browser GUI integration, no sophisticated cross-day event analytics.
