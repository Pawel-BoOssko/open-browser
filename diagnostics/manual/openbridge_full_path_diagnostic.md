# OpenBridge Full Path Diagnostic Report

Date: 2026-05-16
Log analyzed: `run_20260516_181032_685.ndjson` (2.9 MB, 2341 lines)

## 1. Latest log

- Path: `releases/BridgeBrowserAlpha0/logs/run_20260516_181032_685.ndjson`
- Size: 2,940,122 bytes (2.8 MB)
- Lines: 2341

## 2. Event counts

| Event | Count |
|---|---|
| cdp_request | 548 |
| cdp_response_body | 548 |
| cdp_loading_finished | 548 |
| page_fetch_start | 106 |
| **page_fetch_done** | **0** |
| **page_xhr_done** | **0** |
| page_websocket_message | 9 |
| cdp_websocket_frame | 16 |
| openbridge_envelope_detected | 0 |
| openbridge_envelope_parse_error | 0 |
| runtime_approval | 0 |
| pending_created | 0 |
| map_failed | 0 |
| host_execution_started | 0 |
| extraction_final | 1 (at app exit) |

## 3. Marker location

| Occurrence | Location | Classification |
|---|---|---|
| Seq 2066 | cdp_request postData to /backend-api/f/conversation | **Outgoing user request** — correctly ignored |
| (none found in response) | — | **Incoming assistant response** — MISSING from all response bodies |

The `@@OPENBRIDGE_EXEC_BEGIN@@` marker appears ONLY in the outgoing user request (the prompt sent TO ChatGPT). It does NOT appear in any incoming response body captured by CDP or PageTap.

## 4. /backend-api/f/conversation request/response

- **Request ID**: 29360.697
- **Method**: POST
- **postData**: Contains user prompt with `@@OPENBRIDGE_EXEC_BEGIN@@` marker — this is the user ASKING ChatGPT to output the marker block
- **cdp_loading_finished**: Exists (seq 2289)
- **cdp_response_body**: Exists, body length 24,319 chars (truncated in log), contains SSE with system/user-role messages, NOT assistant messages in the visible prefix
- **SSE content**: 275 SSE records in the extractor, but ALL system/user role messages (`"is_visually_hidden_from_conversation": true`). Assistant response with marker text would be later in the stream but was truncated in log AND was blocked by the `_sawPageStream` gate.

## 5. SSE event analysis

From the /backend-api/f/conversation response body prefix:
- `event: delta_encoding` — SSE v1 encoding
- `event: delta` — multiple delta events with `v: {message: {author: {role: "system"}, ...}}` (hidden system messages)
- `event: delta` — user-editable-context message with user instructions
- `status: "finished_successfully"` — present on system/user messages but these have `end_turn: true` and `is_visually_hidden_from_conversation: true`
- The assistant response content was not visible in the truncated log prefix

## 6. Exact break point — TABLE

| Layer | Expected | Observed | Status |
|---|---|---|---|
| ChatGPT visual output | `@@OPENBRIDGE_EXEC_BEGIN@@...` | Visible on screen | OK |
| CDP request capture | User prompt logged | Correctly logged as outgoing | OK |
| CDP response_body capture | SSE body captured | Body logged (24KB) | OK |
| ResponseExtractor receives CDP body | SSE text extracted | **BLOCKED by `_sawPageStream` gate** | **FAIL** |
| CDP body double-nested unwrap | Inner SSE text extracted | **Only one level deep** | **FAIL** |
| SSE parsing | SSE records to deltas | 275 records, 0 payloads | **FAIL** |
| page_fetch_done trigger | Fires on fetch completion | **0 events — PageTap only catches WS** | **MISSING** |
| Extract assistant frames | Messages with role=assistant | 0 frames (only system/user roles) | FAIL |
| ResponseExtractor.Finish() | Called on completion | Only at app exit | FAIL |
| Observer called | Parse result from answer text | Never called | FAIL |
| Parser acceptance | @@ markers detected | N/A — no text to parse | N/A |
| Mapper acceptance | CC command mapped | N/A | N/A |
| Runtime approval pending | Pending command created | 0 | MISSING |
| UI panel shown | Panel visible | Not shown | MISSING |

## Root cause summary

**Two bugs in ResponseExtractor**:

1. **`_sawPageStream` gate blocks CDP conversation data**: When PageTap websocket messages arrive first, `_sawPageStream` is set to true. Then ALL CDP response_body events (which contain the actual assistant SSE from `/backend-api/f/conversation`) are silently dropped by the gate check `if (!isPageStream && _sawPageStream) return;`.

2. **CDP body not fully unwrapped**: The CDP response body has a double-nested JSON structure (`{"body": "{\"body\": \"SSE text\"}"}`). `TryExtractCdpBody` only extracted one level, leaving the inner JSON unparsed.

**Combined effect**: The ResponseExtractor never saw any assistant text from the /backend-api/f/conversation SSE response. The extractor only processed websocket system messages (role=system, role=user), producing zero assistant frames. Finish() was never called during the conversation (only at app exit). The observer/parser/mapper/approval chain never fired.

## Fixes applied

1. **Bypass `_sawPageStream` gate for CDP conversation response bodies**: CDP `cdp_response_body` events containing "conversation" in the URL are now processed regardless of `_sawPageStream` state.

2. **Recursive CDP body unwrapping**: `ProcessRaw` now loops up to 5 levels deep through `TryExtractCdpBody` to fully unwrap nested CDP body JSON and reach the inner SSE text.

3. **WebSocket completion detection** (from previous commit): `WebViewMessageHandler` now calls `Finish()` when a `page_websocket_message` contains `"status":"finished_successfully"` with `"role":"assistant"`, providing a second completion trigger path.
