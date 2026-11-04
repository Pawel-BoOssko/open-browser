namespace BridgeBrowserAlpha0;

// The dashboard only changes which projects are shown. It does not change
// the project registry, active lead or any wake schedule.
internal static class LiteProjectDashboardFilter
{
    internal static bool Matches(string title, string key, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;
        string searchable = (title ?? "") + " " + (key ?? "");
        return query.Split(' ', StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries).All(term =>
                searchable.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}