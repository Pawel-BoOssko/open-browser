using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

/// <summary>
/// CDP network phase telemetry only: no request bodies, no headers,
/// no URL query parameters, no authentication, no frame payloads.
/// </summary>
internal sealed class LiteTransportDiagnostics
{
    private readonly CoreWebView2 _core;
    private readonly ConcurrentDictionary<string, string> _requests = new();
    private readonly ConcurrentDictionary<string, int> _websocketFrameCount = new();

    internal LiteTransportDiagnostics(CoreWebView2 core) => _core = core;

    internal static string Classify(string? address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
            !(uri.Scheme is "https" or "wss") ||
            !(uri.Host == "chatgpt.com" || uri.Host.EndsWith(".chatgpt.com",
                StringComparison.OrdinalIgnoreCase))) return "other";
        if (uri.AbsolutePath.Contains("conversation", StringComparison.OrdinalIgnoreCase))
            return "conversation";
        if (uri.Scheme == "wss") return "websocket";
        return "other";
    }

    internal async Task InitializeAsync()
    {
        Subscribe("Network.requestWillBeSent", RequestStarted);
        Subscribe("Network.responseReceived", ResponseReceived);
        Subscribe("Network.loadingFailed", LoadingFailed);
        Subscribe("Network.loadingFinished", LoadingFinished);
        Subscribe("Network.webSocketCreated", SocketCreated);
        Subscribe("Network.webSocketClosed", SocketClosed);
        Subscribe("Network.webSocketFrameReceived", SocketFrame);
        Subscribe("Network.webSocketFrameSent", SocketFrame);
        await _core.CallDevToolsProtocolMethodAsync("Network.enable", "{}");
        LiteEventLog.Write("lite_transport_initialized");
    }

    private void Subscribe(string name, Action<JsonElement> callback)
    {
        _core.GetDevToolsProtocolEventReceiver(name).DevToolsProtocolEventReceived += (_, e) =>
        {
            try
            {
                using var data = JsonDocument.Parse(e.ParameterObjectAsJson);
                callback(data.RootElement);
            }
            catch (Exception ex)
            {
                LiteEventLog.Write("lite_transport_callback_failed", new {
                    category = name, errorType = ex.GetType().Name
                });
            }
        };
    }

    private static string? Value(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value) ||
            value.ValueKind != JsonValueKind.String) return null;
        return value.GetString();
    }

    private void RequestStarted(JsonElement data)
    {
        string? requestId = Value(data, "requestId");
        if (requestId == null) return;
        if (!data.TryGetProperty("request", out var req)) return;
        string kind = Classify(Value(req, "url"));
        if (kind != "conversation") return;
        if (_requests.Count > 300) _requests.Clear();
        _requests[requestId] = kind;
        LiteEventLog.Write("chat_transport_request_start",
            new { channel = kind, method = Value(req, "method") == "POST" ? "POST" : "other" });
    }

    private void ResponseReceived(JsonElement data)
    {
        string? requestId = Value(data, "requestId");
        if (requestId == null || !_requests.ContainsKey(requestId) ||
            !data.TryGetProperty("response", out var response)) return;
        int status = response.TryGetProperty("status", out var code) &&
                     code.ValueKind == JsonValueKind.Number ? (int)code.GetDouble() : 0;
        LiteEventLog.Write("chat_transport_http_response", new {
            status, httpSuccess = status >= 200 && status < 400
        });
    }

    private void LoadingFailed(JsonElement data)
    {
        string? requestId = Value(data, "requestId");
        if (requestId == null || !_requests.TryRemove(requestId, out var channel)) return;
        string? errorText = Value(data, "errorText");
        // Chromium net error text is a category, not a private URL.
        LiteEventLog.Write("chat_transport_loading_failed", new {
            channel, chromiumError = errorText is { Length: < 100 } &&
                errorText.StartsWith("net::", StringComparison.Ordinal) ? errorText : "unknown",
            canceled = data.TryGetProperty("canceled", out var canceled) &&
                       canceled.ValueKind == JsonValueKind.True
        });
    }

    private void LoadingFinished(JsonElement data)
    {
        string? requestId = Value(data, "requestId");
        if (requestId == null || !_requests.TryRemove(requestId, out var channel)) return;
        // This proves transport completed, not that React displayed the answer.
        LiteEventLog.Write("chat_transport_loading_finished", new { channel });
    }

    private void SocketCreated(JsonElement data)
    {
        string? id = Value(data, "requestId");
        if (id == null || Classify(Value(data, "url")) is not ("conversation" or "websocket"))
            return;
        if (_websocketFrameCount.Count > 100) _websocketFrameCount.Clear();
        _websocketFrameCount[id] = 0;
        LiteEventLog.Write("chat_websocket_open");
    }

    private void SocketClosed(JsonElement data)
    {
        string? id = Value(data, "requestId");
        if (id == null || !_websocketFrameCount.TryRemove(id, out var frames)) return;
        LiteEventLog.Write("chat_websocket_close", new { framesSeen = frames });
    }

    private void SocketFrame(JsonElement data)
    {
        string? id = Value(data, "requestId");
        if (id == null || !_websocketFrameCount.ContainsKey(id)) return;
        int count = _websocketFrameCount.AddOrUpdate(id, 1, (_, previous) => previous + 1);
        if (count == 1 || count % 100 == 0)
            LiteEventLog.Write("chat_websocket_frame_seen", new { frameCount = count });
    }
}
