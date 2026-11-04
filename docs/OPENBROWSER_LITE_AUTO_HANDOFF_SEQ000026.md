# Open Browser Lite — immutable successor handoff SEQ 000026
This is a bounded, verified checkpoint artifact. It does not cause any send
or give another chat permission to act as a lead.

## Identity
Only active machine SATANAMD (LC MultiHost target=satamad).
Current project D:\projects\open-browser.
One active chat lead: https://chatgpt.com/c/6adf463c-4b80-83eb-9a4c-777a89f74068.
Native Open Browser Lite wake engine. No ChatGPT Tasks, PBOX1, manual Git
commit/push/reset. One active lead until verified ACK and atomic handoff.

## Current implementation, acceptance
Canonical docs/OBL_BACKLOG.md, P0-08: rotator and successor, three actual
E2E successes/crash/uncertain tests plus new checkpoint SHA and ACK.
P0-09: genuine WebView2 preflight, no false positive HTTP/Responding.
Other priorities: DOM newest 10, retain 12 local reply turns, short active
checkpoint, watchdog, rollback of every deployment, periodic cleanup.
Read docs/OPENBROWSER_LITE_CURRENT_STATUS.md, docs/OBL_CONTEXT.md,
docs/OPENBROWSER_LITE_CHECKPOINT.md, AGENTS.md, and OPERATING_RULES_2026-10-25.md.
Older roadmaps are not authoritative.

## Immutable lineage, no duplicate sends
Pinned legacy docs/OPENBROWSER_LITE_AUTO_HANDOFF_CURRENT.md SHA256
C7FECBA1DF9BE0DF981FBAFFC852EB2918283CFBCC28F28D8E0C6AF06F975C57.
Original ledger 5b9a5cd1cf6cd9fb_ledger.json is SeedUncertain rev9,
old receipt consumed: no retry, never edit.
Prior newer snapshot docs/OPENBROWSER_LITE_AUTO_HANDOFF_SEQ000021.md SHA256
A5F78DBE26B50F4CF01A81FF98D282190668E20B4DDFED6AC6F9DA51BC6C1858.
Its seq21 ledger is SeedAcked rev3 but only optimistic local-chatgpt
route; read-only revisit redirected to homepage, no confirmed server UUID,
no assistant exact reply. Its message/receipt is spent: do not retry.
Its request marker remains claimed and predecessor pointer must be archived.

## Target for this new generation
New immutable proof+Prepared ledger under seq000026, new distinct ownerEpoch,
new exact file SHA/response fingerprint, versioned immutable predecessor
pointer and atomically replaced active selection pointer.
No new message unless an explicit separately gated ONE-SHOT test is authorized
after rechecking fresh WebView2 blank composer, ledger, receipt and user bubble.
For future first seed: require persistent server /c/{UUID}, exact assistant
role response and two stable user-bubble SHA observations before SeedAcked.
Any uncertain result: never resend and keep existing lead.

## Operating contract
Before modifying deployment: one PID and genuine preflight, record
generating/draft/toolBusy. Owner's latest operating rule permits forced
deployment even if generating, with checkpoint, rollback and GUI verification.
Do not promote any chat without server-persisted UUID, successor checkpoint,
and single-lead ownership fencing. Do not overwrite any pinned snapshot or
rewrite old ledgers, and do not fabricate ACK.