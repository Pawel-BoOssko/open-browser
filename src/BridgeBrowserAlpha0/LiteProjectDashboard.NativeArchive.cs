namespace BridgeBrowserAlpha0;

internal sealed partial class LiteProjectDashboard
{
    private bool _nativeArchiveBusy;

    private async Task ArchiveSelectedProjectNativeAsync()
    {
        if (_nativeArchiveBusy) return;
        try
        {
            var project = SelectedProject()
                ?? throw new InvalidOperationException("Wybierz projekt.");
            if (project.Chats.Count(c => c.Role == "lead") != 1)
                throw new InvalidOperationException("Brak jednego czatu glownego.");
            var older = project.Chats.Where(c => c.Role != "lead")
                .OrderByDescending(c => c.AddedUtc)
                .ThenBy(c => c.Id, StringComparer.Ordinal).Skip(2).ToList();
            var pendingBackend = older.Where(c => !c.NativeArchivedUtc.HasValue &&
                Uri.TryCreate(c.Url, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps && uri.Host == "chatgpt.com" &&
                uri.AbsolutePath.StartsWith("/c/", StringComparison.OrdinalIgnoreCase) &&
                Guid.TryParseExact(uri.AbsolutePath["/c/".Length..], "D", out _)).ToList();
            int pendingNormalList = older.Count(c => c.NativeArchivedUtc.HasValue &&
                !c.NativeArchiveVisibilityVerifiedUtc.HasValue &&
                !c.NativeArchiveOwnerAccepted429Utc.HasValue);
            if (pendingBackend.Count == 0 && pendingNormalList == 0)
            {
                _feedback.Text = "Brak rozmow oczekujacych na archiwizacje lub potwierdzenie listy.";
                return;
            }
            if (MessageBox.Show(this,
                "Projekt: " + project.Title +
                "\nLead i dwa najnowsze poprzednie czaty pozostana na liscie." +
                "\nJeszcze niearchiwizowane na serwerze: " + pendingBackend.Count +
                "\nArchiwizowane na serwerze, ale nadal bez potwierdzenia znikniecia z listy: " +
                pendingNormalList +
                "\n\nWykonac archiwizacje i sprawdzic zwykla liste ChatGPT?" +
                "\nHTTP 429 po potwierdzonej archiwizacji = SUKCES OPERACYJNY (regula wlasciciela).",
                "Archiwizacja ChatGPT: weryfikacja zwyklej listy",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _nativeArchiveBusy = true;
            _feedback.Text = "Sprawdzam zwykla liste ChatGPT, a nie liste archiwum...";
            var result = await _archiveOlder(project.Id);
            RefreshProjects();
            _feedback.Text = "Znikniecie z listy potwierdzone: " +
                result.ConfirmedAbsentFromNormalList +
                ", 429 ZAAKCEPTOWANE: " + result.OwnerAccepted429 +
                ", nadal NIEPOTWIERDZONE: " + result.StillUnverified +
                ", nowe archiwizacje serwerowe: " + result.NewlyArchivedOnServer + ".";
        }
        catch (Exception ex)
        {
            ShowError("Archiwizacja w ChatGPT", ex);
            LiteEventLog.Write("project_dashboard_native_archive_error",
                new { errorType = ex.GetType().Name, message = ex.Message });
        }
        finally { _nativeArchiveBusy = false; }
    }
}
