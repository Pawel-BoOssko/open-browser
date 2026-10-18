using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

public sealed class NetworkLogger
{
    private readonly CoreWebView2 _core;
    private readonly LogWriter _log;
    private readonly ResponseExtractor _extractor;
    private readonly Dictionary<string, string> _requestUrls = new();

    public NetworkLogger(CoreWebView2 core, LogWriter log, ResponseExtractor extractor)
    {
        _core = core;
        _log = log;
        _extractor = extractor;
    }

    public async Task InitializeAsync()
    {
        await _core.CallDevToolsProtocolMethodAsync("Network.enable", "{}");
        Subscribe("Network.requestWillBeSent", OnRequestWillBeSent);
        Subscribe("Network.responseReceived", OnResponseReceived);
        Subscribe("Network.loadingFinished", OnLoadingFinished);
        Subscribe("Network.webSocketFrameReceived", OnWebSocketFrameReceived);
        Subscribe("Network.webSocketFrameSent", OnWebSocketFrameSent);
        Subscribe("Network.webSocketCreated", OnWebSocketCreated);
        Subscribe("Network.webSocketClosed", OnWebSocketClosed);
        _log.WriteRun("cdp", "network_enable", "ok", "CDP Network enabled with WebSocket lifecycle tracking");
    }

    private void Subscribe(string eventName, Action<string> handler)
    {
        var receiver = _core.GetDevToolsProtocolEventReceiver(eventName);
        receiver.DevToolsProtocolEventReceived += (_, e) =>
        {
            try { handler(e.ParameterObjectAsJson); }
            catch (Exception ex) { _log.WriteRun("cdp", "error", "error", $"Handler failed for {eventName}: {ex.Message}"); }
        };
    }

    private void OnRequestWillBeSent(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var requestId = GetString(root, "requestId") ?? "";
        var req = root.TryGetProperty("request", out var r) ? r : default;
        var url = req.ValueKind == JsonValueKind.Object ? GetString(req, "url") ?? "" : "";
        if (!string.IsNullOrEmpty(requestId)) _requestUrls[requestId] = url;

        var isConversation = url.Contains("backend-api/conversation") || url.Contains("backend-api/f/conversation");

        _log.WriteRun("cdp_trace", "request_will_be_sent", "ok", 
            $"CDP Request: id={requestId}, isConversation={isConversation}, url={url}",
            new { requestId, url, isConversation });

        _log.WriteRun("cdp", "cdp_request", "ok", ClassifyUrl(url), new
        {
            requestId,
            url,
            method = req.ValueKind == JsonValueKind.Object ? GetString(req, "method") : null,
            headers = req.ValueKind == JsonValueKind.Object && req.TryGetProperty("headers", out var h) ? JsonSerializer.Deserialize<object>(h.GetRawText()) : null,
            postData = req.ValueKind == JsonValueKind.Object && req.TryGetProperty("postData", out var p) ? p.GetString() : null
        });
    }

    private void OnResponseReceived(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var requestId = GetString(root, "requestId") ?? "";
        var response = root.TryGetProperty("response", out var r) ? r : default;
        var url = response.ValueKind == JsonValueKind.Object ? GetString(response, "url") ?? "" : "";
        var mimeType = response.ValueKind == JsonValueKind.Object ? GetString(response, "mimeType") : null;
        var status = response.ValueKind == JsonValueKind.Object && response.TryGetProperty("status", out var s) ? s.GetDouble() : null as double?;

        var isConversation = url.Contains("backend-api/conversation") || url.Contains("backend-api/f/conversation");

        _log.WriteRun("cdp_trace", "response_received", "ok", 
            $"CDP Response: id={requestId}, status={status}, mimeType={mimeType}, isConversation={isConversation}, url={url}",
            new { requestId, url, status, mimeType, isConversation });

        _log.WriteRun("cdp", "cdp_response", "ok", ClassifyUrl(url), new
        {
            requestId,
            url,
            status,
            mimeType,
            headers = response.ValueKind == JsonValueKind.Object && response.TryGetProperty("headers", out var h) ? JsonSerializer.Deserialize<object>(h.GetRawText()) : null
        });
    }

    private async void OnLoadingFinished(string json)
    {
        string requestId = "";
        try
        {
            using var doc = JsonDocument.Parse(json);
            requestId = GetString(doc.RootElement, "requestId") ?? "";
            _requestUrls.TryGetValue(requestId, out var url);
            var lowerUrl = url?.ToLowerInvariant() ?? "";
            var isConversation = lowerUrl.Contains("backend-api/conversation") && !lowerUrl.Contains("/prepare");

            _log.WriteRun("cdp_trace", "loading_finished", "ok", 
                $"CDP LoadingFinished: id={requestId}, isConversation={isConversation}, url={url ?? "null"}",
                new { requestId, url, isConversation });

            _log.WriteRun("cdp", "cdp_loading_finished", "ok", ClassifyUrl(url ?? ""), new { requestId, url });

            if (string.IsNullOrEmpty(requestId))
            {
                _log.WriteRun("cdp_trace", "get_body_skipped", "ok", $"CDP Skip Body: requestId is null or empty");
                return;
            }

            if (isConversation)
            {
                _log.WriteRun("cdp_trace", "get_body_start", "ok", $"CDP Calling getResponseBody: id={requestId}, url={url}");
                var bodyJson = await _core.CallDevToolsProtocolMethodAsync("Network.getResponseBody", JsonSerializer.Serialize(new { requestId }));
                
                _log.WriteRun("cdp_trace", "get_body_success", "ok", 
                    $"CDP getResponseBody Success: id={requestId}, bodyLength={bodyJson.Length}",
                    new { requestId, bodyLength = bodyJson.Length });

                _log.WriteRun("cdp", "cdp_response_body", "ok", ClassifyUrl(url ?? ""), new { requestId, url, body = bodyJson });
                _extractor.AddRaw("cdp", "cdp_response_body", bodyJson);
            }
            else
            {
                _log.WriteRun("cdp_trace", "get_body_skipped", "ok", $"CDP Skip Body: URL is not conversation stream endpoint: id={requestId}, url={url}");
            }
        }
        catch (Exception ex)
        {
            _log.WriteRun("cdp_trace", "get_body_failed", "error", 
                $"CDP getResponseBody Error: id={requestId}, error={ex.Message}",
                new { requestId, error = ex.Message });

            _log.WriteRun("cdp", "cdp_response_body", "partial", "Network.getResponseBody failed", new { requestId, error = ex.Message });
        }
    }

    private void OnWebSocketFrameReceived(string json)
    {
        _log.WriteRun("cdp", "cdp_websocket_frame", "received", null, JsonSerializer.Deserialize<object>(json));

        // Extract requestId and payloadData for improved logging
        string? requestId = null;
        string? payloadSample = null;
        int payloadLength = 0;
        try
        {
            using var doc = JsonDocument.Parse(json);
            requestId = GetString(doc.RootElement, "requestId");
            if (doc.RootElement.TryGetProperty("response", out var resp) && resp.ValueKind == JsonValueKind.Object)
            {
                var payload = GetString(resp, "payloadData") ?? "";
                payloadLength = payload.Length;
                payloadSample = payload.Length > 500 ? payload.Substring(0, 500) : payload;
            }
        }
        catch { }

        _log.WriteRun("cdp_trace", "websocket_frame_received_detail", "ok",
            $"CDP WS Frame Received: requestId={requestId ?? "null"}, payloadLength={payloadLength}",
            new { requestId, payloadLength, payloadSample });

        _extractor.AddRaw("cdp", "cdp_websocket_frame_received", json);
    }

    private void OnWebSocketFrameSent(string json)
    {
        _log.WriteRun("cdp", "cdp_websocket_frame", "sent", null, JsonSerializer.Deserialize<object>(json));

        // TELEMETRY: Log sent frame details
        string? requestId = null;
        int payloadLength = 0;
        try
        {
            using var doc = JsonDocument.Parse(json);
            requestId = GetString(doc.RootElement, "requestId");
            if (doc.RootElement.TryGetProperty("response", out var resp) && resp.ValueKind == JsonValueKind.Object)
            {
                var payload = GetString(resp, "payloadData") ?? "";
                payloadLength = payload.Length;
            }
        }
        catch { }

        _log.WriteRun("cdp_trace", "websocket_frame_sent_detail", "ok",
            $"CDP WS Frame Sent: requestId={requestId ?? "null"}, payloadLength={payloadLength}",
            new { requestId, payloadLength });
    }

    private void OnWebSocketCreated(string json)
    {
        string? requestId = null;
        string? url = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            requestId = GetString(doc.RootElement, "requestId");
            url = GetString(doc.RootElement, "url");
        }
        catch { }

        _log.WriteRun("cdp", "cdp_websocket_created", "ok",
            $"CDP WebSocket Created: requestId={requestId ?? "null"}, url={url ?? "null"}",
            new { requestId, url });
    }

    private void OnWebSocketClosed(string json)
    {
        string? requestId = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            requestId = GetString(doc.RootElement, "requestId");
        }
        catch { }

        _log.WriteRun("cdp", "cdp_websocket_closed", "ok",
            $"CDP WebSocket Closed: requestId={requestId ?? "null"}",
            new { requestId });
    }

    private static string? GetString(JsonElement e, string prop)
    {
        return e.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    }

    private static string ClassifyUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return "unknown";
        var l = url.ToLowerInvariant();
        if (l.Contains("conversation") || l.Contains("backend-api") || l.Contains("responses") || l.Contains("stream")) return "possible_chatgpt_message_or_stream";
        if (l.Contains("auth") || l.Contains("login")) return "auth_or_login";
        return "network_event";
    }
}
