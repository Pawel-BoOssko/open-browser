using Microsoft.Web.WebView2.Core;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    // Pure request classification: never log user IDs, chat IDs, URL query,
    // request/response bodies, headers, tokens or cookies.
    internal static (string Endpoint, string PathKind, string HttpMethod)
        DescribeBackendRequest(string? url, string? requestMethod)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != "https" ||
            !string.Equals(uri.Host, "chatgpt.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.StartsWith("/backend-api/", StringComparison.OrdinalIgnoreCase))
            return ("ignore", "ignore", "other");

        string path = uri.AbsolutePath;
        string kind =
            path.Equals("/backend-api/conversation", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/backend-api/conversation/", StringComparison.OrdinalIgnoreCase)
                ? "conversation_root" :
            path.StartsWith("/backend-api/conversations", StringComparison.OrdinalIgnoreCase)
                ? "conversation_list" :
            path.StartsWith("/backend-api/conversation/", StringComparison.OrdinalIgnoreCase)
                ? "conversation_detail" :
            path.Contains("conversation", StringComparison.OrdinalIgnoreCase)
                ? "conversation_other" : "other_backend";
        string method = requestMethod is "GET" or "POST" or "PATCH" or "PUT" or "DELETE"
            ? requestMethod : "other";
        return (kind.StartsWith("conversation", StringComparison.Ordinal)
            ? "conversation" : "other_backend", kind, method);
    }

    private static void LogBackendResponseMetadata(
        CoreWebView2WebResourceResponseReceivedEventArgs e, string webviewRole)
    {
        try
        {
            var info = DescribeBackendRequest(e.Request.Uri, e.Request.Method);
            if (info.Endpoint == "ignore") return;
            int status = e.Response.StatusCode;
            if (status < 400 && info.Endpoint != "conversation") return;
            LiteEventLog.Write("web_backend_response", new
            {
                endpoint = info.Endpoint,
                status,
                successHttp = status is >= 200 and < 400,
                method = info.HttpMethod,
                pathClass = info.PathKind,
                webview = webviewRole
            });
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("response_metadata_error",
                new { errorType = ex.GetType().Name, webview = webviewRole });
        }
    }
}
