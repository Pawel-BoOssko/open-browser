namespace BridgeBrowserAlpha0;

/// <summary>
/// Project-specific commands to the existing, conservative handoff handlers.
/// This dashboard does not duplicate wake or rotation state transitions.
/// </summary>
internal sealed partial class LiteProjectDashboard
{
    private void RequestManualHandoff()
    {
        try
        {
            var project = SelectedProject()
                ?? throw new InvalidOperationException("Wybierz projekt.");
            var lead = project.Chats.SingleOrDefault(c => c.Role == "lead")
                ?? throw new InvalidOperationException("Brak czatu glownego.");
            var wake = LiteChatWakeRegistry.Read()
                .SingleOrDefault(c => c.Url == lead.Url)
                ?? throw new InvalidOperationException("Brak harmonogramu leada.");
            if (!wake.Enabled)
                throw new InvalidOperationException(
                    "Wybudzenia projektu sa wstrzymane. Nie mozna zlecic HANDOFF.");
            if (!string.IsNullOrEmpty(wake.HandoffPhase))
                throw new InvalidOperationException(
                    "HANDOFF juz jest w toku: " + wake.HandoffPhase);

            var approval = MessageBox.Show(this,
                "Projekt: " + project.Title
                + "\nCzat glowny: " + lead.Title
                + "\n\nZlecic zapis HANDOFF podczas kolejnego bezpiecznego wybudzenia?"
                + "\nNie zostanie utworzony nowy czat ani uruchomiony drugi wykonawca.",
                "Zlec HANDOFF projektu",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (approval != DialogResult.Yes) return;

            // Same action and safeguards as the old advanced sidebar.
            _requestHandoff(project.Id);
            RefreshProjects();
        }
        catch (Exception ex) { ShowError("Zlecenie HANDOFF", ex); }
    }

    private void TakeOverAfterHandoff()
    {
        try
        {
            var project = SelectedProject()
                ?? throw new InvalidOperationException("Wybierz projekt.");
            var lead = project.Chats.SingleOrDefault(c => c.Role == "lead")
                ?? throw new InvalidOperationException("Brak starego leada.");
            var wake = LiteChatWakeRegistry.Read()
                .SingleOrDefault(c => c.Url == lead.Url)
                ?? throw new InvalidOperationException("Brak harmonogramu starego leada.");
            var (openedUrl, _) = _currentChat();
            var normalized = LiteDevWakeState.NormalizeUrl(openedUrl ?? "");
            if (!LiteChatHandoff.CanTakeOver(wake, normalized))
                throw new InvalidOperationException(
                    "Nie mozna przejac rozmowy: wymagane potwierdzone WAIT_USER, "
                    + "wstrzymany stary wykonawca i inny prawidlowy adres czatu.");

            var approval = MessageBox.Show(this,
                "Projekt: " + project.Title
                + "\nDotychczasowy lead: " + lead.Title
                + "\nNowy czat: " + normalized
                + "\n\nPrzejac ten czat jako wykonawce?"
                + "\nTa operacja moze uruchomic harmonogram nowego leada.",
                "Potwierdz przejecie czatu",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (approval != DialogResult.Yes) return;

            // The legacy operation re-reads all state and re-checks
            // LiteChatHandoff.CanTakeOver at execution time.
            _takeOver(project.Id);
            RefreshProjects();
        }
        catch (Exception ex) { ShowError("Przejecie czatu", ex); }
    }
}
