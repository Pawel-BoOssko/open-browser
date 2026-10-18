# Open Browser P0 — checkpoint 2026-10-18 21:54 CEST
## Kontekst i bezpieczeństwo
Źródło: `D:\projects\open-browser` na SATANAMD, historycznie staged/dirty, reguła **DO NOT COMMIT**, tylko Autosync. Nie tworzyć przypominajek ani automatyzacji ChatGPT. Nie restartować/nie zabijać aktywnej przeglądarki właściciela. Narzędzia: LC MultiHost polecenia, RDC edycja plików i testy GUI w sesji Windows 1.

## Punkt 1 — obserwowalność i incydenty
- PBOX1 LIVE nadal 1.64.0, PID18412 (ostatni pomiar 21:28, Responding true).
- Screenshot operatora: `Przekroczono limit czasu dostarczenia wiadomości. Spróbuj ponownie.`; telemetry PBOX1 `response_error_transition`: 2026-10-18T19:26:50.509Z (`delivery_timeout`) i 19:27:10.503Z (`cleared`), czas widoczności ok. 20 s. Nie ma dowodu, że konkretna odpowiedź została dostarczona na serwer. **ZAKAZ AUTOMATYCZNEGO PONAWIANIA.**
- Na LIVE wystąpiły serie `high_guard_probe_timeout` z okresowym timeoutem każdej próby przy interwale 6s. Źródło 1.66: `LiteHighUi` wykorzystuje dotychczasowy `LiteRendererBackpressure` — po nieodpowiadającym rendererze backoff 30/60/120/240/300s zamiast gorącej pętli. Nie traktować timeoutu sondy jak wyniku modelu.
- PBOX1 resources: ok. 40.1% pojedynczego rdzenia przez 123s; working set jednego renderera 1001.8→794.1MiB, 8 procesów WebView2, wolna pamięć 4265MiB (pomiar ok. 20:45). Duże obciążenie, brak dowodu wycieku.

## Punkt 2 — Luna
- PBOX1 wczesny realny test `--lite-luna-connectivity-test`: PASS, credential Windows Credential Manager bez ujawnienia sekretu, `gpt-6-luna`.
- PBOX1 realne API `--lite-luna-novel-scenario` (około 21:02:58) na syntetycznym opisie książki: decyzja `continue`, pewność `high`, wiadomość: *Napisz jeden nowy, ósmy rozdział, kontynuując fabułę i bohaterów po „Czarnym kompasie”. Nie przerabiaj wcześniejszych rozdziałów ani nie twórz kolejnych tomów.* Zapisane lokalnie `%LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\diagnostics\luna-adventure-scenario-advice.json`. Nie były wysłane żadne prywatne treści rozmowy.

## Punkt 3 — High i rzeczywisty DOM
- OpenFill niezależnie od Lite potwierdził w realnym ChatGPT na czacie książki: `data-selected-reasoning-effort=high` na przycisku `aria-label="Wybierz model ChatGPT"`, slider 2/2, wysiłek High 3/3.
- Realna strona używa DIL, `[data-chatgpt-selection-message-id]` asystent i `[data-user-message-bubble]` użytkownik, stare `data-message-author-role` zero wyników. Zmieniono `LiteHighGuard.js`, `LiteResumeProbe.js`, `LiteResumeSender.js` (ACK w obu wariantach). Unchanged hard prune default OFF.
- `LiteResponseSignal.js` obejmuje teraz krótkie błędy w DIL assistant wrapper oraz test negatywny cytatu użytkownika. `LiteResumeUi.cs` nie zezwala na cykl lub preflight, gdy z diagnostyki strony jest aktywny failure.
- `LiteResumePreflight.cs` (w poprzednim zestawie) sprawdza przed wysyłką ponownie High, URL, SHA256 ostatniej odpowiedzi, busy/draft/error, w przeciwnym razie fail-closed.

## Punkt 4 — test nadzorcy
- OpenFill wykonał odczytowe kontrole prawdziwego czatu książki `https://chatgpt.com/c/6ad4d68a-a304-83eb-950a-bb4284f58c79`. High potwierdzone, rozdziały 1–7. Trzy kolejne próby przygotowania jednego nowego rozdziału zostały wstrzymane BEZ wysyłki: pierwszy raz brak porównywalnej migawki, drugi raz rzeczywisty komunikat `Nie udało się wczytać tej odpowiedzi`, trzeci rozpoczął się po zniknięciu błędu i wymaga końcowej weryfikacji. Nie klikać Ponów/Regeneruj, żadnych retry po niepewnej wysyłce.
- Dodano `LiteWebViewIntegrationSelfTest.cs` i flagę `--lite-webview-integration-selftest`: prawdziwy WebView2 w **interaktywnej** sesji Windows 1, świeży tymczasowy profil, **tylko lokalna strona** na wirtualnym hostcie chatgpt.com; bez sieci i bez zalogowanego konta; testuje High, DIL last answer, pojedynczą wysyłkę, DIL ack i blokadę duplikatu. Najnowszy wynik SATANAMD **exit 0**.
- Wersja SOURCE **1.66.0**, istniejący projekt dirty/master; Node 12/12 suites PASS, .NET Debug 0 errors/1 MSB3277, release gate PASS (przed ostatnią zmianą response signal; powtórzyć po sync), rzeczywisty UI WebView2 offline PASS.
- Uwaga: najpierw test z LC MultiHost sesja 0 zwrócił `COMException 0x80070578` (nieprawidłowy HWND). RDC GUI w sesji 1 pozwolił przetestować, odkryto również błąd harnessu (`JsonElement.True.ToString()` w C# dawał `True`), który poprawiono; potem PASS.
- **To NIE stan LIVE E2E prawdziwej rozmowy w Open Browser Lite.** Wydanie 1.66 wymaga PBOX1 STAGED i realnego przełączenia, ale nie wolno tego robić podczas aktywnej sesji użytkownika. Nie oznaczać punktu 4 jako DONE bez potwierdzonego rzeczywistego cyklu.
## Następne kroki
1. Opublikować jedynie snapshot Autosync 1.66, nie ręczny push; PBOX1 pull --ff-only czystego klona, dotnet publish do nowego katalogu, manifest SHA256 i 16 offline selftestów.
2. Sprawdzić izolowany WebView2 test również na PBOX1 interaktywnie (RDC, nie LC session0).
3. Zaktualizować 35-punktowy backlog, current state, field notes i rejestr błędów, bramka dokumentacji.
4. Kontrolowany realny E2E LIVE 1.66 po osiągnięciu idle/no-error i bez utraty rozmów użytkownika; przy jakimkolwiek błędzie dostarczenia `STOP`, nie ponawiaj.
