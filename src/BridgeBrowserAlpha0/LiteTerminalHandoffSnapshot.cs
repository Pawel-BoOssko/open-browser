using System.Security.Cryptography;
using System.Text;

namespace BridgeBrowserAlpha0;

/// <summary>
/// Durable, read-only-source emergency recovery package for a terminal
/// ChatGPT conversation. Never treats a visible assistant answer as durable.
/// No send, new chat creation, or lead promotion is performed here.
/// </summary>
internal static class LiteTerminalHandoffSnapshot
{
    internal static (bool Saved, string Path, string Reason) Save(
        string chatUrl, long wakeSequence, string evidenceSource)
    {
        const string projectRoot = @"D:\projects\open-browser";
        string docs = System.IO.Path.Combine(projectRoot, "docs");
        string output = System.IO.Path.Combine(
            docs, "OPENBROWSER_LITE_EMERGENCY_HANDOFF_CURRENT.md");
        string checkpoint = System.IO.Path.Combine(
            docs, "OPENBROWSER_LITE_CHECKPOINT.md");
        string handoff = System.IO.Path.Combine(
            docs, "OPENBROWSER_LITE_AUTO_HANDOFF_CURRENT.md");
        string backlog = System.IO.Path.Combine(docs, "OBL_BACKLOG.md");
        try
        {
            if (!LiteDevWakeState.IsChatUrl(chatUrl))
                return (false, output, "invalid_chat_url");
            var project = LiteProjectRegistry.OpenDefault().Projects
                .FirstOrDefault(p => p.Key == "obl" &&
                    p.Chats.Any(c => c.Role == "lead" && c.Url ==
                        LiteDevWakeState.NormalizeUrl(chatUrl)));
            if (project is null)
                return (false, output, "not_obl_lead");
            if (!File.Exists(checkpoint) || !File.Exists(backlog))
                return (false, output, "required_files_missing");

            // A model-authored handoff may be stale. Keep its content with
            // provenance and supplement it with the CURRENT local checkpoint.
            var sb = new StringBuilder();
            sb.AppendLine("# OBL — AWARYJNY PAKIET HANDOFF (AUTOMATYCZNY)");
            sb.AppendLine();
            sb.AppendLine("Utworzono UTC: " + DateTimeOffset.UtcNow.ToString("O"));
            sb.AppendLine("Zrodlo: " + evidenceSource);
            sb.AppendLine("Poprzedni lead: " + chatUrl);
            sb.AppendLine("Ostatnia sekwencja wake: " + wakeSequence);
            sb.AppendLine("Stan: TERMINALNY LIMIT CZATU; NIE POTWIERDZONO NOWEGO LEADA.");
            sb.AppendLine("Pakiet utworzony z plikow lokalnych, a nie z niepewnego DOM.");
            sb.AppendLine("Nowy czat: przeczytaj AGENTS.md, zasady operacyjne,");
            sb.AppendLine("ponizszy checkpoint, handoff i backlog. Potwierdz nowy");
            sb.AppendLine("trwaly URL /c/, jednego leada i przelaczenie wybudzania.");
            sb.AppendLine("Nie ponawiaj niepewnych wysylek i nie uznawaj");
            sb.AppendLine("utworzenia tego pliku za potwierdzenie promocji.");
            foreach (var item in new[] {
                (Path: checkpoint, Title: "AKTUALNY CHECKPOINT"),
                (Path: handoff, Title: "WCZESNIEJSZY HANDOFF — MOZLIWIE STARY"),
                (Path: backlog, Title: "KANONICZNY BACKLOG")
            })
            {
                sb.AppendLine();
                sb.AppendLine("## " + item.Title);
                sb.AppendLine("Zrodlo: " + item.Path);
                sb.AppendLine();
                sb.AppendLine(File.Exists(item.Path)
                    ? File.ReadAllText(item.Path, Encoding.UTF8)
                    : "(brak pliku)");
            }
            string text = sb.ToString();
            Directory.CreateDirectory(docs);
            string tmp = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tmp, text, new UTF8Encoding(false));
                File.Move(tmp, output, true);
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
            var actual = File.ReadAllText(output, Encoding.UTF8);
            if (actual != text) return (false, output, "verification_mismatch");
            return (true, output, "verified");
        }
        catch (Exception ex)
        {
            return (false, output, ex.GetType().Name);
        }
    }
}
