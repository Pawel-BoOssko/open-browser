# Open Browser Lite: Direct Send production deployment
Version: 1.72.5
Date: 2026-10-19, 14:14 CEST

Purpose:
Restore actual message composition and submission from the embedded WebView2 without allowing DevWake probe/ACK machinery to prevent an attempt. This is a deliberately simplified transitional implementation requested by the owner.

Depends on:
- docs/OPENBROWSER_DEEP_HANDOFF_2026-10-19_1245_ChatGPT.md
- docs/openbrowser-checkpoint-2026-10-19-1316-highguard-scoped-busy.md
- docs/agent/READ_BEFORE_WORK_new.md

Impacts:
- src/BridgeBrowserAlpha0/LiteResumeSender.js
- src/BridgeBrowserAlpha0/LiteDevWakeUi.cs
- src/BridgeBrowserAlpha0/LiteDevWakeDirect.cs (new)
- src/BridgeBrowserAlpha0/Program.cs
- src/BridgeBrowserAlpha0/BridgeBrowserAlpha0.csproj
- tests/lite-resume-send-race.test.cjs
- docs/agent/current-state_new.md

Update rule:
Preserve historical deployment evidence and append subsequent actual results rather than conflating submission requested, click invoked, and server reply.

## Implemented
1. Legacy-inspired direct message insertion into the active ChatGPT editor. After 500ms, click form submit or supported Send selector; fall back to form.requestSubmit() or synthetic Enter. Existing detailed sender remains in source for optional later use.
2. DevWake now calls direct sender by default once per configured interval, checking only that browser URL matches the pinned target and that the timer is due. The old detailed Eligibility/High/role/idle/ACK branch is bypassed; set environment OPENBROWSER_LEGACY_DIRECT_SEND=0 to use it again.
3. New Program flag --lite-dev-wake-send-now sets the first timer due immediately for the saved enabled pinned chat. It does not expose the saved URL.
4. Test suite lite-resume-send-race.test.cjs extended from 16 to 18 PASS, including direct DIL form submission. PBOX1 source brought in from existing technical Autosync snapshot 6da63eadf72d801debec3f2a65a9d60f1528d68e. No manual git commit or push.

## SATANAMD validation
- dotnet build -c Release: success, 0 errors, known MSB3277 WindowsBase warning.
- dotnet publish -c Release candidate v1.72.5: success, file version 1.72.5.0. Latest SAT publish SHA256 D8729B88FF917190CFE424F84EC88D6DD7BAB0410323DD5C8BF091FD31430336. Builds differ by build timestamp between SAT and PBOX1.
- Node tests: LiteResumeSenderRace 18/18 PASS, LiteHighGuard 18/18 PASS, LiteHighSlider 5/5 PASS. Sender JS syntax PASS.

## PBOX1 deployment
- Original runtime 1.72.0 PID17840. Separate PBOX1 build worktree at %LOCALAPPDATA%\OpenBrowser\build-source-v1.72.5; existing main source checkout preserved.
- dotnet publish Release from Autosync snapshot 6da63ea into %LOCALAPPDATA%\Programs\OpenBrowserChatGPTLite_v1.72.5_20261019_1408: exit0. EXE version 1.72.5.0, SHA256 DA1AADE9B75E10C20EC3D1C8C016BDB1B7938638260380502C3D9FA57B9C3948. LiteResumeSender.js includes sendDirect.
- BEFORE terminating runtime, wrote and parsed C:\Users\pboru\Documents\deploy-openbrowser-safe-20261019.ps1. Candidate and fallback executable paths both verified. Script stops old PID, launches candidate, checks window/Responding within 45 sec, otherwise starts previous installed version 1.72.0. It records outcomes in C:\Users\pboru\Documents\openbrowser-deploy-recovery-20261019.log.
- 14:09:52 deployment invoked; stopped PID17840, launched candidate with --lite-open-safely --lite-dev-wake-send-now, confirmed 14:10:07 PID6840 healthy. Deployment script exit code 0.
- Independent 14:11:06 process check: 1.72.5.0 PID6840 Responding=True with main window.
- DPAPI configuration metadata-only verification without revealing target: Enabled=True, LastState=direct_submission_requested, LastAttemptUtc=2026-10-19 12:11:30.456414Z (14:11:30 CEST), NextDueUtc=12:36:30.456414Z (14:36:30 CEST).
- Read-only Windows UI Automation found 1 visible ControlType.Text instance containing the expected beginning of the wake message in the WebView2 chat UI at 14:12:32; this is evidence of a displayed message, not proof of a server-side reply. Do not call it backend ACK.
- PBOX1 Desktop and Start Menu 'Open Browser - ChatGPT Lite.lnk' updated from v1.72.0 to new v1.72.5, --lite-open-safely.
- Native ChatGPT automations remain OFF. Windows Task Scheduler legacy OB-10 not changed. Existing source snapshot releases are preserved.

## Limitations and next work
- Immediate goal achieved to SOURCE/DEPLOYED and visible-message level; no independent ChatGPT server delivery/assistant response verification. Direct sender does not wait for ACK and can duplicate or send at a poor time, as explicitly accepted by owner for this recovery phase.
- Old release manifest/gates for 1.72.4 remain historical, not reused. v1.72.5 was deployed via intentional fast path, without full WebView2 isolated release gate.
- Verify the resulting assistant reply in the current chat before hardening retries or high effort gating again. The next automatic due is 14:36:30 CEST if app stays running and pinned conversation remains selected.
- Fallback wrapper handles startup failures, not indefinite continuous supervision after wrapper exits.
- No manual git commit, git push, repository reset, or native ChatGPT scheduled task creation.
