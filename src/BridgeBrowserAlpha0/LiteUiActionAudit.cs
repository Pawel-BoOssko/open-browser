using System.Diagnostics;

namespace BridgeBrowserAlpha0;

/// <summary>Low-content UI action audit. Invocation is not proof of effect.</summary>
internal static class LiteUiActionAudit
{
    internal static string Requested(string action, string source = "winforms_owner")
    {
        string id = Guid.NewGuid().ToString("N");
        LiteEventLog.Write("ui_action_requested", new {
            actionId = id, action, source, actor = "owner_ui",
            monotonicTicks = Stopwatch.GetTimestamp(),
            pid = Environment.ProcessId
        });
        return id;
    }

    internal static void Executed(string actionId, string action, string result)
    {
        LiteEventLog.Write("ui_action_executed", new {
            actionId, action, result,
            monotonicTicks = Stopwatch.GetTimestamp(),
            effectConfirmed = false
        });
    }

    internal static void Result(string actionId, string action, string result,
        bool verified)
    {
        LiteEventLog.Write("ui_action_result", new {
            actionId, action, result, verified,
            monotonicTicks = Stopwatch.GetTimestamp()
        });
    }
}
