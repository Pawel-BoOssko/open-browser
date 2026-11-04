namespace BridgeBrowserAlpha0;

// Human-readable explanation from durable project wake state; never infer
// receipt/delivery from the registry's scheduling metadata.
internal static class LiteWakeStatusExplanation
{
    internal static string Reason(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "brak zapisanej przyczyny";
        if (code.StartsWith("waiting_", StringComparison.Ordinal))
            code = code["waiting_".Length..];
        return code switch
        {
            "draft_present" => "w starym czacie wykryto niewysłany szkic",
            "generating" or "stall_pending_answer" => "odpowiedź była jeszcze generowana",
            "tool_busy" => "narzędzie nadal pracowało",
            "assistant_not_complete" => "odpowiedź asystenta nie była zakończona",
            "no_composer" => "edytor wiadomości nie był dostępny",
            "response_error" => "błąd odpowiedzi ChatGPT",
            "navigation_changed" => "zmiana rozmowy podczas kontroli",
            "background_navigation_failed" or "rotation_navigation_failed" =>
                "nie udało się otworzyć właściwej rozmowy",
            "wrong_chat" => "otwarta była inna rozmowa",
            "worker_backpressure" => "ograniczono próby odczytu workera",
            "backend_http_429" => "serwer ograniczył liczbę żądań (HTTP 429)",
            "history_not_stable" => "historia rozmowy nie była stabilna",
            "answer_not_stable" => "odpowiedź zmieniała się między odczytami",
            "probe_failed" => "odczyt stanu rozmowy nie powiódł się",
            "status_wait_until" => "model polecił oczekiwanie do wskazanego terminu",
            "status_blocked_recovering" => "odroczenie po blokadzie odpowiedzi",
            "own_draft_clicked_unconfirmed" => "próba wysłania bez potwierdzenia odbioru",
            _ => "kod silnika: " + code
        };
    }

    internal static string Display(LiteChatWakeRegistry.Chat? wake)
    {
        if (wake is null) return "Brak harmonogramu wybudzeń.";
        string When(DateTimeOffset dt) => dt == default
            ? "brak danych" : dt.ToLocalTime().ToString("dd.MM HH:mm:ss");
        string current = wake.Enabled ? "włączone" : "wstrzymane";
        string next = wake.Enabled ? When(wake.DueUtc) : "brak aktywnego terminu";
        string role = (wake.WakeSequence + 1) % 10 == 0
            ? "Nowy czat: rotacja leada po wybudzeniu kończącym się na 9"
            : "Kontynuacja w bieżącym czacie";
        string cause = wake.LastState.StartsWith("waiting_", StringComparison.Ordinal)
            ? Reason(wake.LastState) : Reason(wake.LastDeferralReason);
        string last = wake.LastDeferralUtc == default ? "" :
            "Ostatnie odroczenie: " + When(wake.LastDeferralUtc) + 
            ", do " + When(wake.LastDeferralUntilUtc) +
            ", przyczyna: " + Reason(wake.LastDeferralReason) +
            ", liczba kolejnych odroczeń: " + wake.ConsecutiveDeferrals;
        return string.Join(Environment.NewLine, new[]
        {
            "Wybudzenia: " + current + " | co " + wake.EffectiveMinutes + " min",
            "Oczekuje #" + (wake.WakeSequence + 1).ToString("D6") +
                " | następny termin: " + next,
            "Stan: " + wake.LastState + " | powód: " + cause,
            role,
            last.Length == 0 ? "Ostatni odczyt: bez daty odroczenia" : last,
            "Kotwica harmonogramu: " + When(wake.SlotAnchorUtc)
        });
    }
}