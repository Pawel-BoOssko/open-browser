using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

public sealed class WebViewMessageHandler
{
    private readonly LogWriter _log;
    private readonly ResponseExtractor _extractor;
    private readonly Action _requestDiagnosticsRefresh;

    public WebViewMessageHandler(LogWriter log, ResponseExtractor extractor, Action requestDiagnosticsRefresh)
    {
        _log = log;
        _extractor = extractor;
        _requestDiagnosticsRefresh = requestDiagnosticsRefresh;
    }

    public void HandleWebMessage(CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.WebMessageAsJson;
            PipelineRawDump.Write("02_WebViewMessageHandler.txt", json);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var eventType = root.TryGetProperty("eventType", out var et) ? et.GetString() ?? "page_message" : "page_message";
            var status = root.TryGetProperty("status", out var st) ? st.GetString() : "ok";
            object? data = root.TryGetProperty("data", out var d) ? JsonSerializer.Deserialize<object>(d.GetRawText()) : JsonSerializer.Deserialize<object>(json);
            
            _log.WriteRun("page_tap", eventType, status, null, data);
            
            _log.WriteRun("handler_trace", "web_message_received", "ok", 
                $"WebView IPC Message: eventType={eventType}, status={status}, hasData={d.ValueKind != JsonValueKind.Null}",
                new { eventType, status, jsonLength = json.Length });

            if (root.TryGetProperty("data", out var dataEl))
            {
                if (dataEl.TryGetProperty("chunk", out var chunk) && chunk.ValueKind == JsonValueKind.String)
                {
                    var chunkVal = chunk.GetString() ?? "";
                    _log.WriteRun("handler_trace", "add_raw_forward", "ok", $"Forwarding data.chunk to Extractor: eventType={eventType}, length={chunkVal.Length}",
                        new { field = "chunk", eventType, length = chunkVal.Length, sample = chunkVal.Length > 300 ? chunkVal.Substring(0, 300) + "... [TRUNCATED]" : chunkVal });
                    _extractor.AddRaw("page_tap", eventType, chunkVal);
                }
                else if (dataEl.TryGetProperty("responseText", out var responseText) && responseText.ValueKind == JsonValueKind.String)
                {
                    var responseTextVal = responseText.GetString() ?? "";
                    _log.WriteRun("handler_trace", "add_raw_forward", "ok", $"Forwarding data.responseText to Extractor: eventType={eventType}, length={responseTextVal.Length}",
                        new { field = "responseText", eventType, length = responseTextVal.Length, sample = responseTextVal.Length > 300 ? responseTextVal.Substring(0, 300) + "... [TRUNCATED]" : responseTextVal });
                    _extractor.AddRaw("page_tap", eventType, responseTextVal);
                }
                else if (dataEl.TryGetProperty("data", out var wsData) && wsData.ValueKind == JsonValueKind.String)
                {
                    var wsDataVal = wsData.GetString() ?? "";
                    _log.WriteRun("handler_trace", "add_raw_forward", "ok", $"Forwarding data.data to Extractor: eventType={eventType}, length={wsDataVal.Length}",
                        new { field = "data", eventType, length = wsDataVal.Length, sample = wsDataVal.Length > 300 ? wsDataVal.Substring(0, 300) + "... [TRUNCATED]" : wsDataVal });
                    _extractor.AddRaw("page_tap", eventType, wsDataVal);
                }
                else
                {
                    // TELEMETRY: No recognized data field found — log what fields ARE present
                    var fieldNames = new System.Collections.Generic.List<string>();
                    if (dataEl.ValueKind == JsonValueKind.Object)
                        foreach (var prop in dataEl.EnumerateObject()) fieldNames.Add(prop.Name);
                    _log.WriteRun("handler_trace", "add_raw_no_recognized_field", "warning",
                        $"Data element has no chunk/responseText/data field: eventType={eventType}, fields=[{string.Join(",", fieldNames)}]",
                        new { eventType, fieldNames = fieldNames.ToArray(), dataKind = dataEl.ValueKind.ToString(), rawLength = json.Length });
                }
            }
            else
            {
                // TELEMETRY: No 'data' property at all
                _log.WriteRun("handler_trace", "add_raw_no_data_property", "warning",
                    $"Message has no 'data' property: eventType={eventType}",
                    new { eventType, jsonLength = json.Length });
            }

            // SSE stream capture from fetch responses
            if (eventType == "page_fetch_sse_record" && root.TryGetProperty("data", out var sseData) &&
                sseData.ValueKind == JsonValueKind.Object &&
                sseData.TryGetProperty("record", out var sseRecord) && sseRecord.ValueKind == JsonValueKind.String)
            {
                var sseRecordVal = sseRecord.GetString() ?? "";
                var sseUrl = sseData.TryGetProperty("url", out var sseUrlProp) && sseUrlProp.ValueKind == JsonValueKind.String ? sseUrlProp.GetString() : null;
                var sseChunkIndex = sseData.TryGetProperty("chunkIndex", out var sseIdx) && sseIdx.ValueKind == JsonValueKind.Number ? sseIdx.GetInt32() : -1;
                _log.WriteRun("handler_trace", "add_raw_forward_sse", "ok", $"Forwarding SSE record to Extractor: length={sseRecordVal.Length}, chunkIndex={sseChunkIndex}, url={sseUrl ?? "null"}",
                    new { length = sseRecordVal.Length, chunkIndex = sseChunkIndex, url = sseUrl, sample = sseRecordVal.Length > 500 ? sseRecordVal.Substring(0, 500) + "... [TRUNCATED]" : sseRecordVal });
                _extractor.AddRaw("page_tap", "page_fetch_sse_record", sseRecordVal);
            }

            if (eventType is "page_fetch_done" or "page_xhr_done" or "page_eventsource_error" or "page_fetch_sse_done")
            {
                _log.WriteRun("handler_trace", "extractor_finish_trigger", "ok", $"Triggering Extractor.Finish due to eventType={eventType}");
                _extractor.Finish();
            }
            else if (eventType == "page_websocket_message" && root.TryGetProperty("data", out var wsDataEl))
            {
                var rawWs = wsDataEl.TryGetProperty("data", out var rawText) && rawText.ValueKind == JsonValueKind.String
                    ? rawText.GetString() ?? "" : "";
                if (rawWs.Contains("\"status\":\"finished_successfully\"", StringComparison.Ordinal) &&
                    rawWs.Contains("\"role\":\"assistant\"", StringComparison.Ordinal))
                {
                    _log.WriteRun("handler_trace", "extractor_finish_websocket", "ok", "WebSocket finished successfully message found. Triggering Extractor.Finish");
                    _extractor.Finish();
                }
            }

            if (eventType.StartsWith("loaded_turns_monitor", StringComparison.OrdinalIgnoreCase))
                _requestDiagnosticsRefresh();
        }
        catch (Exception ex)
        {
            _log.WriteRun("page_tap", "error", "error", "Failed to handle page message", new { ex.Message, raw = e.WebMessageAsJson });
        }
    }
}
