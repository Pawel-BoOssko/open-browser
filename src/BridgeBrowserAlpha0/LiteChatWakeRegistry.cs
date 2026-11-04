using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BridgeBrowserAlpha0;

/// <summary>Independent, explicitly enabled wake schedules, encrypted per Windows user.</summary>
internal static class LiteChatWakeRegistry
{
    internal sealed class Chat
    {
        public string Url { get; set; } = "";
        public bool Enabled { get; set; }
        // Legacy field: always >=20 so any rollback to v1.72.59 can parse it.
        public int Minutes { get; set; } = 25;
        // Optional short interval, understood by newer clients only (15..19).
        // Old clients ignore it and continue with the safe legacy 20m value.
        public int FastMinutes { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int EffectiveMinutes => FastMinutes is >= 15 and < 20
            ? FastMinutes : Minutes;
        // Project-owned wake number, never reset when a chat is rotated.
        public long WakeSequence { get; set; }
        // Only the current project lead may attempt the next decade boundary.
        // Stored in projects.json together with WakeSequence (not in the chat).
        public string RotationPhase { get; set; } = "";
        public string RotationReceipt { get; set; } = "";
        public string RotationSeedSha256 { get; set; } = "";
        public string RotationServerUrl { get; set; } = "";
        public DateTimeOffset DueUtc { get; set; }
        // Stable clock phase. DueUtc may move for a brief retry; slot phase must not.
        public DateTimeOffset SlotAnchorUtc { get; set; }
        public DateTimeOffset LastAttemptUtc { get; set; }
        public DateTimeOffset RotationStartedUtc { get; set; }
        public string EmergencyReason { get; set; } = "";
        public string EmergencyPhase { get; set; } = "";
        public string EmergencyMessageId { get; set; } = "";
        public DateTimeOffset EmergencySentUtc { get; set; }
        public DateTimeOffset EmergencyDeadlineUtc { get; set; }
        public string EmergencyHandoffPath { get; set; } = "";
        public int EmergencyOrdinal { get; set; }
        public DateTimeOffset StallFirstDueUtc { get; set; }
        public string StallPhase { get; set; } = "";
        public DateTimeOffset StallResumeUtc { get; set; }
        public string LastState { get; set; } = "armed";
        // Current project-owned deferral evidence, persisted atomically with
        // DueUtc. Source is the actual engine decision, never UI inference.
        public string LastDeferralReason { get; set; } = "";
        public DateTimeOffset LastDeferralUtc { get; set; }
        public DateTimeOffset LastDeferralPreviousDueUtc { get; set; }
        public DateTimeOffset LastDeferralUntilUtc { get; set; }
        public int ConsecutiveDeferrals { get; set; }
        public string LastAnswerFingerprint { get; set; } = "";
        // Durable dedup for each NEW model-confirmed owner intervention.
        // Never implies an email was actually delivered.
        public string LastInterventionKey { get; set; } = "";
        // Optional, rollback-compatible one-shot handoff state.
        // Empty: normal; request_pending: prepare prompt; requested: no repeat.
        public string HandoffPhase { get; set; } = "";
        public string Prompt { get; set; } = "";
    }

    private sealed class Store
    {
        public int Schema { get; set; } = 1;
        public List<Chat> Chats { get; set; } = new();
    }

    private static readonly string PathOnDisk = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenBrowser", "ChatGPT-Lite", "chat-wakes.dpapi");

    internal static List<Chat> Read()
    {
        // New canonical source: project-owned schedules and their retired history.
        // When first launched from a legacy build, migration occurs on Write.
        var projectWakes = LiteProjectWakeStore.Read();
        if (projectWakes is not null) return projectWakes;
        if (!File.Exists(PathOnDisk))
        {
            var legacy = LiteDevWakeState.Read();
            if (LiteDevWakeState.IsChatUrl(legacy.TargetUrl))
            {
                var migration = new List<Chat> { new() {
                    Url = LiteDevWakeState.NormalizeUrl(legacy.TargetUrl),
                    Enabled = legacy.Enabled, Minutes = legacy.IntervalMinutes,
                    DueUtc = legacy.NextDueUtc > DateTimeOffset.UtcNow
                        ? legacy.NextDueUtc : DateTimeOffset.UtcNow.AddMinutes(2),
                    LastAttemptUtc = legacy.LastAttemptUtc,
                    LastState = "migrated", Prompt = LiteDevWakeState.WakeText
                }};
                Write(migration);
                return migration;
            }
            return new List<Chat>();
        }
        var cipher = File.ReadAllBytes(PathOnDisk);
        var clear = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
        try
        {
            var store = JsonSerializer.Deserialize<Store>(clear)
                ?? throw new InvalidDataException("Empty chat wake registry");
            if (store.Schema != 1 || store.Chats.Count > 48)
                throw new InvalidDataException("Invalid registry schema");
            var urls = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in store.Chats)
            {
                if (!LiteDevWakeState.IsChatUrl(c.Url) || !urls.Add(c.Url) ||
                    c.Minutes is < 20 or > 1440 ||
                    (c.FastMinutes != 0 && c.FastMinutes is < 15 or >= 20) ||
                    c.Prompt.Length > 16000 || c.WakeSequence < 0 ||
                    c.HandoffPhase is not ("" or "request_pending" or "requested") ||
                    (c.LastAnswerFingerprint.Length != 0 &&
                     (c.LastAnswerFingerprint.Length != 64 ||
                      !c.LastAnswerFingerprint.All(Uri.IsHexDigit))))
                    throw new InvalidDataException("Invalid scheduled chat");
            }
            if (!LiteProjectRegistry.CanScheduleProjectLeads(
                LiteProjectRegistry.OpenDefault().Projects, store.Chats))
                throw new InvalidDataException(
                    "Multiple project executors or archive wake detected");
            foreach (var c in store.Chats)
                if (c.SlotAnchorUtc == default) c.SlotAnchorUtc = c.DueUtc;
            return store.Chats;
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }

    internal static void Write(List<Chat> chats)
    {
        // A project's single lead is the only chat allowed to wake; a pinned
        // archival chat must never be silently promoted by a schedule edit.
        if (!LiteProjectRegistry.CanScheduleProjectLeads(
            LiteProjectRegistry.OpenDefault().Projects, chats))
            throw new InvalidOperationException(
                "One enabled lead chat per project is required");
        if (chats.Count > 48 || chats.Any(c =>
            !LiteDevWakeState.IsChatUrl(c.Url) || c.Minutes is < 20 or > 1440 ||
            (c.FastMinutes != 0 && c.FastMinutes is < 15 or >= 20) ||
            c.Prompt.Length > 16000 || c.WakeSequence < 0 ||
            c.HandoffPhase is not ("" or "request_pending" or "requested") ||
            (c.LastAnswerFingerprint.Length != 0 &&
             (c.LastAnswerFingerprint.Length != 64 ||
              !c.LastAnswerFingerprint.All(Uri.IsHexDigit)))) ||
            chats.Select(c => c.Url).Distinct(StringComparer.Ordinal).Count() != chats.Count)
            throw new InvalidDataException("Invalid chat wakes");
        var plain = JsonSerializer.SerializeToUtf8Bytes(new Store { Chats = chats });
        byte[] cipher;
        try { cipher = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser); }
        finally { CryptographicOperations.ZeroMemory(plain); }
        var dir = System.IO.Path.GetDirectoryName(PathOnDisk)!;
        Directory.CreateDirectory(dir);
        var tmp = PathOnDisk + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(tmp, cipher);
            File.Move(tmp, PathOnDisk, true);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
        // Rollback mirror retained only during transition: projects.json is authoritative.
        LiteProjectWakeStore.Write(chats);
    }

    // Only an explicit operator command may reset the fingerprint of a
    // previously CONFIRMED wake response. Never rearm a draft/uncertain send.
    // This is one fresh owner-authorized wake, with a new sequence and receipt.
    internal static bool TryOwnerRearmNow(List<Chat> chats, string url,
        DateTimeOffset now)
    {
        url = LiteDevWakeState.NormalizeUrl(url);
        var entry = chats.SingleOrDefault(c => c.Url == url);
        if (entry is null || !entry.Enabled ||
            entry.LastState != "waiting_same_answer_already_woken" ||
            entry.LastAnswerFingerprint.Length != 64 ||
            entry.WakeSequence <= 0)
            return false;
        entry.LastAnswerFingerprint = "";
        entry.DueUtc = now.AddSeconds(-1);
        entry.LastState = "owner_explicit_rearm_due";
        return true;
    }
    internal static Chat Configure(List<Chat> chats, string url, int minutes,
        string prompt, DateTimeOffset now)
    {
        url = LiteDevWakeState.NormalizeUrl(url);
        if (minutes is < 15 or > 1440 || string.IsNullOrWhiteSpace(prompt) ||
            prompt.Length > 16000) throw new ArgumentException("Invalid wake options");
        var entry = chats.FirstOrDefault(c => c.Url == url);
        if (entry is null) { entry = new Chat { Url = url }; chats.Add(entry); }
        entry.Enabled = true;
        entry.Minutes = Math.Max(20, minutes);
        entry.FastMinutes = minutes < 20 ? minutes : 0;
        entry.Prompt = prompt;
        entry.DueUtc = now.AddMinutes(minutes);
        entry.SlotAnchorUtc = entry.DueUtc;
        entry.LastAnswerFingerprint = ""; // explicit reconfiguration permits one fresh wake
        entry.LastState = "armed";
        return entry;
    }

    // Recompute from the original slot, never from the time an attempt completed.
    // A retry may temporarily move DueUtc; that must not move SlotAnchorUtc.
    internal static DateTimeOffset NextFixedSlotUtc(DateTimeOffset anchor,
        DateTimeOffset now, int minutes)
    {
        if (minutes is < 15 or > 1440)
            throw new ArgumentOutOfRangeException(nameof(minutes));
        anchor = anchor.ToUniversalTime();
        now = now.ToUniversalTime();
        if (now < anchor) return anchor;
        long intervalTicks = TimeSpan.FromMinutes(minutes).Ticks;
        long elapsed = (now - anchor).Ticks;
        long steps = checked(elapsed / intervalTicks + 1);
        return anchor.AddTicks(checked(steps * intervalTicks));
    }
    // Fingerprint only the exact note characters included in the wake payload.
    // No note text goes into diagnostics; edits beyond the 24k cutoff
    // cannot falsely appear as delivered in the hash.
    internal static string DeliveredNotesFingerprint(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return "";
        string included = notes.Length <= 24000 ? notes : notes[..24000];
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(included)));
    }
    internal static string Message(Chat entry, string notes)
    {
        var msg = string.IsNullOrWhiteSpace(entry.Prompt)
            ? "Wróć do pracy nad tą rozmową i kontynuuj ostatnie zadanie."
            : entry.Prompt.Trim();
        if (!string.IsNullOrWhiteSpace(notes))
        {
            // Explicitly indicate truncation instead of falsely implying all notes were delivered.
            const int maxNotes = 24000;
            string body = notes.Length <= maxNotes ? notes : notes[..maxNotes] +
                "\n[Notatki dłuższe niż 24000 znaków. Dalsza część pozostała w aplikacji.]";
            msg += "\n\nNotatki właściciela przypisane do tego czatu:\n" + body;
        }
        return msg;
    }

    // One stable numeric ID per attempted wake in this chat, even if
    // the renderer later crashes or an ACK is missing. Gaps are intentional.
    internal static string NumberedMessage(Chat entry, string notes, string receipt,
        DateTimeOffset? wakeStartedUtc = null, bool forceOrdinary = false,
        bool? registeredOblLead = null)
    {
        // Production caller supplies authoritative project-lead identity.
        // Legacy prompt inference is used only by callers without identity
        // (e.g., isolated self-test); a renamed prompt cannot change protocol.
        bool isObl = registeredOblLead ??
            (entry.Prompt.Contains("Open Browser Lite", StringComparison.OrdinalIgnoreCase) ||
             entry.Prompt.Contains(@"D:\projects\open-browser",
                 StringComparison.OrdinalIgnoreCase));
        if (isObl)
        {
            string header = "[OB-WAKE-" + entry.WakeSequence.ToString("D6") + "]";
            // P0 2026-11-02: real registered OBL leads receive the full
            // owner-approved production instructions, not just a two-line ping.
            // Legacy inferred-protocol self-test and unrelated projects stay unchanged.
            if (registeredOblLead == true)
            {
                header += "\nOBL, tylko SATANAMD. Odczytaj D:/projects/open-browser/docs/OBL_BACKLOG.md " +
                    "oraz docs/OPENBROWSER_LITE_CHECKPOINT.md. Wybierz jedno faktycznie " +
                    "nieukonczone zadanie P0, wykonaj zmiane SOURCE i nie zatrzymuj sie " +
                    "na samym planie ani diagnostyce.\n" +
                    "WDROZENIE: po podstawowym sprawdzeniu kompiluj nowa wersje, " +
                    "zapisz wydanie i SHA, ustaw QUEUED i aktywuj LIVE natychmiast, " +
                    "takze gdy poprzednia wersja generuje, edytor ma szkic lub worker pracuje. " +
                    "Nie czekaj na idle i nie uzalezniaj wdrozenia od odroczen. " +
                    "Sprawdz rzeczywisty LIVE (PID, EXE, SHA i phase), zachowaj rollback.\n" +
                    "WYBUDZENIA: przy niepewnosci domyslnie probuj nadawac, " +
                    "bez dodatkowych serii testow. Nie nadpisuj szkicu uzytkownika; " +
                    "gdy edytor jest zajety, kontynuuj przez nowego leada. " +
                    "Maksymalnie trzy podstawowe wybudzenia kontrolne na etap.\n" +
                    "DOKUMENTACJA: zapisz faktyczne zmiany i nastepny krok w checkpoint; " +
                    "historyczne wymagania oznaczaj NIEAKTUALNE zamiast hurtowo usuwac. " +
                    "Nie wykonuj recznych commit/push ani dzialan na innych projektach.";
            }
            bool handoff = !forceOrdinary && (entry.HandoffPhase == "request_pending" ||
                LiteChatHandoff.ShouldPrestageSnapshot(entry, true));
            return header + "\n" + (handoff
                ? "Zapisz aktualny handoff projektu zgodnie z zasadami. Następnie wykonaj kolejny niezakończony etap z planu."
                : "Kontynuuj projekt: wykonaj kolejny niezakończony etap z planu, przede wszystkim zmianę w kodzie lub wdrożenie. Nie powtarzaj zaliczonych testów bez konkretnej przyczyny.");
        }
        // Legacy format remains for unrelated scheduled chats.
        // The engine owns this timestamp. Never ask the model to guess the
        // actual time of dispatch from its own potentially stale clock.
        DateTimeOffset startedUtc = (wakeStartedUtc ?? DateTimeOffset.UtcNow)
            .ToUniversalTime();
        TimeZoneInfo warsaw = TimeZoneInfo.FindSystemTimeZoneById(
            "Central European Standard Time");
        DateTimeOffset startedLocal = TimeZoneInfo.ConvertTime(startedUtc, warsaw);
        string stamp = startedLocal.ToString(
            "yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
        string started = "WYBUDZENIE URUCHOMIONE: " + stamp +
            " (Warszawa, czas silnika; UTC " +
            startedUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
            ")";
        string body = Message(entry, notes);
        string marker = "[OB-" + receipt + "] [OB-WAKE-" +
            entry.WakeSequence.ToString("D6") + "]";
        // Required for every project: this is visible in ChatGPT history even
        // when a model starts its substantive work many minutes later.
        body = started + "\n" +
            "Napisz datę i godzinę. Pierwszy wiersz swojej odpowiedzi " +
            "zapisz dokładnie: DATA I GODZINA WYBUDZENIA: " + stamp +
            " (Warszawa). To godzina rozpoczęcia wybudzenia podana przez silnik, " +
            "nie zgaduj innej i nie zastępuj jej czasem generowania odpowiedzi. " +
            "Następnie wykonaj polecenie projektu.\n\n" + body;
        // The entire issued ID is diagnostic. CLI accepts only its first
        // canonical project component (not the receipt or conversation URL).
        var owner = LiteProjectRegistry.OpenDefault().Projects.FirstOrDefault(p =>
            p.Chats.Any(c => c.Role == "lead" && c.Url == entry.Url));
        if (owner is not null && LiteProjectWakeCli.ValidKey(owner.Key))
        {
            string completeId = owner.Key.ToUpperInvariant() + "-" +
                entry.WakeSequence.ToString("D6",CultureInfo.InvariantCulture) +
                "-A01-" + startedLocal.ToString("HHmmss",CultureInfo.InvariantCulture);
            body = "PEŁNY IDENTYFIKATOR WYBUDZENIA: " + completeId +
                "\nKOD PROJEKTU DO CLI: " + owner.Key +
                "\nCLI: --lite-project-wake status " + owner.Key + "\n" + body;
        }        if (entry.HandoffPhase == "request_pending")
            body += LiteChatHandoff.RequestText;
        // Project-only work instructions must be selected from the configured
        // schedule, NEVER from user notes attached to an unrelated chat.
        // The registered project identity has priority over incidental
        // mentions of OBL in an unrelated chat's prompt.
        bool project = isObl;
        if (LiteChatHandoff.ShouldPrestageSnapshot(entry, owner is not null))
            body += owner!.Key == "obl" ? LiteChatHandoff.PrestageSnapshotText :
                "\n\nROTACJA PROJEKTU " + owner.Key +
                ": To wybudzenie konczy sie cyfra 9. Zapisz HANDOFF do " +
                "D:/OpenBrowserLiteHandoffs/" + owner.Key +
                "/HANDOFF_CURRENT.md na SATANAMD, NADPISZ ten jeden plik. " +
                "Naglowek musi podawac kod projektu, numer zwyklego wybudzenia, " +
                "pelny identyfikator tego wybudzenia, czas zapisu, URL starego " +
                "czatu i host SATANAMD. Podaj faktyczne zmiany, stan projektu, " +
                "niedokonczone prace, odpowiedni checkpoint, blokady i nastepny krok. " +
                "Nie czytaj starych handoffow. " +
                "Jesli zapis sie nie uda, zapisz blad i kontynuuj. " +
                "Nastepne wybudzenie konczace sie na 0 nalezy do NOWEGO leada " +
                "niezaleznie od handoffu. Nie zatrzymuj harmonogramu.";
        if (project)
            body += "\n\nBEADS — STAN ZADAN NA SATANAMD: przed wyborem nowej pracy uruchom " +
                "powershell -NoProfile -ExecutionPolicy Bypass -File " +
                "D:/projects/open-browser/scripts/obl-beads.ps1 -Action scan. " +
                "Najpierw sprawdz zadania in_progress: ostatnie STOP oznacza normalne przerwanie, " +
                "a ostatnie START bez STOP wymaga sprawdzenia checkpointu, artefaktow " +
                "i czy poprzednia sesja nadal pracuje. Nie zamykaj zadania tylko z powodu czasu. " +
                "Kontynuuj najpierw niewykonana prace; wybieraj tylko konkretne podzadania, nie epiki. Dopiero gdy jej nie ma, uzyj -Action ready " +
                "i pobierz nowe zadanie. Przy rozpoczeciu pracy wywolaj " +
                "obl-beads.ps1 -Action start -Task <ID_ZADANIA> -Wake <PELNE_ID_Z_NAGLOWKA>. " +
                "Przy zakonczeniu TEGO WYBUDZENIA wywolaj -Action stop z tym samym ID " +
                "nawet jesli zadanie jest niedokonczone (pozostaje in_progress). " +
                "Tylko po rzeczywistym wykonaniu i sprawdzeniu zadania wywolaj -Action done -Evidence <WERYFIKOWANY_WYNIK>. " +
                "Beads zarzadza stanem zadan, a zwykly checkpoint dalej opisuje przebieg wybudzenia.";
        if (project) body += "\n\nKANONICZNY BACKLOG: D:/projects/open-browser/docs/OBL_BACKLOG.md. Zrodlo wymagan; stan wykonania i nastepne podzadanie przez Beads, bez czytania starych roadmap.";
        if (project)
            body += "\n\nOBOWIĄZKOWY CHECKPOINT [OB-WAKE-" +
                entry.WakeSequence.ToString("D6") +
                "]: zapisuj regularnie krótkie, konkretne postępy do " +
                @"D:\projects\open-browser\docs\OPENBROWSER_LITE_CHECKPOINT.md" +
                " na SatanAMD. PRZED rozpoczęciem pracy odczytaj ten plik, " +
                "a po każdej istotnej czynności, przed restartem i przed końcem " +
                "zaktualizuj maksymalnie kilkanaście krótkich wierszy: " +
                "LIVE/SOURCE, zmiany, testy, blokady i następny krok. " +
                "Ważne zdarzenia możesz dopisać także do " +
                @"D:\projects\open-browser\docs\OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md" +
                ". DODATKOWO prowadź wspólny dziennik z silnikiem na SatanAMD: " +
                @"%LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\diagnostics\wake-lifecycle.jsonl" +
                ". Na POCZĄTKU dopisz jedną linię JSONL z polami schema=1, " +
                "utc (ISO UTC), actor=chat, stage=chat_started, wakeSequence=" +
                entry.WakeSequence + ", receipt=" + receipt +
                ". Przed końcem dopisz drugą linię z actor=chat, " +
                "stage=chat_finished albo chat_blocked, tym samym numerem " +
                "i receipt, oraz krótkim result bez tajnych danych. " +
                "Możesz dodać stage=chat_checkpoint w czasie pracy. " +
                "To jest TEN SAM PLIK co wpisy silnika due/attempt/ACK. " +
                "Nie dopisuj fałszywego ACK ani nie uznawaj próby za wysłaną. " +
                "Jeśli zapis do dziennika SATANAMD się nie uda, wskaż to " +
                "w checkpointcie; nie udawaj sukcesu. " +
                "Nie używaj ChatGPT Tasks ani wewnętrznych automatyzacji. " +
                "Gdy JEDNA operacja lub narzędzie odmówi zapisu, " +
                "odnotuj fakt i kontynuuj inne dozwolone zadania P0. " +
                "Nie obchodź odmowy innym kanałem. " +
                "Nie kończ CAŁEJ autonomicznej pracy statusem BLOCKED " +
                "wyłącznie z powodu jednej odrzuconej operacji; " +
                "BLOCKED oznacza, że naprawdę nie da się już wykonać " +
                "żadnego dalszego dozwolonego kroku. " +
                "Błędy konkretnego narzędzia umieść w checkpointcie.";
        // Radar models sometimes mix the displayed [OB-...] marker and
        // [OB-WAKE-...] into one receipt value in chat-side journal entries.
        // Keep the engine's mechanical evidence untouched; make the optional
        // chat-side metadata contract unambiguous without a resend.
        if (entry.Prompt.Contains("Networking Radar",
            StringComparison.OrdinalIgnoreCase))
            body += "\n\nWażne dla wpisów actor=chat w wake-lifecycle.jsonl: " +
                "wakeSequence jest LICZBĄ " + entry.WakeSequence +
                " (bez OB-WAKE- i bez cudzysłowów), receipt to DOKŁADNIE " +
                "32 znaki hex: " + receipt +
                " (bez OB-, bez nawiasów, bez dodawania |OB-WAKE-...). " +
                "Jeśli zapisujesz chat_started/chat_finished, użyj " +
                "osobnych pól JSON: wakeSequence=" + entry.WakeSequence +
                ", receipt=\"" + receipt +
                "\", actor=\"chat\", stage=\"chat_started\" lub \"chat_finished\"; " +
                "utc powinien zawierać rzeczywisty czas ISO UTC wpisu. " +
                "Nie poprawiaj historycznych wpisów i nie dopisuj ACK silnika.";
        // Advisory protocol for the two verified project executors.
        // Older, unrelated schedules keep their exact previous payload.
        if (project || entry.Prompt.Contains("Networking Radar",
            StringComparison.OrdinalIgnoreCase))
            body += "\n\nOpcjonalnie zakończ ostatnią odpowiedź JEDNYM " +
                "samodzielnym wierszem: [OB-STATUS: CONTINUE], " +
                "[OB-STATUS: WAIT_USER], [OB-STATUS: BLOCKED], " +
                "[OB-STATUS: DONE] albo [OB-STATUS: WAIT_UNTIL " +
                "2026-10-23T12:00:00Z] (z rzeczywistą datą ISO UTC). " +
                "Nie wymyślaj blokad ani terminu; brak znacznika jest dozwolony. " +
                "Status nie zastępuje checkpointu projektu.";
        return marker + "\n" + body;
    }

    internal static int SelfTest()
    {
        try
        {
            if (LiteProjectWakeCli.SelfTest() != 0) return 190;
            if (LiteWakeRotation.SelfTest() != 0) return 177;
            int projectWakeTest = LiteProjectWakeStore.SelfTest();
            if (projectWakeTest != 0) return 180 + projectWakeTest;
            if (LiteChatHandoff.SelfTest() != 0) return 167;
            if (LiteHandoffEvidence.SelfTest() != 0) return 168;
            if (LiteHandoffSuccession.SelfTest() != 0) return 169;
            // Stable clock slots: attempt delays, retry and overrun do not drift.
            var slot = new DateTimeOffset(2026, 10, 28, 11, 45, 0, TimeSpan.Zero);
            if (NextFixedSlotUtc(slot, slot.AddMinutes(-2), 15) != slot) return 171;
            if (NextFixedSlotUtc(slot, slot, 15) != slot.AddMinutes(15)) return 172;
            if (NextFixedSlotUtc(slot, slot.AddMinutes(47), 15) != slot.AddMinutes(60))
                return 173;
            if (NextFixedSlotUtc(slot, slot.AddMinutes(3), 20) != slot.AddMinutes(20))
                return 174;
            // Different configured phases do not collide or inherit each other.
            var otherSlot = slot.AddMinutes(5);
            if (NextFixedSlotUtc(otherSlot, slot.AddMinutes(47), 15) !=
                otherSlot.AddMinutes(45)) return 175;            var now = DateTimeOffset.UtcNow;
            string a = "https://chatgpt.com/c/11111111-1111-1111-1111-111111111111";
            string b = "https://chatgpt.com/c/22222222-2222-2222-2222-222222222222";
            var list = new List<Chat>();
            Configure(list, a, 25, "Rozwijaj projekt", now);
            Configure(list, b, 30, "Pisz następny rozdział", now);
            // A persistent per-chat note is appended to every wake, not consumed.
            string testNoteA = "OBL_NOTE_A_20261027";
            string testNoteB = "OBL_NOTE_B_20261027";
            for (int testWake = 111; testWake <= 113; testWake++)
            {
                list[0].WakeSequence = testWake;
                string output = NumberedMessage(list[0], testNoteA,
                    testWake.ToString("D32"), now.AddMinutes(15*(testWake-111)));
                if (!output.Contains(testNoteA) || output.Contains(testNoteB))
                    return 151;
            }
            // Snapshot hashes are stable across wakes, change with owner edits,
            // and ignore undispatched characters beyond the 24k limit.
            if (DeliveredNotesFingerprint(testNoteA).Length != 64 ||
                DeliveredNotesFingerprint(testNoteA) ==
                    DeliveredNotesFingerprint(testNoteA+"_EDITED") ||
                DeliveredNotesFingerprint("") != "" ||
                DeliveredNotesFingerprint("   ") != "") return 153;
            string longPrefix = new string('n',24000);
            if (DeliveredNotesFingerprint(longPrefix+"OLD") !=
                DeliveredNotesFingerprint(longPrefix+"NEW")) return 154;            string changedNote = NumberedMessage(list[0],
                testNoteA+"_EDITED", new string('4',32), now.AddMinutes(45));
            string otherNote = NumberedMessage(list[1], testNoteB,
                new string('5',32), now.AddMinutes(45));
            if (!changedNote.Contains(testNoteA+"_EDITED") ||
                otherNote.Contains(testNoteA) || !otherNote.Contains(testNoteB))
                return 152;            if (list.Count != 2 || list[0].DueUtc != now.AddMinutes(25) ||
                list[1].DueUtc != now.AddMinutes(30)) return 2;
            Configure(list, a, 45, "Kontynuuj", now.AddMinutes(1));
            if (list.Count != 2 || list[1].Prompt != "Pisz następny rozdział" ||
                list[0].DueUtc != now.AddMinutes(46)) return 3;
            if (!Message(list[1], "Zarys bohatera").Contains("Zarys bohatera")) return 4;
            if (Message(list[1], "").Contains("Notatki właściciela")) return 5;
            if (!Message(list[0], new string('x', 25000)).Contains("Dalsza część")) return 6;
            list[0].LastAnswerFingerprint = new string('A', 64);
            Configure(list, a, 25, "Renewed project work", now);
            if (list[0].LastAnswerFingerprint != "") return 7;
            Configure(list, a, 15, "Kontynuuj Open Browser Lite", now);
            if (list[0].Minutes != 20 || list[0].FastMinutes != 15 ||
                list[0].EffectiveMinutes != 15 ||
                list[0].DueUtc != now.AddMinutes(15)) return 8;
            string wire = JsonSerializer.Serialize(list[0]);
            if (!wire.Contains("\"Minutes\":20") ||
                !wire.Contains("\"FastMinutes\":15") ||
                wire.Contains("EffectiveMinutes")) return 11;
            // Registered lead identity takes precedence over prompt text.
            list[0].Prompt = "Kontynuuj zadanie";
            list[0].WakeSequence = 26;
            string promptIndependent = NumberedMessage(list[0], "PRIVATE_NOTES",
                "0123456789abcdef0123456789abcdef",
                registeredOblLead: true);
            if (promptIndependent != "[OB-WAKE-000026]\nKontynuuj projekt: wykonaj kolejny niezakończony etap z planu, przede wszystkim zmianę w kodzie lub wdrożenie. Nie powtarzaj zaliczonych testów bez konkretnej przyczyny.")
                return 182;
            list[1].Prompt = "Opisz Open Browser Lite";
            string unrelated = NumberedMessage(list[1], "PRIVATE_NOTES",
                "0123456789abcdef0123456789abcdef",
                registeredOblLead: false);
            if (unrelated.StartsWith("[OB-WAKE-", StringComparison.Ordinal) ||
                !unrelated.Contains("PRIVATE_NOTES", StringComparison.Ordinal) ||
                unrelated.Contains("BEADS", StringComparison.Ordinal))
                return 183;
            list[0].Prompt = "Kontynuuj Open Browser Lite";
            list[1].Prompt = "Pisz następny rozdział";
            list[0].WakeSequence = 27;
            string numbered = NumberedMessage(list[0], "",
                "0123456789abcdef0123456789abcdef");
            if (numbered != "[OB-WAKE-000027]\nKontynuuj projekt: wykonaj kolejny niezakończony etap z planu, przede wszystkim zmianę w kodzie lub wdrożenie. Nie powtarzaj zaliczonych testów bez konkretnej przyczyny.")
                return 9;
            if (numbered.Contains("0123456789abcdef") ||
                numbered.Contains("WYBUDZENIE URUCHOMIONE") ||
                numbered.Contains("OBL_BACKLOG") ||
                numbered.Contains("BEADS") || numbered.Contains("checkpoint")) return 176;
            list[0].WakeSequence = 29;
            string handoff = NumberedMessage(list[0], "private notes",
                "0123456789abcdef0123456789abcdef");
            if (handoff != "[OB-WAKE-000029]\nZapisz aktualny handoff projektu zgodnie z zasadami. Następnie wykonaj kolejny niezakończony etap z planu.")
                return 170;
            string successorAtNine = NumberedMessage(list[0], "",
                "0123456789abcdef0123456789abcdef",
                forceOrdinary: true);
            if (successorAtNine !=
                "[OB-WAKE-000029]\nKontynuuj projekt: wykonaj kolejny niezakończony etap z planu, przede wszystkim zmianę w kodzie lub wdrożenie. Nie powtarzaj zaliczonych testów bez konkretnej przyczyny.")
                return 177;
            list[0].WakeSequence = 30;
            if (NumberedMessage(list[0], "private notes",
                "0123456789abcdef0123456789abcdef") !=
                "[OB-WAKE-000030]\nKontynuuj projekt: wykonaj kolejny niezakończony etap z planu, przede wszystkim zmianę w kodzie lub wdrożenie. Nie powtarzaj zaliczonych testów bez konkretnej przyczyny.")
                return 12;
            list[1].WakeSequence = 2;
            if (NumberedMessage(list[1], "", "0123456789abcdef0123456789abcdef")
                .Contains("OBOWIĄZKOWY CHECKPOINT")) return 10;
            // OBL output is invariant under engine dispatch time and note edits.
            var summerUtc = new DateTimeOffset(2026, 10, 23, 8, 41, 12, TimeSpan.Zero);
            var summer = NumberedMessage(list[0], "edited notes",
                "0123456789abcdef0123456789abcdef", summerUtc);
            if (summer != "[OB-WAKE-000030]\nKontynuuj projekt: wykonaj kolejny niezakończony etap z planu, przede wszystkim zmianę w kodzie lub wdrożenie. Nie powtarzaj zaliczonych testów bez konkretnej przyczyny.")
                return 14;
            var winterUtc = new DateTimeOffset(2026, 11, 2, 8, 41, 12, TimeSpan.Zero);
            var winter = NumberedMessage(list[0], "", "0123456789abcdef0123456789abcdef", winterUtc);
            if (winter != summer) return 15;
            // A non-generating but unanswered user turn must not stall a
            // project lead indefinitely. Other chats keep the old policy.
            if (!ChatGptLiteForm.IsStalledLeadReason("generating", false) ||
                !ChatGptLiteForm.IsStalledLeadReason("assistant_not_complete", true) ||
                ChatGptLiteForm.IsStalledLeadReason("assistant_not_complete", false) ||
                ChatGptLiteForm.IsStalledLeadReason("draft_present", true) ||
                ChatGptLiteForm.IsStalledLeadReason("wrong_chat", true))
                return 178;
            if (ChatGptLiteForm.ShouldHoldUnconfirmedRecoveryForProject(
                    ChatGptLiteForm.WakePreflightGate.HoldUnconfirmedRecovery, true) ||
                !ChatGptLiteForm.ShouldHoldUnconfirmedRecoveryForProject(
                    ChatGptLiteForm.WakePreflightGate.HoldUnconfirmedRecovery, false) ||
                ChatGptLiteForm.ShouldHoldUnconfirmedRecoveryForProject(
                    ChatGptLiteForm.WakePreflightGate.Continue, false) ||
                ChatGptLiteForm.ShouldHoldUnconfirmedRecoveryForProject(
                    ChatGptLiteForm.WakePreflightGate.Complete, false))
                return 181;
            // Alternating renderer failures must exhaust the SAME bounded
            // recovery budget, even when the reason changes every poll.
            if (ChatGptLiteForm.NextOblDeferralCount("draft_present", 1,
                    "no_composer", true) != 2 ||
                ChatGptLiteForm.NextOblDeferralCount("no_composer", 2,
                    "probe_failed", true) != 3 ||
                ChatGptLiteForm.NextOblDeferralCount("probe_failed", 3,
                    "worker_exception", true) != 4 ||
                !ChatGptLiteForm.RotateOblAfterRepeatedDeferral("probe_failed", 3) ||
                !ChatGptLiteForm.RotateOblAfterRepeatedDeferral("worker_exception", 3) ||
                ChatGptLiteForm.NextOblDeferralCount("no_composer", 2,
                    "generating", true) != 1 ||
                ChatGptLiteForm.NextOblDeferralCount("generating", 2,
                    "draft_present", true) != 1 ||
                ChatGptLiteForm.NextOblDeferralCount("draft_present", 2,
                    "no_composer", false) != 1 ||
                ChatGptLiteForm.RotateOblAfterRepeatedDeferral("backend_http_429", 99))
                return 184;
            var unsentFixture = new Chat {
                Url = a, Enabled = true, WakeSequence = 316,
                LastAnswerFingerprint = new string('a', 64),
                DueUtc = now.AddMinutes(15)
            };
            if (ChatGptLiteForm.RecordDefinitelyUnsentWake(
                    unsentFixture, now, "missing_composer", true) ||
                unsentFixture.ConsecutiveDeferrals != 1 ||
                unsentFixture.WakeSequence != 316 ||
                unsentFixture.DueUtc != now.AddMinutes(2) ||
                unsentFixture.LastAnswerFingerprint.Length != 0)
                return 185;
            // The next reserved N does not reset this counter.
            unsentFixture.WakeSequence++;
            if (ChatGptLiteForm.RecordDefinitelyUnsentWake(
                    unsentFixture, now.AddMinutes(2), "blocked_navigation", true) ||
                unsentFixture.ConsecutiveDeferrals != 2 ||
                unsentFixture.EmergencyPhase.Length != 0) return 186;
            unsentFixture.WakeSequence++;
            if (!ChatGptLiteForm.RecordDefinitelyUnsentWake(
                    unsentFixture, now.AddMinutes(4), "draft_button_missing", true) ||
                unsentFixture.ConsecutiveDeferrals != 3 ||
                unsentFixture.EmergencyPhase != "rotate" ||
                unsentFixture.WakeSequence != 318 ||
                unsentFixture.DueUtc >= now.AddMinutes(4) ||
                !unsentFixture.Enabled) return 187;
            var anotherProjectUnsent = new Chat {
                Url = b, Enabled = true, WakeSequence = 40,
                ConsecutiveDeferrals = 2, DueUtc = now.AddMinutes(15)
            };
            if (ChatGptLiteForm.RecordDefinitelyUnsentWake(
                    anotherProjectUnsent, now, "blocked_navigation", false) ||
                anotherProjectUnsent.EmergencyPhase.Length != 0 ||
                anotherProjectUnsent.WakeSequence != 40) return 188;
            var mixedFailure = new Chat {
                Url = a, Enabled = true, WakeSequence = 50,
                LastDeferralReason = "probe_failed", ConsecutiveDeferrals = 2
            };
            if (!ChatGptLiteForm.RecordDefinitelyUnsentWake(
                    mixedFailure, now, "busy_draft_present", true) ||
                mixedFailure.EmergencyPhase != "rotate") return 189;
            var generationCameBack = new Chat {
                Url = a, Enabled = true, WakeSequence = 60,
                LastDeferralReason = "definitely_unsent",
                ConsecutiveDeferrals = 2, DueUtc = now.AddMinutes(15)
            };
            if (ChatGptLiteForm.RecordDefinitelyUnsentWake(
                    generationCameBack, now, "busy_generating", true) ||
                generationCameBack.ConsecutiveDeferrals != 0 ||
                generationCameBack.EmergencyPhase.Length != 0 ||
                generationCameBack.LastDeferralReason != "generating" ||
                generationCameBack.DueUtc != now.AddMinutes(2)) return 191;
            var uncertainWake = new Chat {
                Url = a, Enabled = true, WakeSequence = 317,
                LastAnswerFingerprint = new string('c', 64),
                DueUtc = now.AddMinutes(15),
                LastDeferralReason = "probe_failed",
                ConsecutiveDeferrals = 1
            };
            if (ChatGptLiteForm.RecordOblUncertainSend(
                    uncertainWake, now, false) ||
                uncertainWake.ConsecutiveDeferrals != 2 ||
                uncertainWake.LastAnswerFingerprint.Length != 64 ||
                uncertainWake.DueUtc != now.AddMinutes(2) ||
                uncertainWake.LastState != "send_result_uncertain" ||
                uncertainWake.EmergencyPhase.Length != 0)
                return 192;
            // Reservation increments N, but must not erase the failure budget.
            uncertainWake.WakeSequence++;
            if (!ChatGptLiteForm.RecordOblUncertainSend(
                    uncertainWake, now.AddMinutes(2), false) ||
                uncertainWake.ConsecutiveDeferrals != 3 ||
                uncertainWake.WakeSequence != 318 ||
                uncertainWake.EmergencyPhase != "rotate" ||
                uncertainWake.DueUtc >= now.AddMinutes(2))
                return 193;
            var clickedWithoutAck = new Chat {
                Url = a, Enabled = true,
                LastDeferralReason = "receipt_uncertain",
                ConsecutiveDeferrals = 2,
                LastAnswerFingerprint = new string('d', 64)
            };
            if (ChatGptLiteForm.RecordOblUncertainSend(
                    clickedWithoutAck, now, true) ||
                clickedWithoutAck.ConsecutiveDeferrals != 0 ||
                clickedWithoutAck.LastDeferralReason != "receipt_uncertain_click" ||
                clickedWithoutAck.LastState != "receipt_uncertain_after_click" ||
                clickedWithoutAck.DueUtc != now.AddMinutes(2) ||
                clickedWithoutAck.LastAnswerFingerprint.Length != 64 ||
                clickedWithoutAck.EmergencyPhase.Length != 0)
                return 194;
            if (ChatGptLiteForm.RotateOblAfterRepeatedDeferral("draft_present", 2) ||
                !ChatGptLiteForm.RotateOblAfterRepeatedDeferral("draft_present", 3) ||
                !ChatGptLiteForm.RotateOblAfterRepeatedDeferral("probe_failed", 3) ||
                !ChatGptLiteForm.RotateOblAfterRepeatedDeferral("forced_wake_composer_not_ready", 3) ||
                ChatGptLiteForm.RotateOblAfterRepeatedDeferral("generating", 99) ||
                ChatGptLiteForm.RotateOblAfterRepeatedDeferral("backend_http_429", 99))
                return 180;
            if (ChatGptLiteForm.StallRecoveryMinutes(true, 15) != 2 ||
                ChatGptLiteForm.StallRecoveryMinutes(true, 30) != 2 ||
                ChatGptLiteForm.StallRecoveryMinutes(false, 15) != 15)
                return 179;
            if (ChatGptLiteForm.WakeDeferralMinutes("response_error", 2) != 15 ||
                ChatGptLiteForm.WakeDeferralMinutes("response_error", 20) != 20 ||
                ChatGptLiteForm.WakeDeferralMinutes("assistant_not_complete", 2) != 15 ||
                ChatGptLiteForm.WakeDeferralMinutes("assistant_not_complete", 20) != 20 ||
                ChatGptLiteForm.WakeDeferralMinutes("generating", 2) != 2 ||
                ChatGptLiteForm.WakeDeferralMinutes("backend_http_429", 3) != 3)
                return 130;
            // The operational timeline must not bury successful work among
            // two-minute due/deferred noise, and must tolerate malformed JSONL.
            const string activityDue = "{\"utc\":\"2026-10-23T11:20:29Z\"," +
                "\"actor\":\"engine\",\"stage\":\"due\",\"wakeSequence\":15}";
            const string activityDone = "{\"utc\":\"2026-10-23T11:21:00Z\"," +
                "\"actor\":\"chat\",\"stage\":\"chat_finished\",\"wakeSequence\":15," +
                "\"receipt\":\"70d8f4be39ad416c9ab3162cc89d58db\"}";
            if (ChatGptLiteForm.DescribeActivityLine(activityDue) is not null ||
                ChatGptLiteForm.DescribeActivityLine(activityDue, showAll: true) is not string dueLine ||
                !dueLine.Contains("Termin wybudzenia") ||
                ChatGptLiteForm.DescribeActivityLine(activityDone) is not string doneLine ||
                !doneLine.Contains("#000015 70d8f4be") ||
                !doneLine.Contains("Czat zakończył pracę") ||
                ChatGptLiteForm.DescribeActivityLine("{") is not null ||
                ChatGptLiteForm.DescribeActivityLine("[]") is not null ||
                ChatGptLiteForm.DescribeActivityLine(
                    "{\"utc\":\"2026-10-23T11:20:29Z\",\"actor\":\"chat\"}") is not null)
                return 151;
            if (!ChatGptLiteForm.IsCurrentResponseProbe(5, 5, 14, 14,
                    "/c/aaa", "/c/aaa") ||
                ChatGptLiteForm.IsCurrentResponseProbe(5, 6, 14, 14,
                    "/c/aaa", "/c/aaa") ||
                ChatGptLiteForm.IsCurrentResponseProbe(5, 5, 14, 15,
                    "/c/aaa", "/c/aaa") ||
                ChatGptLiteForm.IsCurrentResponseProbe(5, 5, 14, 14,
                    "/c/aaa", "/c/bbb"))
                return 153;
            var probeDelayed = new DateTimeOffset(2026, 10, 23, 11, 20, 0,
                TimeSpan.Zero);
            if (ChatGptLiteForm.ResponseProbeIsStalled(null,
                    probeDelayed.AddMinutes(3)) ||
                ChatGptLiteForm.ResponseProbeIsStalled(probeDelayed,
                    probeDelayed.AddSeconds(34)) ||
                !ChatGptLiteForm.ResponseProbeIsStalled(probeDelayed,
                    probeDelayed.AddSeconds(35)))
                return 152;
            // Headless regression for foreground follow after successor promotion.
            // WebView2 fixture separately verifies GUI; these checks run in Session 0.
            const string oldLead = "https://chatgpt.com/c/11111111-1111-1111-1111-111111111111";
            const string newLead = "https://chatgpt.com/c/22222222-2222-2222-2222-222222222222";
            const string unrelatedLead = "https://chatgpt.com/c/33333333-3333-3333-3333-333333333333";
            if (!ChatGptLiteForm.ShouldFollowPromotedLeadForeground(
                    oldLead, oldLead, newLead)) return 211;
            if (!ChatGptLiteForm.ShouldFollowPromotedLeadForeground(
                    "https://chatgpt.com/", oldLead, newLead)) return 212;
            if (ChatGptLiteForm.ShouldFollowPromotedLeadForeground(
                    unrelatedLead, oldLead, newLead)) return 213;
            if (ChatGptLiteForm.ShouldFollowPromotedLeadForeground(
                    "https://example.com/", oldLead, newLead)) return 214;
            if (ChatGptLiteForm.ShouldFollowPromotedLeadForeground(
                    oldLead, oldLead, "https://example.com/")) return 215;
            int keepalivePauseTest = LiteKeepalivePause.SelfTest();
            if (keepalivePauseTest != 0) return 155 + keepalivePauseTest;
            int noteIsolation = LiteChatNotes.SelfTest();
            if (noteIsolation != 0) return 140 + noteIsolation;
            int modelStatus = LiteModelStatus.SelfTest();
            if (modelStatus != 0) return 110 + modelStatus;
            int ownerQueue = LiteOwnerInterventions.SelfTest();
            if (ownerQueue != 0) return 120 + ownerQueue;
            int projectPolicy = LiteProjectRegistry.WakePolicySelfTest();
            if (projectPolicy != 0) return 100 + projectPolicy;
            int wakeJournal = LiteWakeJournal.SelfTest();
            if (wakeJournal != 0) return 20 + wakeJournal;
            int throttle = LiteHttpReadThrottle.SelfTest();
            if (throttle != 0) return 80 + throttle;
            int preflight = LiteChatWakePreflight.SelfTest();
            return preflight == 0 ? 0 : 40 + preflight;
        }
        catch { return 7; }
    }
}
