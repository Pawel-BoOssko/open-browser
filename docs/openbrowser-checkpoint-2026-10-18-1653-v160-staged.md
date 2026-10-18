# Open Browser Lite v1.60.0 — checkpoint 2026-10-18 16:53 CEST

## Verified facts
- SATANAMD SOURCE D:\projects\open-browser: version 1.60.0 with timestamp embedded by SDK, displayed in titlebar and first toolbar label. Program SelfTest --lite-version-selftest EXIT=0.
- Files: LiteBuildInfo.cs, LiteResumeProbe.js, LiteResumeSender.js, LiteResumePolicy.cs, LiteResumeLedger.cs, LiteLunaAdvisor.cs, LiteResumeUi.cs and their selftests. The last four C# tests version/resume/luna/ledger EXIT=0 on SATANAMD.
- Latest SATANAMD Node: LiteResumeAgent 7 PASS, HardPrune 9 PASS, DomOptimizer 5 PASS, Snapshot 5 PASS, ResponseSignals 7 PASS => 33 PASS; documentation/folder gate PASS, scoped git diff --check 0.
- Autosync branch autosync/SATANAMD revision 21dae1d5e1d8; PBOX1 source clone pulled and clean before update.
- PBOX1 Release dotnet publish 0 errors (known MSB3277), folder %LOCALAPPDATA%\Programs\OpenBrowserChatGPTLite_v1.60.0_20261018.
- PBOX1 embedded version verified: 1.60.0+build.20261018T145117Z.21dae1d5e1d8..., i.e. Warsaw 18.10.2026 16:51:17 (+02:00).
- PBOX1 six selftests version/resume/luna/ledger/projects/archive all EXIT=0. All required resume JS files in executable folder from earlier verification.
- PBOX1 Desktop and Start shortcuts already point to v1.60.0; previous live PID 18676 from notes_dom_voice remains running. No forced closure, no automatic duplicate WebView in same user profile.
- Windows Scheduled Task 'OpenBrowser ChatGPT Lite' still points earlier telemetry exe (previous Set-ScheduledTask timed out, OB-10); do not claim autostart fixed.

## Resume behavior & constraints
- Opt-in Nadzór:WYŁ. toolbar, monitor only approved 'testowy' chat, High mode if DOM can verify, 3 stable observations across >=40s after generation/tool idle, no draft/error.
- Luna adapter uses OpenAI Responses structured JSON and high effort; requires OPENBROWSER_LUNA_MODEL (real API ID) plus OPENAI_API_KEY in Windows user environment. Both checked FALSE on PBOX1; no paid API call or final model test performed.
- Decision continue/wait/ask_user/stop; no invented tasks, bypassing safety barriers prohibited. Test scope: adventure book chat. DPAPI ledger persists idempotency and uncertain delivery state before DOM injection.
- Real ChatGPT UI E2E, acknowledgement under live React, long-running tool calls, model costs, real High setting and chat completion are NOT VERIFIED. Do not auto-enable project-wide resume yet.
- No browser process was restarted or upgraded in-place. User-created chat named 'testowy' is not yet confirmed/registered; after it exists, use controlled opt-in beta tests.

## Development
- Full 35-step plan: docs/design/openbrowser-full-delivery-plan-v2-2026-10-18-1626.md. Existing project rules READ_BEFORE_WORK_new.md and Autosync apply.
- Further priorities: model API credentials/alias via protected configuration, test chat E2E, full supervisor lifecycle + lease, stable native cards, safe production promotion/rollback, tooling failure log.
- Failures and resolved tests logged in docs/Rejestr nieudanych komend.md with unique names, including version regex, Luna test fixture, and missing model configuration.