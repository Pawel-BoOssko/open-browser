using System.Security.Cryptography;
using System.Text;

namespace BridgeBrowserAlpha0;

// Creates an on-demand, human-readable instruction for the NEXT chat.
// No browser actions, role change, schedule change, or file mutation.
internal static class LiteHandoffResumePacket
{
    private const string DocsRoot = @"D:\projects\open-browser\docs";
    private const string CurrentCheckpoint =
        @"D:\projects\open-browser\docs\OPENBROWSER_LITE_CHECKPOINT.md";
    private const string CurrentHandoff =
        @"D:\projects\open-browser\docs\OPENBROWSER_LITE_AUTO_HANDOFF_CURRENT.md";

    private static string? FindValue(string content, string prefix) =>
        content.Split('\n').Select(x => x.TrimEnd('\r'))
            .FirstOrDefault(x => x.StartsWith(prefix, StringComparison.Ordinal))
            ?[prefix.Length..].Trim();

    // Atomic user-selected export. Verify bytes after rename before reporting success.
    internal static string ExportVerified(string destination)
    {
        string full = Path.GetFullPath(destination);
        string? directory = Path.GetDirectoryName(full);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            throw new DirectoryNotFoundException("Export directory does not exist");
        byte[] expected = new UTF8Encoding(false).GetBytes(Compose());
        string temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, expected);
            if (!File.ReadAllBytes(temporary).AsSpan().SequenceEqual(expected))
                throw new IOException("Temporary export readback mismatch");
            File.Move(temporary, full, overwrite: true);
            if (!File.ReadAllBytes(full).AsSpan().SequenceEqual(expected))
                throw new IOException("Export readback mismatch");
            return Convert.ToHexString(SHA256.HashData(expected));
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
    internal static string Compose()
    {
        var checkpoint = new FileInfo(CurrentCheckpoint);
        if (!checkpoint.Exists || checkpoint.Length == 0 ||
            checkpoint.Length > 1_048_576)
            throw new InvalidDataException("Current checkpoint is missing or invalid");

        string currentHash = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(CurrentCheckpoint))).ToLowerInvariant();
        string? savedAt = null;
        string? previousUrl = null;
        string? lastWake = null;
        string freshness = "Brak migawki HANDOFF: uzyj aktualnego checkpointu";
        if (File.Exists(CurrentHandoff))
        {
            var handoff = new FileInfo(CurrentHandoff);
            if (handoff.Length > 0 && handoff.Length <= 1_048_576)
            {
                string snapshot = File.ReadAllText(CurrentHandoff, Encoding.UTF8);
                if (snapshot.Contains("Format: OBL_HANDOFF_SNAPSHOT_V2",
                    StringComparison.Ordinal))
                {
                    savedAt = FindValue(snapshot, "Saved UTC: ");
                    previousUrl = FindValue(snapshot, "Current chat URL: ");
                    lastWake = FindValue(snapshot, "Last wake: ");
                    string hash = FindValue(snapshot, "Checkpoint SHA256: ") ?? "";
                    freshness = string.Equals(hash, currentHash,
                        StringComparison.OrdinalIgnoreCase)
                        ? "Migawka zgodna z checkpointem"
                        : "Migawka HISTORYCZNA: nie traktuj jej jako najnowszego stanu";
                }
            }
        }
        var text = new StringBuilder();
        text.AppendLine("KONTYNUACJA PROJEKTU OPEN BROWSER LITE");
        text.AppendLine("Host: SATANAMD. SOURCE: D:\\projects\\open-browser");
        text.AppendLine("Wersja uruchomionego OBL: " + Application.ProductVersion);
        text.AppendLine("Sciezka uruchomionego EXE: " +
            (Environment.ProcessPath ?? "nieustalona"));
        text.AppendLine("Dane wygenerowano UTC: " +
            DateTimeOffset.UtcNow.ToString("O"));
        text.AppendLine("Aktualny checkpoint: " + CurrentCheckpoint);
        text.AppendLine("Checkpoint SHA256: " + currentHash);
        text.AppendLine("HANDOFF: " + CurrentHandoff);
        text.AppendLine("Stan HANDOFF: " + freshness);
        text.AppendLine("HANDOFF zapis UTC: " + (savedAt ?? "nieustalony"));
        text.AppendLine("Ostatni wake w HANDOFF: " + (lastWake ?? "nieustalony"));
        text.AppendLine("URL poprzedniego leada wg HANDOFF: " +
            (previousUrl ?? "nieustalony"));
        text.AppendLine("Backlog: " + Path.Combine(DocsRoot, "OBL_BACKLOG.md"));
        text.AppendLine("Zasady: D:\\projects\\open-browser\\AGENTS.md");
        text.AppendLine();
        text.AppendLine("1. Sprawdz COMPUTERNAME=SATANAMD. Nie uzywaj PBOX1.");
        text.AppendLine("2. Przeczytaj CALY aktualny checkpoint i zasady pracy.");
        text.AppendLine("3. Odczytaj Beads poleceniem scripts\\obl-beads.ps1 -Action scan;");
        text.AppendLine("   porownaj z backlogiem, wybierz pierwszy NIEZAKONCZONY etap.");
        text.AppendLine("4. Ustal aktualne LIVE z Release Managera i procesu, nie ze starej migawki.");
        text.AppendLine("5. Kontynuuj zmiane kodu i wdrozenie, bez ponawiania zaliczonych testow.");
        text.AppendLine("6. Zachowaj jednego aktywnego leada na projekt; nie przejmuj");
        text.AppendLine("   automatycznie harmonogramu, nie ponawiaj UNCERTAIN.");
        text.AppendLine("7. Zapisz checkpoint po pracy z potwierdzonym stanem LIVE/SOURCE.");
        return text.ToString();
    }
}