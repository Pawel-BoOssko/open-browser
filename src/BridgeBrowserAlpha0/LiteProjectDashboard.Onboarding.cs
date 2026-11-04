namespace BridgeBrowserAlpha0;

internal sealed partial class LiteProjectDashboard
{
    private void OnboardProject()
    {
        try
        {
            var (url, chatTitle) = _currentChat();
            var db = LiteProjectRegistry.OpenDefault();
            bool validChat = Uri.TryCreate(url, UriKind.Absolute, out var candidate)
                && candidate.Scheme == Uri.UriSchemeHttps
                && string.Equals(candidate.Host, "chatgpt.com", StringComparison.OrdinalIgnoreCase)
                && (candidate.AbsolutePath.StartsWith("/c/", StringComparison.OrdinalIgnoreCase)
                    || candidate.AbsolutePath.StartsWith("/g/", StringComparison.OrdinalIgnoreCase));
            bool unassigned = validChat && !db.Projects.Any(p =>
                p.Chats.Any(c => string.Equals(
                    c.Url, candidate!.GetLeftPart(UriPartial.Path), StringComparison.OrdinalIgnoreCase)));
            using var dialog = new Form
            {
                Text = "Nowy projekt", Width = 520, Height = 264,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false, MinimizeBox = false,
                ShowInTaskbar = false
            };
            var label = new Label
            {
                Text = "Nazwa projektu", Left = 18, Top = 16, Width = 460, Height = 24
            };
            var name = new TextBox
            {
                Left = 18, Top = 43, Width = 467, MaxLength = 120
            };
            var attach = new CheckBox
            {
                Left = 18, Top = 87, Width = 467, Height = 34,
                Text = "Dodaj otwarty czat jako glowny", Enabled = unassigned,
                Checked = unassigned
            };
            var hint = new Label
            {
                Left = 18, Top = 126, Width = 467, Height = 50,
                AutoEllipsis = true, Text = unassigned
                    ? "Zaznaczona opcja: jeden nowy projekt i jego lead, bez wlaczania wybudzen. Pusta nazwa pobierze tytul czatu."
                    : "Otwarty czat nie jest dostepny lub jest juz przypisany. Mozesz utworzyc pusty projekt."
            };
            var ok = new Button
            {
                Text = "Utworz", Left = 286, Top = 180,
                Width = 94, DialogResult = DialogResult.OK
            };
            var cancel = new Button
            {
                Text = "Anuluj", Left = 391, Top = 180,
                Width = 94, DialogResult = DialogResult.Cancel
            };
            dialog.Controls.AddRange(new Control[] { label, name, attach, hint, ok, cancel });
            dialog.AcceptButton = ok;
            dialog.CancelButton = cancel;
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            if (!attach.Checked && string.IsNullOrWhiteSpace(name.Text))
                throw new ArgumentException("Podaj nazwe pustego projektu.");
            var saved = attach.Checked
                ? db.AddProjectWithCurrentChat(name.Text, chatTitle, url, requireCurrentChat: true)
                : db.AddProject(name.Text);
            // Do not arm a schedule here. Project ownership is independent
            // from permission to send a later wake.
            _selectedId = saved.Id;
            LiteEventLog.Write("project_dashboard_onboarded", new
            {
                projectKey = saved.Key, leadAttached = attach.Checked
            });
            RefreshProjects();
        }
        catch (Exception ex) { ShowError("Nowy projekt", ex); }
    }
}
