namespace BridgeBrowserAlpha0;

/// <summary>
/// Conservative, one-shot handoff request policy. The engine never fabricates
/// a new chat URL or automatically activates a second project executor.
/// </summary>
internal static class LiteChatHandoff
{
    // Count verified eligibility opportunities conservatively using the
    // durable wake sequence. A manual request can occur sooner.
    // Observed production long-chat cap can arrive before 20 wakes; request
    // a checkpoint while the conversation can still produce a full handoff.
    internal const long SuggestAfterWakeAttempts = 14;
    internal const int SuggestAfterVisibleTurns = 50;

    // A request that demands WAIT_USER can permanently stop an autonomous
    // project. There is no verified automatic new-chat/ACK/ownership transfer
    // yet. Fail OPEN for continuing the existing safe wake schedule and FAIL
    // CLOSED for the automatic STOP handoff request, until full E2E is proven.
    internal const bool AutonomousSuccessorReady = false;

    internal static bool NeedsRotation(LiteChatWakeRegistry.Chat chat,
        bool isRegisteredProjectLead, int observedTurns = 0) =>
        isRegisteredProjectLead && chat.Enabled &&
        (chat.WakeSequence >= SuggestAfterWakeAttempts ||
         observedTurns >= SuggestAfterVisibleTurns) &&
        string.IsNullOrEmpty(chat.HandoffPhase);

    internal static bool ShouldRequest(LiteChatWakeRegistry.Chat chat,
        bool isRegisteredProjectLead, int observedTurns = 0) =>
        AutonomousSuccessorReady &&
        NeedsRotation(chat, isRegisteredProjectLead, observedTurns);

    internal static bool Request(LiteChatWakeRegistry.Chat chat, bool manual,
        bool isRegisteredProjectLead, int observedTurns = 0)
    {
        if (!isRegisteredProjectLead || !chat.Enabled ||
            !string.IsNullOrEmpty(chat.HandoffPhase)) return false;
        if (!manual && !ShouldRequest(chat, isRegisteredProjectLead,
            observedTurns)) return false;
        chat.HandoffPhase = "request_pending";
        return true;
    }

    // After a click that might have sent the request, never inject the same
    // handoff instruction again. If definitely unsent, it remains pending.
    internal static void MarkAttempted(LiteChatWakeRegistry.Chat chat,
        bool mayHaveSubmitted)
    {
        if (mayHaveSubmitted && chat.HandoffPhase == "request_pending")
            chat.HandoffPhase = "requested";
    }

    internal static bool CanTakeOver(LiteChatWakeRegistry.Chat oldChat,
        string? newUrl) =>
        oldChat.HandoffPhase == "requested" &&
        oldChat.LastState == "status_wait_user" &&
        !oldChat.Enabled &&
        LiteDevWakeState.IsChatUrl(newUrl) &&
        !string.Equals(LiteDevWakeState.NormalizeUrl(newUrl!),
            oldChat.Url, StringComparison.Ordinal);

    internal static string RequestText =
        "\n\nROTACJA CZATU WYKONAWCZEGO — jednorazowa prośba o HANDOFF. " +
        "Ta rozmowa jest długa. Nie rozpoczynaj już nowych dużych zadań. " +
        "Przygotuj pełny, konkretny dokument HANDOFF w dokumentacji projektu: " +
        "co zrobiono, bieżące pliki i ścieżki, LIVE/SOURCE, testy, otwarte zadania, " +
        "zagrożenia, identyfikatory czatów, zasady pracy i następny krok. " +
        "Nie zgaduj ani nie twórz adresu nowego czatu. " +
        "Po faktycznym zapisaniu dokumentu podaj jego dokładną ścieżkę oraz " +
        "krótkie podsumowanie, a jako OSTATNI wiersz wypisz [OB-STATUS: WAIT_USER]. " +
        "W ten sposób Open Browser zatrzyma harmonogram starego wykonawcy. " +
        "Nowy czat będzie można uruchomić dopiero po zatwierdzonym przejęciu. " +
        "Jeśli zapis dokumentu się nie udał, nie deklaruj gotowego handoffu " +
        "ani nie emituj WAIT_USER — opisz blokadę w zwykłym checkpointcie.";

    internal static int SelfTest()
    {
        var old = new LiteChatWakeRegistry.Chat {
            Url = "https://chatgpt.com/c/11111111-1111-1111-1111-111111111111",
            Enabled = true, WakeSequence = 13
        };
        if (ShouldRequest(old, true) || Request(old, false, true)) return 1;
        if (ShouldRequest(old, true, 49) || NeedsRotation(old, true, 49)) return 12;
        if (ShouldRequest(old, true, 50) || !NeedsRotation(old, true, 50)) return 13;
        old.WakeSequence = 14;
        if (ShouldRequest(old, false) || Request(old, false, false) ||
            ShouldRequest(old, true) || !NeedsRotation(old, true) ||
            Request(old, false, true) || old.HandoffPhase != "") return 2;
        // Explicit UI owner request still works while unattended takeover
        // remains gated. Never silently send a WAIT_USER instruction.
        if (!Request(old, true, true) ||
            old.HandoffPhase != "request_pending") return 14;
        if (Request(old, true, true)) return 3;
        MarkAttempted(old, false);
        if (old.HandoffPhase != "request_pending") return 4;
        MarkAttempted(old, true);
        if (old.HandoffPhase != "requested" ||
            ShouldRequest(old, true) || NeedsRotation(old, true)) return 5;
        old.Enabled = false; old.LastState = "status_wait_user";
        const string next = "https://chatgpt.com/c/22222222-2222-2222-2222-222222222222";
        if (!CanTakeOver(old, next) || CanTakeOver(old, old.Url) ||
            CanTakeOver(old, "https://chatgpt.com/c/unknown")) return 6;
        old.LastState = "visible_ack";
        if (CanTakeOver(old, next)) return 7;
        old.LastState = "status_wait_user";old.Enabled = true;
        if (CanTakeOver(old, next)) return 8;
        // The project ownership invariant must reject a second enabled chat
        // before the lead role is explicitly transferred.
        old.Enabled = false;
        var original = new LiteProjectRegistry.Chat {
            Url = old.Url, Role = "lead"
        };
        var replacement = new LiteProjectRegistry.Chat {
            Url = next, Role = "archive"
        };
        var project = new LiteProjectRegistry.Project {
            Chats = new List<LiteProjectRegistry.Chat> { original, replacement }
        };
        var scheduleNew = new LiteChatWakeRegistry.Chat {
            Url = next, Enabled = true
        };
        if (LiteProjectRegistry.CanScheduleProjectLeads(
            new[] { project }, new[] { old, scheduleNew })) return 9;
        original.Role = "archive"; replacement.Role = "lead";
        if (!LiteProjectRegistry.CanScheduleProjectLeads(
            new[] { project }, new[] { old, scheduleNew })) return 10;
        old.Enabled = true;
        if (LiteProjectRegistry.CanScheduleProjectLeads(
            new[] { project }, new[] { old, scheduleNew })) return 11;
        return 0;
    }
}
