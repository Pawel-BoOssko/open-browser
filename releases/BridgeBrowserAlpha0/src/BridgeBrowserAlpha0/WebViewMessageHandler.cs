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
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var eventType = root.TryGetProperty("eventType", out var et) ? et.GetString() ?? "page_message" : "page_message";
            var status = root.TryGetProperty("status", out var st) ? st.GetString() : "ok";
            object? data = root.TryGetProperty("data", out var d) ? JsonSerializer.Deserialize<object>(d.GetRawText()) : JsonSerializer.Deserialize<object>(json);
            _log.WriteRun("page_tap", eventType, status, null, data);


            if (root.TryGetProperty("data", out var dataEl))
            {
                if (dataEl.TryGetProperty("chunk", out var chunk) && chunk.ValueKind == JsonValueKind.String)
                    _extractor.AddRaw("page_tap", eventType, chunk.GetString() ?? "");
                else if (dataEl.TryGetProperty("responseText", out var responseText) && responseText.ValueKind == JsonValueKind.String)
                    _extractor.AddRaw("page_tap", eventType, responseText.GetString() ?? "");
                else if (dataEl.TryGetProperty("data", out var wsData) && wsData.ValueKind == JsonValueKind.String)
                    _extractor.AddRaw("page_tap", eventType, wsData.GetString() ?? "");
            }

            if (eventType is "page_fetch_done" or "page_xhr_done" or "page_eventsource_error")
                _extractor.Finish();
            else if (eventType == "page_websocket_message" && root.TryGetProperty("data", out var wsDataEl))
            {
                var rawWs = wsDataEl.TryGetProperty("data", out var rawText) && rawText.ValueKind == JsonValueKind.String
                    ? rawText.GetString() ?? "" : "";
                if (rawWs.Contains("\"status\":\"finished_successfully\"", StringComparison.Ordinal) &&
                    rawWs.Contains("\"role\":\"assistant\"", StringComparison.Ordinal))
                    _extractor.Finish();
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
