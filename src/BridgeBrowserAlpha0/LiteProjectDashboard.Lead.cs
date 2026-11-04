namespace BridgeBrowserAlpha0;

// The manual lead change is an explicit project-level administrative action.
// A wake never silently changes its destination while staying enabled.
internal sealed partial class LiteProjectDashboard
{
    private sealed record LeadChoice(string Id, string Title, bool IsCurrent)
    {
        public override string ToString() =>
            (IsCurrent ? "[AKTUALNY] " : "") + Title;
    }

    private void ChangeSelectedLead()
    {
        try
        {
            var db = LiteProjectRegistry.OpenDefault();
            var project = db.Projects.SingleOrDefault(p => p.Id == _selectedId)
                ?? throw new InvalidOperationException("Wybierz projekt.");
            var current = project.Chats.SingleOrDefault(c => c.Role == "lead");
            if (project.Chats.Count == 0)
                throw new InvalidOperationException(
                    "Projekt nie ma czatow. Najpierw przypnij rozmowe.");

            using var dialog = new Form
            {
                Text = "Czat glowny: " + project.Title,
                Width = 530, Height = 290,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false, MaximizeBox = false,
                ShowInTaskbar = false
            };
            var note = new Label
            {
                Text = "Wybierz nowy czat glowny. Harmonogram projektu zostanie"
                    + " wstrzymany do recznego wznowienia.",
                Left = 16, Top = 15, Width = 480, Height = 58
            };
            var choices = new ListBox
            {
                Left = 16, Top = 74, Width = 480, Height = 105,
                IntegralHeight = false
            };
            foreach (var chat in project.Chats.OrderByDescending(c => c.AddedUtc))
                choices.Items.Add(new LeadChoice(chat.Id, chat.Title,
                    chat.Id == current?.Id));
            for (int i = 0; i < choices.Items.Count; i++)
                if ((choices.Items[i] as LeadChoice)?.IsCurrent == true)
                {
                    choices.SelectedIndex = i;
                    break;
                }
            if (choices.SelectedIndex < 0 && choices.Items.Count > 0)
                choices.SelectedIndex = 0;

            var confirm = new Button
            {
                Text = "Wybierz leada", Left = 270, Top = 195,
                Width = 130, DialogResult = DialogResult.OK
            };
            var cancel = new Button
            {
                Text = "Anuluj", Left = 407, Top = 195,
                Width = 89, DialogResult = DialogResult.Cancel
            };
            dialog.Controls.AddRange(new Control[] { note, choices, confirm, cancel });
            dialog.AcceptButton = confirm;
            dialog.CancelButton = cancel;
            if (dialog.ShowDialog(this) != DialogResult.OK ||
                choices.SelectedItem is not LeadChoice chosen ||
                chosen.Id == current?.Id)
                return;

            var decision = MessageBox.Show(this,
                "Nowy czat: " + chosen.Title + "\n\n"
                + "Zmiana wstrzyma wybudzanie TYLKO tego projektu. "
                + "Zachowana zostanie dotychczasowa numeracja i historia. "
                + "Po zmianie wybudzanie trzeba wlaczyc w pulpicie.",
                "Potwierdz zmiane leada",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (decision != DialogResult.Yes) return;

            // Recheck that the user approved the same project and leader.
            var fresh = LiteProjectRegistry.OpenDefault();
            var actual = fresh.Projects.SingleOrDefault(p => p.Id == project.Id)
                ?? throw new InvalidDataException("Projekt zniknal.");
            if (actual.Chats.SingleOrDefault(c => c.Role == "lead")?.Id
                != current?.Id ||
                !actual.Chats.Any(c => c.Id == chosen.Id))
                throw new InvalidDataException(
                    "W miedzyczasie zmieniono przypisanie czatow. Odswiez panel.");

            fresh.PromoteLeadPaused(project.Id, chosen.Id);
            var stored = LiteProjectRegistry.OpenDefault().Projects
                .Single(p => p.Id == project.Id);
            if (stored.Chats.Single(c => c.Role == "lead").Id != chosen.Id ||
                stored.Wake?["Enabled"]?.GetValue<bool>() == true)
                throw new InvalidDataException(
                    "Zapis leada lub wstrzymania wybudzen nie zostal potwierdzony.");
            LiteEventLog.Write("dashboard_project_lead_changed", new
            {
                projectKey = stored.Key, wakePaused = true
            });
            RefreshProjects();
            _feedback.Text = "Zmieniono leada. Wybudzanie tego projektu jest wstrzymane.";
        }
        catch (Exception ex) { ShowError("Zmiana leada", ex); }
    }
}
