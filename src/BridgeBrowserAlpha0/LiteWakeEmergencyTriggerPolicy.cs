namespace BridgeBrowserAlpha0;

internal static class LiteWakeEmergencyTriggerPolicy
{
    // A transient page shell/error does not mean the current conversation
    // has exhausted its length. Only a confirmed length error starts handoff.
    internal static bool StartsAutomaticHandoff(string error) =>
        error == "conversation_length_exceeded";
}
