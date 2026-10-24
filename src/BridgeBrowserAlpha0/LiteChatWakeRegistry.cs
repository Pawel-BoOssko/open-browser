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
        // Durable per-conversation wake attempt ID. Incremented before sending.
        public long WakeSequence { get; set; }
        public DateTimeOffset DueUtc { get; set; }
        public DateTimeOffset LastAttemptUtc { get; set; }
        public string LastState { get; set; } = "armed";
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
        entry.LastAnswerFingerprint = ""; // explicit reconfiguration permits one fresh wake
        entry.LastState = "armed";
        return entry;
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
        DateTimeOffset? wakeStartedUtc = null)
    {
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
        if (entry.HandoffPhase == "request_pending")
            body += LiteChatHandoff.RequestText;
        // Project-only work instructions must be selected from the configured
        // schedule, NEVER from user notes attached to an unrelated chat.
        bool project = entry.Prompt.Contains("Open Browser Lite",
                StringComparison.OrdinalIgnoreCase) ||
            entry.Prompt.Contains(@"D:\projects\open-browser",
                StringComparison.OrdinalIgnoreCase);
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
            if (LiteChatHandoff.SelfTest() != 0) return 167;
            var now = DateTimeOffset.UtcNow;
            string a = "https://chatgpt.com/c/11111111-1111-1111-1111-111111111111";
            string b = "https://chatgpt.com/c/22222222-2222-2222-2222-222222222222";
            var list = new List<Chat>();
            Configure(list, a, 25, "Rozwijaj projekt", now);
            Configure(list, b, 30, "Pisz następny rozdział", now);
            if (list.Count != 2 || list[0].DueUtc != now.AddMinutes(25) ||
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
            list[0].WakeSequence = 27;
            string numbered = NumberedMessage(list[0], "",
                "0123456789abcdef0123456789abcdef");
            if (!numbered.Contains("[OB-WAKE-000027]") ||
                !numbered.Contains("OBOWIĄZKOWY CHECKPOINT") ||
                !numbered.Contains("OPENBROWSER_LITE_CHECKPOINT.md") ||
                !numbered.Contains("OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md"))
                return 9;
            list[1].WakeSequence = 2;
            if (NumberedMessage(list[1], "", "0123456789abcdef0123456789abcdef")
                .Contains("OBOWIĄZKOWY CHECKPOINT")) return 10;
            if (NumberedMessage(list[1], "W notatkach padło Open Browser Lite",
                    "0123456789abcdef0123456789abcdef")
                .Contains("OBOWIĄZKOWY CHECKPOINT")) return 13;
            if (!numbered.Contains("wake-lifecycle.jsonl") ||
                !numbered.Contains("chat_started") ||
                !numbered.Contains("chat_finished")) return 12;
            // Every wake carries an engine-generated absolute timestamp that
            // the model must repeat as the first line of its answer.
            var summerUtc = new DateTimeOffset(2026, 10, 23, 8, 41, 12, TimeSpan.Zero);
            var summer = NumberedMessage(list[0], "", "0123456789abcdef0123456789abcdef",
                summerUtc);
            if (!summer.Contains("WYBUDZENIE URUCHOMIONE: 2026-10-23 10:41:12 +02:00") ||
                !summer.Contains("UTC 2026-10-23 08:41:12") ||
                !summer.Contains("Napisz datę i godzinę.") ||
                !summer.Contains("DATA I GODZINA WYBUDZENIA: 2026-10-23 10:41:12 +02:00"))
                return 14;
            var winterUtc = new DateTimeOffset(2026, 11, 2, 8, 41, 12, TimeSpan.Zero);
            var winter = NumberedMessage(list[1], "", "0123456789abcdef0123456789abcdef",
                winterUtc);
            if (!winter.Contains("WYBUDZENIE URUCHOMIONE: 2026-11-02 09:41:12 +01:00") ||
                !winter.Contains("DATA I GODZINA WYBUDZENIA: 2026-11-02 09:41:12 +01:00"))
                return 15;
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
