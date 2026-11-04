namespace BridgeBrowserAlpha0;

// Human-readable wake state for the SELECTED project, read fresh on request.
// Scheduler registry values are telemetry, not proof of message delivery.
internal sealed partial class LiteProjectDashboard
{
    private void ShowSelectedWakeStatus()
    {
        try
        {
            var project = SelectedProject()
                ?? throw new InvalidOperationException("Wybierz projekt.");
            using var dialog = new Form
            {
                Text = "Stan wybudzen: " + project.Title,
                Width = 670, Height = 430,
                MinimumSize = new Size(540, 340),
                StartPosition = FormStartPosition.CenterParent,
                Font = new Font("Segoe UI", 10f)
            };
            var summary = new TextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = true,
                BackColor = SystemColors.Window,
                Font = new Font("Consolas", 10f)
            };
            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 55,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8)
            };
            var close = new Button { Text = "Zamknij", Width = 105, Height = 32 };
            var refresh = new Button { Text = "Odswiez stan", Width = 120, Height = 32 };
            close.Click += (_, _) => dialog.Close();
            refresh.Click += (_, _) => LoadStatus();
            footer.Controls.Add(close);
            footer.Controls.Add(refresh);
            dialog.Controls.Add(summary);
            dialog.Controls.Add(footer);

            void LoadStatus()
            {
                try
                {
                    // Read both registries anew. Never copy a status from a
                    // different project whose sequence number happens to match.
                    var fresh = LiteProjectRegistry.OpenDefault().Projects
                        .SingleOrDefault(p => p.Id == project.Id)
                        ?? throw new InvalidDataException("Projekt nie istnieje.");
                    var lead = fresh.Chats.SingleOrDefault(c => c.Role == "lead");
                    var wake = lead is null ? null : LiteChatWakeRegistry.Read()
                        .SingleOrDefault(w => w.Url == lead.Url);
                    string When(DateTimeOffset time) => time == default
                        ? "brak danych"
                        : time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz");
                    summary.Text = string.Join(Environment.NewLine, new[]
                    {
                        "PROJEKT",
                        "Nazwa: " + fresh.Title,
                        "Klucz: " + fresh.Key,
                        "Czat glowny: " + (lead?.Title ?? "brak"),
                        "",
                        "HARMONOGRAM",
                        "Wlaczony: " + (wake?.Enabled == true ? "TAK" : "NIE"),
                        "Interwal: " + (wake?.EffectiveMinutes.ToString() ?? "brak") + " min",
                        "Nastepny termin: " + (wake?.Enabled == true
                            ? When(wake.DueUtc) : "brak aktywnego terminu"),
                        "",
                        "OSTATNIE DZIALANIE",
                        "Numer w rejestrze: " + (wake?.WakeSequence.ToString() ?? "brak"),
                        "Ostatnia proba: " + (wake is null
                            ? "brak danych" : When(wake.LastAttemptUtc)),
                        "Stan silnika: " + (wake?.LastState ?? "brak danych"),
                        "Handoff: " + (string.IsNullOrWhiteSpace(wake?.HandoffPhase)
                            ? "brak" : wake.HandoffPhase),
                        "",
                        "WYJASNIENIE TERMINU I ODRACZANIA",
                        LiteWakeStatusExplanation.Display(wake),
                        "Ostatnia planowana proba: " + (wake is null
                            ? "brak" : When(wake.LastDeferralPreviousDueUtc)),
                        "",
                        "Stan silnika i czas proby nie sa potwierdzeniem",
                        "odbioru wiadomosci przez ChatGPT."
                    });
                }
                catch (Exception ex)
                {
                    summary.Text = "Blad odczytu: " + ex.GetType().Name
                        + ": " + ex.Message;
                }
            }

            LoadStatus();
            dialog.ShowDialog(this);
        }
        catch (Exception ex) { ShowError("Stan wybudzen", ex); }
    }
}
