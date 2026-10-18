using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

public sealed class ResponseExtractor
{
    private sealed class AssistantMessageFrame
    {
        public required string MessageId { get; init; }
        public required string StartedAtUtc { get; init; }
        public string? EndedAtUtc { get; set; }
        public string Role { get; init; } = "assistant";
        public string Channel { get; init; } = "final";
        public string Status { get; set; } = "open";
        public string? CloseReason { get; set; }
        public StringBuilder Text { get; } = new();
        public int DeltaCount { get; set; }
        public int MessageObjectCount { get; set; }
        public int MarkerCount { get; set; }
        public bool SawLastToken { get; set; }
        public string? LastTextAtUtc { get; set; }
    }

    private readonly LogWriter _log;
    private readonly object _gate = new();
    private readonly StringBuilder _sseBuffer = new();
    private readonly List<AssistantMessageFrame> _frames = new();
    private readonly Dictionary<string, AssistantMessageFrame> _framesById = new(StringComparer.Ordinal);
    private readonly BridgeBrowserAlpha0.OpenBridgeProtocol.OpenBridgeEnvelopeObserver _observer;
    public Action<BridgeBrowserAlpha0.OpenBridgeProtocol.OpenBridgeEnvelopeParseResult>? OnEnvelopeDetected;
    private StreamWriter? _rawWriter;
    private string? _answerPath;
    private string? _messagesPath;
    private bool _sawPageStream;
    private int _rawEvents;
    private int _pageStreamEvents;
    private int _sseRecords;
    private int _ssePayloads;
    private int _jsonParseErrors;
    private int _textValuesWithoutMessageId;
    private int _textValuesIgnoredByPath;
    private int _lastTokenMarkers;
    private int _lastTokenForUnknownMessage;
    private int _emptyDeltaValues;
    private string? _currentAssistantMessageId;
    private string _currentEventTsUtc = DateTime.UtcNow.ToString("O");
    private volatile int _pendingFinishCount;
    private readonly HashSet<string> _observedMessageIds = new(StringComparer.Ordinal);

    public ResponseExtractor(LogWriter log)
    {
        _log = log;
        _observer = new BridgeBrowserAlpha0.OpenBridgeProtocol.OpenBridgeEnvelopeObserver(log);
    }

    public void StartRun()
    {
        lock (_gate)
        {
            _rawWriter?.Dispose();
            _sseBuffer.Clear();
            _frames.Clear();
            _framesById.Clear();
            _sawPageStream = false;
            _currentAssistantMessageId = null;
            _currentEventTsUtc = DateTime.UtcNow.ToString("O");
            _rawEvents = 0;
            _pageStreamEvents = 0;
            _sseRecords = 0;
            _ssePayloads = 0;
            _jsonParseErrors = 0;
            _textValuesWithoutMessageId = 0;
            _textValuesIgnoredByPath = 0;
            _lastTokenMarkers = 0;
            _lastTokenForUnknownMessage = 0;
            _emptyDeltaValues = 0;
            _answerPath = Path.Combine(AppPaths.Extracted, $"run_{_log.RunId}_answer.txt");
            _messagesPath = Path.Combine(AppPaths.Extracted, $"run_{_log.RunId}_messages.ndjson");
            var pendingPath = Path.Combine(AppPaths.Extracted, $"run_{_log.RunId}_pending.ndjson");
            _rawWriter = new StreamWriter(new FileStream(pendingPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8) { AutoFlush = true };
            _log.WriteRun("extractor", "extraction_update", "started", "Extractor initialized", new
            {
                answerPath = _answerPath,
                rawPath = "per-message",
                messagesPath = _messagesPath,
                mode = "message_id_framed_sse_with_diagnostics_v_alpha06"
            });
        }
    }

    public void AddRaw(string source, string eventType, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;
        lock (_gate)
        {
            if (_rawWriter == null) return;

            _currentEventTsUtc = DateTime.UtcNow.ToString("O");
            _rawEvents++;
            var record = JsonSerializer.Serialize(new { tsUtc = _currentEventTsUtc, source, eventType, raw = Redactor.RedactString(raw) });
            _rawWriter.WriteLine(record);

            var isPageStream = source.Equals("page_tap", StringComparison.OrdinalIgnoreCase)
                               && (eventType.Contains("fetch", StringComparison.OrdinalIgnoreCase)
                                   || eventType.Contains("xhr", StringComparison.OrdinalIgnoreCase)
                                   || eventType.Contains("websocket", StringComparison.OrdinalIgnoreCase)
                                   || eventType.Contains("eventsource", StringComparison.OrdinalIgnoreCase));

            var isCdpConversationBody = source.Equals("cdp", StringComparison.OrdinalIgnoreCase)
                                         && eventType == "cdp_response_body"
                                         && raw.Contains("conversation", StringComparison.OrdinalIgnoreCase);

            var isCdpWebSocketFrame = source.Equals("cdp", StringComparison.OrdinalIgnoreCase)
                                      && eventType == "cdp_websocket_frame_received";

            var wasSawPageStreamBefore = _sawPageStream;

            if (isPageStream)
            {
                _sawPageStream = true;
                _pageStreamEvents++;
            }

            _log.WriteRun("extractor_trace", "add_raw_routing", "ok", 
                $"Routing check: source={source}, eventType={eventType}, length={raw.Length}, isPageStream={isPageStream}, isCdpConversationBody={isCdpConversationBody}, isCdpWebSocketFrame={isCdpWebSocketFrame}, sawPageStreamBefore={wasSawPageStreamBefore}, sawPageStreamAfter={_sawPageStream}",
                new { 
                    source, 
                    eventType, 
                    length = raw.Length, 
                    isPageStream, 
                    isCdpConversationBody, 
                    isCdpWebSocketFrame,
                    sawPageStreamBefore = wasSawPageStreamBefore, 
                    sawPageStreamAfter = _sawPageStream,
                    sample = raw.Length > 200 ? raw.Substring(0, 200) + "... [TRUNCATED]" : raw
                });

            if (!isPageStream && !isCdpConversationBody && !isCdpWebSocketFrame && _sawPageStream)
            {
                _log.WriteRun("extractor_trace", "add_raw_ignored", "ignored", 
                    $"Extractor IGNORED raw chunk because _sawPageStream=True. Source: {source}, Event: {eventType}",
                    new { source, eventType, length = raw.Length });
                return;
            }

            var before = GetCurrentAnswerText();
            ProcessRaw(raw, flushTail: false);
            var after = GetCurrentAnswerText();
            if (!string.Equals(before, after, StringComparison.Ordinal) && _answerPath != null)
            {
                PipelineRawDump.Write("03_ResponseExtractor.txt", after);
                File.WriteAllText(_answerPath, after, new UTF8Encoding(false));
                _log.WriteRun("extractor", "extraction_update", "partial", "Assistant message frames updated", new
                {
                    chars = after.Length,
                    frames = _frames.Count,
                    currentMessageId = _currentAssistantMessageId,
                    answerPath = _answerPath
                });
            }
        }
        if (_pendingFinishCount > 0)
        {
            var count = Interlocked.Exchange(ref _pendingFinishCount, 0);
            for (int i = 0; i < count; i++)
                Finish();
        }
    }

    public void Finish()
    {
        lock (_gate)
        {
            _currentEventTsUtc = DateTime.UtcNow.ToString("O");
            ProcessRaw("", flushTail: true);
            CloseOpenFrames("page_fetch_done_or_finish");

            var answer = GetCurrentAnswerText();
            AppConstants.LastAssistantResponseText = answer;
            PipelineRawDump.Write("04_ResponseExtractor_Finish.txt", answer);

            var hasNewFrames = false;
            var newFrames = new List<AssistantMessageFrame>();
            var duplicateFrames = new List<string>();
            foreach (var f in _frames)
            {
                if (f.Text.Length > 0)
                {
                    if (_observedMessageIds.Add(f.MessageId))
                    {
                        hasNewFrames = true;
                        newFrames.Add(f);
                    }
                    else
                    {
                        duplicateFrames.Add(f.MessageId);
                    }
                }
            }

            // TELEMETRY: Log frame observation results
            _log.WriteRun("extractor_trace", "finish_frame_observation", "ok",
                $"Finish frame scan: totalFrames={_frames.Count}, newFrames={newFrames.Count}, duplicateFrames={duplicateFrames.Count}, totalObserved={_observedMessageIds.Count}",
                new { totalFrames = _frames.Count, newFrameIds = newFrames.Select(f => f.MessageId).ToArray(), duplicateFrameIds = duplicateFrames.ToArray(), totalObserved = _observedMessageIds.Count });

            if (hasNewFrames)
            {
                AppConstants.LastAssistantMessageId = newFrames[^1].MessageId;
                var newAnswer = GetCurrentAnswerTextForFrames(newFrames);

                // TELEMETRY: Log full text going to Observer
                _log.WriteRun("extractor_trace", "finish_observer_input", "ok",
                    $"Sending to Observer: newFrameCount={newFrames.Count}, answerLength={newAnswer.Length}",
                    new { newFrameCount = newFrames.Count, answerLength = newAnswer.Length,
                           answerSample = newAnswer.Length > 1000 ? newAnswer.Substring(0, 1000) + "... [TRUNCATED]" : newAnswer,
                           containsExecBegin = newAnswer.Contains("@@OPENBRIDGE_EXEC_BEGIN@@"),
                           containsExecEnd = newAnswer.Contains("@@OPENBRIDGE_EXEC_END@@") });

                var parseResult = _observer.Observe(newAnswer);

                // TELEMETRY: Log observer result
                _log.WriteRun("extractor_trace", "finish_observer_result", "ok",
                    $"Observer result: hasEnvelope={parseResult?.HasEnvelope}, error={parseResult?.Error}",
                    new { hasEnvelope = parseResult?.HasEnvelope, error = parseResult?.Error.ToString() });

                if (parseResult?.HasEnvelope == true)
                {
                    OnEnvelopeDetected?.Invoke(parseResult);
                }
            }
            else
            {
                _log.WriteRun("extractor_trace", "finish_no_new_frames", "ok",
                    $"No new frames to observe. Total frames={_frames.Count}, already observed={_observedMessageIds.Count}");
            }

            var status = answer.Length > 0 ? "ok" : "extraction_failed";
            if (answer.Length > 0 && _answerPath != null)
                File.WriteAllText(_answerPath, answer, new UTF8Encoding(false));

            WriteMessagesNdjson();

            var diagnostics = BuildDiagnostics();
            foreach (var warning in diagnostics.Warnings)
                _log.WriteRun("extractor", "parser_warning", "warning", warning.message, warning.data);

            _log.WriteRun("extractor", "extraction_final", status, answer.Length > 0 ? "Assistant message frames extracted" : "No assistant final text recognized; inspect raw NDJSON", new
            {
                chars = answer.Length,
                answerPath = _answerPath,
                rawPath = "per-message",
                messagesPath = _messagesPath,
                summary = diagnostics.Summary,
                frames = _frames.Select(f => new
                {
                    messageId = f.MessageId,
                    f.StartedAtUtc,
                    f.EndedAtUtc,
                    f.Role,
                    f.Channel,
                    chars = f.Text.Length,
                    f.DeltaCount,
                    f.MessageObjectCount,
                    f.MarkerCount,
                    f.SawLastToken,
                    f.Status,
                    f.CloseReason
                }).ToArray(),
                bufferedTailChars = _sseBuffer.Length
            });
        }
    }

    private void ProcessRaw(string raw, bool flushTail)
    {
        _log.WriteRun("extractor_trace", "process_raw_start", "ok", 
            $"ProcessRaw started: rawLength={raw.Length}, flushTail={flushTail}",
            new { rawLength = raw.Length, flushTail });

        for (int depth = 0; depth < 5; depth++)
        {
            var cdpBody = TryExtractCdpBody(raw);
            if (cdpBody == null) break;
            _log.WriteRun("extractor_trace", "process_raw_cdp_unwrap", "ok", 
                $"Unwrapped CDP body at depth {depth}: newLength={cdpBody.Length}",
                new { depth, newLength = cdpBody.Length });
            raw = cdpBody;
        }

        var looksLikeSse = raw.Contains("data:", StringComparison.OrdinalIgnoreCase)
                           || raw.Contains("event:", StringComparison.OrdinalIgnoreCase)
                           || _sseBuffer.Length > 0;

        _log.WriteRun("extractor_trace", "process_raw_classification", "ok", 
            $"Classification: looksLikeSse={looksLikeSse}, rawContainsData={raw.Contains("data:")}, sseBufferLength={_sseBuffer.Length}",
            new { looksLikeSse, sseBufferLength = _sseBuffer.Length });

        if (looksLikeSse)
        {
            if (raw.Length > 0) _sseBuffer.Append(raw);
            ProcessSseBuffer(flushTail);
            return;
        }

        var trimmed = raw.Trim();
        if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
        {
            _log.WriteRun("extractor_trace", "process_raw_direct_json", "ok", "Raw payload looks like direct JSON, processing...", new { length = trimmed.Length });
            ProcessJsonPayload(trimmed, null);
        }
        else
        {
            _log.WriteRun("extractor_trace", "process_raw_ignored_non_json", "ignored", 
                $"Raw chunk not SSE and does not start with JSON object or array notation. Sample: {(trimmed.Length > 100 ? trimmed.Substring(0, 100) : trimmed)}");
        }
    }

    private void ProcessSseBuffer(bool flushTail)
    {
        if (_sseBuffer.Length == 0) return;

        _log.WriteRun("extractor_trace", "process_sse_buffer_start", "ok", 
            $"ProcessSseBuffer: bufferLength={_sseBuffer.Length}, flushTail={flushTail}",
            new { bufferLength = _sseBuffer.Length, flushTail });

        var text = _sseBuffer.ToString().Replace("\r\n", "\n").Replace('\r', '\n');
        var consumed = 0;

        while (true)
        {
            var idx = text.IndexOf("\n\n", consumed, StringComparison.Ordinal);
            if (idx < 0) break;
            var record = text.Substring(consumed, idx - consumed);
            consumed = idx + 2;

            _log.WriteRun("extractor_trace", "process_sse_buffer_split", "ok", 
                $"Split SSE record found: length={record.Length}, consumedTotal={consumed}",
                new { recordLength = record.Length, consumed });

            ProcessSseRecord(record);
        }

        var tail = text.Substring(consumed);
        _log.WriteRun("extractor_trace", "process_sse_buffer_end", "ok", 
            $"Buffer processing complete: consumed={consumed}, remainingTail={tail.Length}",
            new { consumed, tailLength = tail.Length });

        if (flushTail && !string.IsNullOrWhiteSpace(tail))
        {
            _log.WriteRun("extractor_trace", "process_sse_buffer_flush_tail", "ok", 
                $"Flushing trailing SSE buffer record: length={tail.Length}",
                new { tailLength = tail.Length });
            ProcessSseRecord(tail);
            tail = "";
        }

        _sseBuffer.Clear();
        if (tail.Length > 0)
        {
            if (tail.Length > 1_000_000)
            {
                _log.WriteRun("extractor_trace", "process_sse_buffer_overflow", "warning", 
                    $"SSE buffer overflow! Trimming buffer from {tail.Length} to 1,000,000 characters.");
                tail = tail[^1_000_000..];
            }
            _sseBuffer.Append(tail);
        }
    }

    private void ProcessSseRecord(string record)
    {
        if (string.IsNullOrWhiteSpace(record)) return;
        _sseRecords++;

        _log.WriteRun("extractor_trace", "process_sse_record_start", "ok", 
            $"Processing SSE record: length={record.Length}",
            new { length = record.Length, sample = record.Length > 300 ? record.Substring(0, 300) + "... [TRUNCATED]" : record });

        string? eventName = null;
        var data = new StringBuilder();
        using var reader = new StringReader(record);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
            {
                eventName = line[6..].Trim();
                _log.WriteRun("extractor_trace", "sse_record_event_type", "ok", $"SSE Event field parsed: '{eventName}'");
            }
            else if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                if (data.Length > 0) data.Append('\n');
                data.Append(line[5..].TrimStart());
            }
        }

        var payload = data.ToString().Trim();
        _log.WriteRun("extractor_trace", "sse_record_parsed_data", "ok", 
            $"SSE Event parsed payload: eventName={eventName ?? "none"}, dataLength={payload.Length}",
            new { eventName, dataLength = payload.Length });

        if (payload.Length == 0) return;
        if (payload == "[DONE]")
        {
            _log.WriteRun("extractor_trace", "sse_record_done_marker", "ok", "SSE record contained standard [DONE] stream end marker.");
            return;
        }
        _ssePayloads++;
        ProcessJsonPayload(payload, eventName);
    }

    private static string? TryExtractCdpBody(string raw)
    {
        var trimmed = raw.Trim();
        if (!trimmed.StartsWith("{")) return null;
        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String)
                return body.GetString();
        }
        catch { }
        return null;
    }

    private void ProcessJsonPayload(string payload, string? sseEvent)
    {
        JsonDocument? doc = null;
        try { doc = JsonDocument.Parse(payload); } catch { _jsonParseErrors++; }
        if (doc == null)
        {
            _log.WriteRun("extractor_trace", "json_payload_parse_failed", "error", 
                $"JSON payload failed to parse. Length={payload.Length}",
                new { length = payload.Length, sample = payload.Length > 500 ? payload.Substring(0, 500) + "... [TRUNCATED]" : payload });
            return;
        }

        _log.WriteRun("extractor_trace", "json_payload_parse_success", "ok", 
            $"JSON payload parsed successfully. Length={payload.Length}, sseEvent={sseEvent ?? "none"}",
            new { length = payload.Length, sseEvent });

        using (doc)
        {
            // TELEMETRY: Extract conversation_id from conversation-update payloads
            try
            {
                var root = doc.RootElement;
                string? convType = null;
                string? convId = null;
                string? updateType = null;

                // Direct conversation-update format: {"type":"conversation-update","payload":{"conversation_id":"..."}}
                if (root.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
                    convType = typeProp.GetString();
                if (root.TryGetProperty("payload", out var payloadProp) && payloadProp.ValueKind == JsonValueKind.Object)
                {
                    if (payloadProp.TryGetProperty("conversation_id", out var cidProp) && cidProp.ValueKind == JsonValueKind.String)
                        convId = cidProp.GetString();
                    if (payloadProp.TryGetProperty("type", out var utProp) && utProp.ValueKind == JsonValueKind.String)
                        updateType = utProp.GetString();
                    if (payloadProp.TryGetProperty("update_type", out var ut2Prop) && ut2Prop.ValueKind == JsonValueKind.String)
                        updateType = ut2Prop.GetString();
                }
                // Wrapper array format: [{"type":"message","payload":{"type":"conversation-turn-complete","payload":{"conversation_id":"..."}}}]
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var first = root[0];
                    if (first.TryGetProperty("payload", out var innerPayload) && innerPayload.ValueKind == JsonValueKind.Object)
                    {
                        if (innerPayload.TryGetProperty("type", out var innerType) && innerType.ValueKind == JsonValueKind.String)
                            updateType = innerType.GetString();
                        if (innerPayload.TryGetProperty("payload", out var innerPayload2) && innerPayload2.ValueKind == JsonValueKind.Object)
                        {
                            if (innerPayload2.TryGetProperty("conversation_id", out var cidInner) && cidInner.ValueKind == JsonValueKind.String)
                                convId = cidInner.GetString();
                        }
                    }
                }

                if (convId != null || convType != null)
                {
                    _log.WriteRun("extractor_trace", "json_payload_conversation_context", "ok",
                        $"Conversation context: convId={convId ?? "null"}, type={convType ?? "null"}, updateType={updateType ?? "null"}",
                        new { conversationId = convId, type = convType, updateType });
                }
            }
            catch { }

            ProcessElement(doc.RootElement, sseEvent, null, null);
        }
    }

    private void ProcessElement(JsonElement element, string? sseEvent, string? op, string? path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (TryProcessMessageMarker(element)) return;

            if (element.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object)
            {
                ProcessMessage(message);
                return;
            }

            var localOp = element.TryGetProperty("o", out var o) && o.ValueKind == JsonValueKind.String ? o.GetString() : op;
            var localPath = element.TryGetProperty("p", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : path;

            if (element.TryGetProperty("v", out var v))
            {
                if (v.ValueKind == JsonValueKind.String)
                {
                    var value = v.GetString() ?? "";
                    var shouldApply = ShouldApplyTextValue(sseEvent, localOp, localPath);

                    _log.WriteRun("extractor_trace", "process_element_value_match", "ok", 
                        $"Evaluating property value: path={localPath ?? "null"}, op={localOp ?? "null"}, shouldApply={shouldApply}, sseEvent={sseEvent ?? "null"}, valueLength={value.Length}",
                        new { path = localPath, op = localOp, shouldApply, sseEvent, valueLength = value.Length });

                    if (shouldApply)
                    {
                        ApplyTextValue(localOp, localPath, value);
                        return;
                    }
                    if (string.Equals(sseEvent, "delta", StringComparison.OrdinalIgnoreCase))
                        _textValuesIgnoredByPath++;
                }
                else
                {
                    ProcessElement(v, sseEvent, localOp, localPath);
                    return;
                }
            }

            foreach (var prop in element.EnumerateObject())
            {
                if (prop.NameEquals("v") || prop.NameEquals("o") || prop.NameEquals("p")) continue;
                ProcessElement(prop.Value, sseEvent, localOp, prop.Name);
            }
            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                ProcessElement(item, sseEvent, op, path);
        }
    }

    private bool TryProcessMessageMarker(JsonElement element)
    {
        var type = GetString(element, "type");
        if (!string.Equals(type, "message_marker", StringComparison.OrdinalIgnoreCase)) return false;

        var messageId = GetString(element, "message_id");
        var marker = GetString(element, "marker");
        var markerEvent = GetString(element, "event");
        
        _log.WriteRun("extractor_trace", "message_marker_seen", "ok", 
            $"Message marker: messageId={messageId ?? "null"}, marker={marker ?? "null"}, event={markerEvent ?? "null"}",
            new { messageId, marker, markerEvent });

        if (string.IsNullOrWhiteSpace(messageId)) return true;

        if (string.Equals(marker, "last_token", StringComparison.OrdinalIgnoreCase)
            && string.Equals(markerEvent, "last", StringComparison.OrdinalIgnoreCase))
        {
            _lastTokenMarkers++;
            if (_framesById.TryGetValue(messageId, out var markedFrame))
            {
                markedFrame.MarkerCount++;
                markedFrame.SawLastToken = true;
                _log.WriteRun("extractor_trace", "message_marker_last_token", "ok", $"Set SawLastToken=True on frame: messageId={messageId}");
            }
            else
            {
                _lastTokenForUnknownMessage++;
                _log.WriteRun("extractor_trace", "message_marker_unknown_frame", "warning", $"Received last_token message marker for unknown frame: messageId={messageId}");
            }
            CloseFrame(messageId, "last_token");
        }

        return true;
    }

    private void ProcessMessage(JsonElement message)
    {
        var id = GetString(message, "id");
        if (string.IsNullOrWhiteSpace(id)) return;

        var role = "";
        if (message.TryGetProperty("author", out var author) && author.ValueKind == JsonValueKind.Object)
            role = GetString(author, "role") ?? "";

        var channel = GetString(message, "channel") ?? "";
        var status = GetString(message, "status") ?? "";

        // TELEMETRY: conversation_id comes from wrapping conversation-update context\n        // It is logged in ProcessElement's traversal when encountered
        
        _log.WriteRun("extractor_trace", "process_message_metadata", "ok", 
            $"ProcessMessage frame evaluation: id={id}, role={role}, channel={channel}, status={status}",
            new { id, role, channel, status });

        if (!role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
        {
            // TELEMETRY: Log rejected non-assistant messages
            _log.WriteRun("extractor_trace", "process_message_rejected_role", "ok",
                $"Message rejected: role={role} (not assistant). id={id}, channel={channel}",
                new { id, role, channel, status });
            return;
        }

        // gpt-5-5-thinking sends final messages without a channel field (null/empty).
        // Only skip messages that explicitly declare a non-final channel (e.g. "thinking").
        if (!string.IsNullOrEmpty(channel) && !channel.Equals("final", StringComparison.OrdinalIgnoreCase))
        {
            // TELEMETRY: Log rejected non-final channel messages
            _log.WriteRun("extractor_trace", "process_message_rejected_channel", "ok",
                $"Message rejected: channel={channel} (not final/empty). id={id}, role={role}",
                new { id, role, channel, status });
            return;
        }

        if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object)
        {
            _log.WriteRun("extractor_trace", "process_message_no_content", "ok",
                $"Message has no content object: id={id}", new { id, role, channel });
            return;
        }
        var contentType = GetString(content, "content_type") ?? "";
        if (!contentType.Equals("text", StringComparison.OrdinalIgnoreCase))
        {
            _log.WriteRun("extractor_trace", "process_message_rejected_content_type", "ok",
                $"Message rejected: content_type={contentType} (not text). id={id}",
                new { id, role, channel, contentType });
            return;
        }

        var frame = GetOrCreateFrame(id);
        frame.MessageObjectCount++;
        _currentAssistantMessageId = id;

        if (!content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
        {
            _log.WriteRun("extractor_trace", "process_message_no_parts", "ok",
                $"Message content has no parts array: id={id}", new { id, contentType });
            return;
        }
        var text = string.Concat(parts.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? ""));
        
        _log.WriteRun("extractor_trace", "process_message_content", "ok", 
            $"ProcessMessage content extracted: frameTextLenBefore={frame.Text.Length}, newTextLen={text.Length}",
            new { id, frameTextLenBefore = frame.Text.Length, newTextLen = text.Length,
                   containsExecBegin = text.Contains("@@OPENBRIDGE_EXEC_BEGIN@@"),
                   textSample = text.Length > 500 ? text.Substring(0, 500) + "... [TRUNCATED]" : text });

        if (text.Length == 0) return;

        if (text.Length >= frame.Text.Length)
        {
            _log.WriteRun("extractor_trace", "process_message_content_overwrite", "ok", $"Overwriting frame text buffer with full content: id={id}, length={text.Length}");
            frame.Text.Clear();
            frame.Text.Append(text);
        }
    }

    private static bool ShouldApplyTextValue(string? sseEvent, string? op, string? path)
    {
        if (IsTextPartPath(path)) return true;
        if (sseEvent != null && sseEvent.Equals("delta", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(path)) return true;
        if (op != null && (op.Equals("append", StringComparison.OrdinalIgnoreCase) || op.Equals("replace", StringComparison.OrdinalIgnoreCase)) && IsTextPartPath(path)) return true;
        return false;
    }

    private void ApplyTextValue(string? op, string? path, string value)
    {
        if (value.Length == 0)
        {
            _emptyDeltaValues++;
            _log.WriteRun("extractor_trace", "apply_text_value_empty", "ok", "Ignored empty delta value.");
            return;
        }
        if (string.IsNullOrWhiteSpace(_currentAssistantMessageId))
        {
            _textValuesWithoutMessageId++;
            _log.WriteRun("extractor_trace", "apply_text_value_no_frame", "warning", $"Ignored delta value because _currentAssistantMessageId is null or empty. Value: '{value}'");
            return;
        }

        var frame = GetOrCreateFrame(_currentAssistantMessageId);
        frame.LastTextAtUtc = _currentEventTsUtc;
        var beforeLen = frame.Text.Length;

        if (op != null && op.Equals("replace", StringComparison.OrdinalIgnoreCase) && IsTextPartPath(path))
        {
            frame.Text.Clear();
            frame.Text.Append(value);
            frame.DeltaCount++;
            
            _log.WriteRun("extractor_trace", "apply_text_value_replace", "ok", 
                $"Applied REPLACE delta to frame={frame.MessageId}: op={op}, path={path}, valueLength={value.Length}, deltaCount={frame.DeltaCount}, textLenBefore={beforeLen}, textLenAfter={frame.Text.Length}",
                new { messageId = frame.MessageId, op, path, valueLength = value.Length, deltaCount = frame.DeltaCount, textLenBefore = beforeLen, textLenAfter = frame.Text.Length, sample = value.Length > 200 ? value.Substring(0, 200) + "... [TRUNCATED]" : value });
            return;
        }

        var current = frame.Text.ToString();
        if (value.Length >= current.Length && value.StartsWith(current, StringComparison.Ordinal))
        {
            frame.Text.Clear();
            frame.Text.Append(value);
            frame.DeltaCount++;

            _log.WriteRun("extractor_trace", "apply_text_value_superset", "ok", 
                $"Applied SUPERSET replacement delta to frame={frame.MessageId}: valueLength={value.Length}, deltaCount={frame.DeltaCount}, textLenBefore={beforeLen}, textLenAfter={frame.Text.Length}",
                new { messageId = frame.MessageId, valueLength = value.Length, deltaCount = frame.DeltaCount, textLenBefore = beforeLen, textLenAfter = frame.Text.Length, addedLength = value.Length - beforeLen });
            return;
        }

        if (current.EndsWith(value, StringComparison.Ordinal))
        {
            _log.WriteRun("extractor_trace", "apply_text_value_duplicate_suffix", "ok", $"Ignored delta value because it is already a suffix of the current buffer: value='{value}'");
            return;
        }

        frame.Text.Append(value);
        frame.DeltaCount++;

        _log.WriteRun("extractor_trace", "apply_text_value_append", "ok", 
            $"Applied APPEND delta to frame={frame.MessageId}: valueLength={value.Length}, deltaCount={frame.DeltaCount}, textLenBefore={beforeLen}, textLenAfter={frame.Text.Length}",
            new { messageId = frame.MessageId, valueLength = value.Length, deltaCount = frame.DeltaCount, textLenBefore = beforeLen, textLenAfter = frame.Text.Length, sample = value });
    }

    private AssistantMessageFrame GetOrCreateFrame(string messageId)
    {
        if (_framesById.TryGetValue(messageId, out var existing)) return existing;

        var frame = new AssistantMessageFrame
        {
            MessageId = messageId,
            StartedAtUtc = _currentEventTsUtc
        };
        _framesById[messageId] = frame;
        _frames.Add(frame);
        return frame;
    }

    private void CloseFrame(string messageId, string reason)
    {
        if (!_framesById.TryGetValue(messageId, out var frame)) return;
        if (frame.Status == "complete") return;
        frame.Status = reason == "last_token" ? "complete" : "partial";
        frame.CloseReason = reason;
        frame.EndedAtUtc = _currentEventTsUtc;
        if (string.Equals(_currentAssistantMessageId, messageId, StringComparison.Ordinal))
            _currentAssistantMessageId = null;
        if (reason == "last_token" && frame.Text.Length > 0)
        {
            // Close pending raw file as message-specific
            if (_rawWriter != null)
            {
                _rawWriter.Dispose();
                var pendingPath = Path.Combine(AppPaths.Extracted, $"run_{_log.RunId}_pending.ndjson");
                var finalPath = Path.Combine(AppPaths.Extracted, $"run_{_log.RunId}_msg_{messageId}_raw.ndjson");
                try { File.Move(pendingPath, finalPath); } catch { }
                // Start new pending for next message
                _rawWriter = new StreamWriter(new FileStream(pendingPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8) { AutoFlush = true };
            }
            Interlocked.Increment(ref _pendingFinishCount);
        }
    }

    private void CloseOpenFrames(string reason)
    {
        foreach (var frame in _frames)
        {
            if (frame.Status == "open")
            {
                frame.Status = frame.Text.Length > 0 ? "partial" : "empty";
                frame.CloseReason = reason;
                frame.EndedAtUtc = _currentEventTsUtc;
            }
        }
    }


    private void WriteMessagesNdjson()
    {
        if (string.IsNullOrWhiteSpace(_messagesPath)) return;

        using var writer = new StreamWriter(new FileStream(_messagesPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false)) { AutoFlush = true };
        foreach (var frame in _frames)
        {
            var record = new
            {
                tsUtc = DateTime.UtcNow.ToString("O"),
                runId = _log.RunId,
                tabInternalId = _log.TabInternalId,
                messageId = frame.MessageId,
                startedAt = frame.StartedAtUtc,
                endedAt = frame.EndedAtUtc,
                role = frame.Role,
                channel = frame.Channel,
                chars = frame.Text.Length,
                deltaCount = frame.DeltaCount,
                messageObjectCount = frame.MessageObjectCount,
                markerCount = frame.MarkerCount,
                sawLastToken = frame.SawLastToken,
                status = frame.Status,
                endReason = frame.CloseReason
            };
            writer.WriteLine(JsonSerializer.Serialize(record));
        }
    }

    private (object Summary, List<(string message, object data)> Warnings) BuildDiagnostics()
    {
        var warnings = new List<(string message, object data)>();
        var missingLastToken = _frames.Where(f => f.Text.Length > 0 && !f.SawLastToken).Select(f => f.MessageId).ToArray();
        var emptyFrames = _frames.Where(f => f.Text.Length == 0).Select(f => f.MessageId).ToArray();
        var duplicateFrames = _frames.GroupBy(f => f.MessageId).Where(g => g.Count() > 1).Select(g => new { messageId = g.Key, count = g.Count() }).ToArray();

        if (missingLastToken.Length > 0)
            warnings.Add(("Assistant message frame without last_token", new { messageIds = missingLastToken }));
        if (emptyFrames.Length > 0)
            warnings.Add(("Assistant message frame has zero chars", new { messageIds = emptyFrames }));
        if (_textValuesWithoutMessageId > 0)
            warnings.Add(("Text delta values observed without current assistant message id", new { count = _textValuesWithoutMessageId }));
        if (_textValuesIgnoredByPath > 0)
            warnings.Add(("Delta string values ignored because path/event did not match assistant text rules", new { count = _textValuesIgnoredByPath }));
        if (_lastTokenForUnknownMessage > 0)
            warnings.Add(("last_token marker observed for unknown message id", new { count = _lastTokenForUnknownMessage }));
        if (_jsonParseErrors > 0)
            warnings.Add(("JSON payload parse errors observed", new { count = _jsonParseErrors }));
        if (duplicateFrames.Length > 0)
            warnings.Add(("Duplicate frame objects for the same message id", new { frames = duplicateFrames }));
        if (_frames.Count == 0)
            warnings.Add(("No assistant final message frames were created", new { rawEvents = _rawEvents, pageStreamEvents = _pageStreamEvents, sseRecords = _sseRecords, ssePayloads = _ssePayloads }));

        var summary = new
        {
            frames = _frames.Count,
            completeFrames = _frames.Count(f => f.Status == "complete"),
            partialFrames = _frames.Count(f => f.Status == "partial"),
            emptyFrames = _frames.Count(f => f.Text.Length == 0),
            chars = _frames.Sum(f => f.Text.Length),
            deltaCount = _frames.Sum(f => f.DeltaCount),
            rawEvents = _rawEvents,
            pageStreamEvents = _pageStreamEvents,
            sseRecords = _sseRecords,
            ssePayloads = _ssePayloads,
            lastTokenMarkers = _lastTokenMarkers,
            textValuesWithoutMessageId = _textValuesWithoutMessageId,
            textValuesIgnoredByPath = _textValuesIgnoredByPath,
            jsonParseErrors = _jsonParseErrors,
            warnings = warnings.Count
        };

        return (summary, warnings);
    }

    private string GetCurrentAnswerText()
    {
        var sb = new StringBuilder();
        foreach (var frame in _frames.Where(f => f.Text.Length > 0))
        {
            if (sb.Length > 0) sb.AppendLine().AppendLine();
            AppendFrameText(sb, frame);
        }
        return sb.ToString();
    }

    private static string GetCurrentAnswerTextForFrames(List<AssistantMessageFrame> frames)
    {
        var sb = new StringBuilder();
        foreach (var frame in frames)
        {
            if (sb.Length > 0) sb.AppendLine().AppendLine();
            AppendFrameText(sb, frame);
        }
        return sb.ToString();
    }

    private static void AppendFrameText(StringBuilder sb, AssistantMessageFrame frame)
    {
        sb.Append('[').Append(frame.StartedAtUtc).Append("] assistant message ").Append(frame.MessageId).AppendLine();
        sb.AppendLine();
        sb.Append(frame.Text);
        if (!frame.Text.ToString().EndsWith("\n", StringComparison.Ordinal)) sb.AppendLine();
        sb.AppendLine();
        sb.Append("[END assistant message ").Append(frame.MessageId);
        if (!string.IsNullOrWhiteSpace(frame.EndedAtUtc)) sb.Append(" at ").Append(frame.EndedAtUtc);
        if (!string.IsNullOrWhiteSpace(frame.Status)) sb.Append(" status=").Append(frame.Status);
        if (!string.IsNullOrWhiteSpace(frame.CloseReason)) sb.Append(" reason=").Append(frame.CloseReason);
        sb.Append(']');
    }

    private static bool IsTextPartPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var p = path.Replace("\\/", "/");
        return p.Contains("/message/content/parts/", StringComparison.OrdinalIgnoreCase)
               || p.Contains("content.parts", StringComparison.OrdinalIgnoreCase)
               || p.EndsWith("/parts/0", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetString(JsonElement e, string prop)
    {
        return e.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    }
}
