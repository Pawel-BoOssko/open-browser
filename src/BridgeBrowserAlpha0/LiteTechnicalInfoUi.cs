using System.Text.Json;

namespace BridgeBrowserAlpha0;

internal sealed partial class ChatGptLiteForm
{
    private readonly Button _technicalInfoButton = new()
    {
        Text = "Techniczne", Width = 94
    };
    private string _webViewRuntimeVersion = "nieuruchomiony";

    private void InitializeTechnicalInfoUi()
    {
        _technicalInfoButton.Click += (_, _) =>
        {
            MessageBox.Show(this, ReadTechnicalInfo(),
                "Open Browser Lite: informacje techniczne",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
    }

    private string ReadTechnicalInfo()
    {
        var directory = AppContext.BaseDirectory;
        var manifest = Path.Combine(directory, "openbrowser-release-manifest.json");
        var channel = "UNKNOWN (brak manifestu)";
        var commit = "nieokreślony";
        var manifestVersion = "brak";
        var digest = "brak";
        try
        {
            if (File.Exists(manifest))
            {
                using var parsed = JsonDocument.Parse(File.ReadAllText(manifest));
                var root = parsed.RootElement;
                if (root.TryGetProperty("channel", out var ch))
                    channel = ch.GetString() ?? channel;
                if (root.TryGetProperty("sourceCommit", out var src))
                    commit = src.GetString() ?? commit;
                if (root.TryGetProperty("version", out var ver))
                    manifestVersion = ver.GetString() ?? manifestVersion;
                if (root.TryGetProperty("executableSha256", out var sha))
                    digest = sha.GetString() ?? digest;
            }
        }
        catch (Exception ex)
        {
            channel = "MANIFEST_ERROR";
            LiteEventLog.Write("technical_identity_read_failed",
                new { errorType = ex.GetType().Name });
        }
        var localData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        var profile = Path.Combine(localData, "OpenBrowser", "ChatGPT-Lite", "Profile");
        return "Build: " + LiteBuildInfo.Display() + Environment.NewLine +
            "Wydanie: " + channel + Environment.NewLine +
            "Manifest version: " + manifestVersion + Environment.NewLine +
            "Source SHA: " + commit + Environment.NewLine +
            "EXE SHA256: " + digest + Environment.NewLine +
            "WebView2 runtime: " + _webViewRuntimeVersion + Environment.NewLine +
            "Profil WebView2: " + profile + Environment.NewLine +
            "Katalog instalacji: " + directory;
    }
}
