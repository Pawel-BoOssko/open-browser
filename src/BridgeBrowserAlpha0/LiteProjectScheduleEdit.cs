namespace BridgeBrowserAlpha0;

// Project dashboard schedule edits. An unchanged save must never rephase
// the wake clock; the scheduler (not the settings dialog) owns DueUtc.
internal static class LiteProjectScheduleEdit
{
    internal static bool Apply(
        List<LiteChatWakeRegistry.Chat> schedules, string leadUrl,
        bool enabled, int minutes, DateTimeOffset nowUtc)
    {
        if (minutes is < 15 or > 1440)
            throw new ArgumentOutOfRangeException(nameof(minutes));
        leadUrl = LiteDevWakeState.NormalizeUrl(leadUrl);
        var wake = schedules.SingleOrDefault(w => w.Url == leadUrl);
        if (!enabled)
        {
            if (wake is null || !wake.Enabled) return false;
            wake.Enabled = false;
            wake.LastState = "dashboard_paused";
            return true;
        }
        if (wake is null)
        {
            LiteChatWakeRegistry.Configure(schedules, leadUrl, minutes,
                "Kontynuuj prace w projekcie zgodnie z ustalonym planem.", nowUtc);
            return true;
        }
        if (wake.Enabled && wake.EffectiveMinutes == minutes) return false;
        wake.Minutes = Math.Max(20, minutes);
        wake.FastMinutes = minutes < 20 ? minutes : 0;
        wake.Enabled = true;
        wake.DueUtc = nowUtc.AddMinutes(minutes);
        wake.SlotAnchorUtc = wake.DueUtc;
        wake.LastState = "dashboard_armed";
        return true;
    }
}