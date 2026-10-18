namespace BridgeBrowserAlpha0;

internal static class LiteTransportSelfTest
{
    internal static int Run()
    {
        try
        {
            Require(LiteTransportDiagnostics.Classify(
                "https://chatgpt.com/backend-api/conversation/abc?token=secret") ==
                "conversation", "conversation endpoint");
            Require(LiteTransportDiagnostics.Classify(
                "https://chatgpt.com/backend-api/f/conversation") == "conversation",
                "alternate conversation endpoint");
            Require(LiteTransportDiagnostics.Classify(
                "wss://ws.chatgpt.com/connect") == "websocket",
                "WebSocket transport");
            Require(LiteTransportDiagnostics.Classify(
                "https://chatgpt.com/settings") == "other", "unrelated endpoint");
            Require(LiteTransportDiagnostics.Classify(
                "https://fakechatgpt.com/backend-api/conversation") == "other",
                "fake host rejected");
            Require(LiteTransportDiagnostics.Classify(
                "http://chatgpt.com/backend-api/conversation") == "other",
                "insecure scheme rejected");
            LiteEventLog.Write("transport_selftest", new { passed = true, checks = 6 });
            return 0;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("transport_selftest",
                new { passed = false, errorType = ex.GetType().Name });
            return 2;
        }
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
    }
}
