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

            if (isPageStream)
            {
                _sawPageStream = true;
                _pageStreamEvents++;
            }
            if (!isPageStream && !isCdpConversationBody && _sawPageStream) return;

            // Capture Python tool message_id for sandbox download
            // Raw data has escaped JSON. Search for message_id near a UUID.
            if (raw.Contains("tool_invoked"))
            {
                var toolMid = System.Text.RegularExpressions.Regex.Match(
                    raw, @"message_id[^a-f0-9-]*([a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})");
                if (toolMid.Success)
                    AppConstants.LastToolMessageId = toolMid.Groups[1].Value;
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
            foreach (var f in _frames)
            {
                if (f.Text.Length > 0 && _observedMessageIds.Add(f.MessageId))
                {
                    hasNewFrames = true;
                    newFrames.Add(f);
                }
            }

            if (hasNewFrames)
            {
                AppConstants.LastAssistantMessageId = newFrames[^1].MessageId;
                var newAnswer = GetCurrentAnswerTextForFrames(newFrames);
                var parseResult = _observer.Observe(newAnswer);
                if (parseResult?.HasEnvelope == true)
                {
                    OnEnvelopeDetected?.Invoke(parseResult);
                }
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
        for (int depth = 0; depth < 5; depth++)
        {
            var cdpBody = TryExtractCdpBody(raw);
            if (cdpBody == null) break;
            raw = cdpBody;
        }

        var looksLikeSse = raw.Contains("data:", StringComparison.OrdinalIgnoreCase)
                           || raw.Contains("event:", StringComparison.OrdinalIgnoreCase)
                           || _sseBuffer.Length > 0;

        if (looksLikeSse)
        {
            if (raw.Length > 0) _sseBuffer.Append(raw);
            ProcessSseBuffer(flushTail);
            return;
        }

        var trimmed = raw.Trim();
        if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            ProcessJsonPayload(trimmed, null);
    }

    private void ProcessSseBuffer(bool flushTail)
    {
        if (_sseBuffer.Length == 0) return;

        var text = _sseBuffer.ToString().Replace("\r\n", "\n").Replace('\r', '\n');
        var consumed = 0;

        while (true)
        {
            var idx = text.IndexOf("\n\n", consumed, StringComparison.Ordinal);
            if (idx < 0) break;
            var record = text.Substring(consumed, idx - consumed);
            consumed = idx + 2;
            ProcessSseRecord(record);
        }

        var tail = text.Substring(consumed);
        if (flushTail && !string.IsNullOrWhiteSpace(tail))
        {
            ProcessSseRecord(tail);
            tail = "";
        }

        _sseBuffer.Clear();
        if (tail.Length > 0)
        {
            if (tail.Length > 1_000_000)
                tail = tail[^1_000_000..];
            _sseBuffer.Append(tail);
        }
    }

    private void ProcessSseRecord(string record)
    {
        if (string.IsNullOrWhiteSpace(record)) return;
        _sseRecords++;

        string? eventName = null;
        var data = new StringBuilder();
        using var reader = new StringReader(record);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
                eventName = line[6..].Trim();
            else if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                if (data.Length > 0) data.Append('\n');
                data.Append(line[5..].TrimStart());
            }
        }

        var payload = data.ToString().Trim();
        if (payload.Length == 0 || payload == "[DONE]") return;
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
        if (doc == null) return;
        using (doc)
        {
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
                    if (ShouldApplyTextValue(sseEvent, localOp, localPath))
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
        if (string.IsNullOrWhiteSpace(messageId)) return true;

        if (string.Equals(marker, "last_token", StringComparison.OrdinalIgnoreCase)
            && string.Equals(markerEvent, "last", StringComparison.OrdinalIgnoreCase))
        {
            _lastTokenMarkers++;
            if (_framesById.TryGetValue(messageId, out var markedFrame))
            {
                markedFrame.MarkerCount++;
                markedFrame.SawLastToken = true;
            }
            else
            {
                _lastTokenForUnknownMessage++;
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
        if (!role.Equals("assistant", StringComparison.OrdinalIgnoreCase)) return;

        var channel = GetString(message, "channel") ?? "";
        if (!channel.Equals("final", StringComparison.OrdinalIgnoreCase)) return;

        if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object) return;
        var contentType = GetString(content, "content_type") ?? "";
        if (!contentType.Equals("text", StringComparison.OrdinalIgnoreCase)) return;

        var frame = GetOrCreateFrame(id);
        frame.MessageObjectCount++;
        _currentAssistantMessageId = id;

        if (!content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array) return;
        var text = string.Concat(parts.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? ""));
        if (text.Length == 0) return;

        if (text.Length >= frame.Text.Length)
        {
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
            return;
        }
        if (string.IsNullOrWhiteSpace(_currentAssistantMessageId))
        {
            _textValuesWithoutMessageId++;
            return;
        }

        var frame = GetOrCreateFrame(_currentAssistantMessageId);
        frame.LastTextAtUtc = _currentEventTsUtc;
        if (op != null && op.Equals("replace", StringComparison.OrdinalIgnoreCase) && IsTextPartPath(path))
        {
            frame.Text.Clear();
            frame.Text.Append(value);
            frame.DeltaCount++;
            return;
        }

        var current = frame.Text.ToString();
        if (value.Length >= current.Length && value.StartsWith(current, StringComparison.Ordinal))
        {
            frame.Text.Clear();
            frame.Text.Append(value);
            frame.DeltaCount++;
            return;
        }

        if (current.EndsWith(value, StringComparison.Ordinal)) return;

        frame.Text.Append(value);
        frame.DeltaCount++;
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
