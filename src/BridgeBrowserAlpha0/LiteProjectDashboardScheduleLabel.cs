namespace BridgeBrowserAlpha0;

// Compact schedule metadata for project list rows. This is not a proof of
// successful message delivery, and it does not modify the wake registry.
internal static class LiteProjectDashboardScheduleLabel
{
    internal static string Format(bool hasLead, bool enabled,
        int intervalMinutes, DateTimeOffset dueUtc)
    {
        if (!hasLead) return "brak leada";
        if (!enabled) return "wstrzymany";
        if (intervalMinutes < 1 || dueUtc == default)
            return "wlaczony, termin nieznany";
        return "co " + intervalMinutes + " min, " +
            dueUtc.ToLocalTime().ToString("dd.MM HH:mm");
    }
}