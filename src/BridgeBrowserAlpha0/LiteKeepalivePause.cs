using System.Text.Json;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Explicit owner pause of the EXISTING Windows Keepalive.
/// File presence wins, so even a truncated marker must not launch the browser.
/// Manual startup is always allowed. No new scheduler or background loop.
/// </summary>
internal static class LiteKeepalivePause
{
    internal static string MarkerPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenBrowser", "ChatGPT-Lite", "diagnostics",
            "keepalive-paused.json");

    internal static bool IsPaused(string? path = null) =>
        File.Exists(path ?? MarkerPath);

    internal static void SetPaused(bool paused, string? path = null)
    {
        path ??= MarkerPath;
        if (!paused)
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }
        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("No pause marker directory");
        Directory.CreateDirectory(directory);
        string temporary = path + ".writing-" + Guid.NewGuid().ToString("N");
        try
        {
            string content = JsonSerializer.Serialize(new
            {
                schema = 1,
                pausedAtUtc = DateTimeOffset.UtcNow,
                by = "owner"
            });
            File.WriteAllText(temporary, content);
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    internal static int SelfTest()
    {
        string dir = Path.Combine(Path.GetTempPath(),
            "obl_pause_test_" + Guid.NewGuid().ToString("N"));
        string file = Path.Combine(dir, "pause.json");
        try
        {
            if (IsPaused(file)) return 1;
            SetPaused(true, file);
            if (!IsPaused(file)) return 2;
            using (var doc = JsonDocument.Parse(File.ReadAllText(file)))
                if (doc.RootElement.GetProperty("schema").GetInt32() != 1)
                    return 3;
            // A partial write must also fail closed in Keepalive.
            File.WriteAllText(file, "{");
            if (!IsPaused(file)) return 4;
            SetPaused(false, file);
            if (IsPaused(file)) return 5;
            return 0;
        }
        catch { return 6; }
        finally
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch { }
        }
    }
}

internal sealed partial class ChatGptLiteForm
{
    private readonly Button _keepalivePauseButton = new()
    {
        Text = "Pauza i zamknij", Width = 149, Height = 29
    };

    private void InitializeKeepalivePauseUi()
    {
        UpdateKeepalivePauseUi();
        _keepalivePauseButton.Click += (_, _) => ToggleKeepalivePause();
    }

    private void UpdateKeepalivePauseUi()
    {
        bool paused = LiteKeepalivePause.IsPaused();
        _keepalivePauseButton.Text = paused
            ? "Wznów autostart" : "Pauza i zamknij";
        _keepalivePauseButton.BackColor = paused
            ? Color.FromArgb(255, 238, 201)
            : SystemColors.Control;
        _keepalivePauseButton.UseVisualStyleBackColor = !paused;
        _keepalivePauseButton.AccessibleDescription = paused
            ? "Wznawia automatyczny nadzór i uruchamianie aplikacji."
            : "Zapisuje pauzę nadzoru, bezpiecznie zamyka aplikację. " +
              "Keepalive nie otworzy jej ponownie do ręcznego wznowienia.";
    }

    private void ToggleKeepalivePause()
    {
        if (LiteKeepalivePause.IsPaused())
        {
            try
            {
                LiteKeepalivePause.SetPaused(false);
                LiteEventLog.Write("keepalive_resumed_by_user");
            }
            catch (Exception ex)
            {
                LiteEventLog.Write("keepalive_resume_failed",
                    new { errorType = ex.GetType().Name });
                MessageBox.Show(this, "Nie udało się wznowić autostartu.",
                    "Open Browser Lite", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            UpdateKeepalivePauseUi();
            return;
        }
        PauseAndClose();
    }

    private void PauseAndClose()
    {
        if (!FlushNotes())
        {
            MessageBox.Show(this,
                "Notatki nie zostały zapisane. Nie zamykam aplikacji.",
                "Open Browser Lite", MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }
        try
        {
            LiteKeepalivePause.SetPaused(true);
            LiteEventLog.Write("keepalive_paused_by_user");
            UpdateKeepalivePauseUi();
            Close();
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("keepalive_pause_failed",
                new { errorType = ex.GetType().Name });
            MessageBox.Show(this, "Nie udało się wstrzymać autostartu. " +
                "Aplikacja pozostaje uruchomiona.",
                "Open Browser Lite", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
