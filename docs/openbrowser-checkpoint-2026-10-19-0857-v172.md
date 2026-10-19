# Open Browser Lite 1.72 — release i wdrożenie oczekujące
2026-10-19, około 08:57 CEST

## Cel operatora
Odzyskać praktyczną możliwość wysyłania wielu wiadomości, z której korzystała wcześniejsza generacja Open Browser, i doprowadzić do faktycznego autonomicznego budzenia tego JEDNEGO czatu projektu co około 25 min. Brak ChatGPT Tasks/reminders. Nie przerywać aktywnej odpowiedzi, nie robić manualnego commit/push.

## Analiza PRE-LITE
Pełne porównanie: `docs/openbrowser-old-sender-audit-2026-10-19-0845.md`. Klucz: stary `MainForm.SendTextToChatAsync` / `InjectImmediatelyAsync` wpisywał HTML contentEditable i po 500 ms klikał przycisk / SVG bez ACK. `PageTap` rozdzielał SSE `response.body.tee()` i przekazywał strumienie do `ResponseExtractor` — potencjalnie kosztowne dla RAM i prywatności. Lite nie powinien bezkrytycznie aktywować pełnego PageTap.

## Fakty LIVE
- PBOX1 v1.71.0 `PID 21560` od 07:44:50, Responding=True, nie zamykano okna.
- PBOX1 DPAPI dev-wake: `Enabled=True`, jeden poprawnie przypisany czat, `IntervalMinutes=25`, `LastState=uncertain_no_ack`, `NextDueLocal=09:00:56` w chwili pomiaru 08:54. Nie publikowano URL ani treści.
- Logi PBOX1 w dniu 19.10: 58 prób wybudzania, 56 potwierdzonych odmów busy przed wysyłką, 2 bez potwierdzonego ACK (ostatnia 08:35); nie było dowodu `dev_wake_ack` w badanej historii. To NIE znaczy, że wszystkie próby na pewno nie dotarły do serwera.
- Samo istnienie timera i włączonego statusu nie dowodzi poprawnego działania automatycznego budzenia.

## SOURCE/TEST 1.72.0
- `LiteResumeSender.js`: przycisk „Wyślij” wybierany z formularza faktycznego edytora (`editor.closest('form')`), akceptuje dokładne aria-label PL/EN i `type=submit`. Fallback `data-testid=send-button` tylko z właściwego formularza, żadnego `button svg`. Brak kliknięcia sąsiadującego fioletowego przycisku głosowego. `diagnose()` loguje wyłącznie metadane czy znaleziono i wyłączono submit, bez treści wiadomości.
- `LiteDevWakeUi.cs`: po braku ACK zdarzenie `dev_wake_delivery_phase` przechowuje phase i `triedClick`, pozwalając rozdzielić `draft_button_missing` od `click_invoked` i transportu o niepewnym wyniku. Nie ponawia oryginalnego receipt.
- `tests/lite-resume-send-race.test.cjs`: 14 PASS, w tym prawdziwy formularz z przyciskiem bez data-testid, przycisk głosowy nieklikany, disabled draft, podmiana edytora.
- `LiteWebViewIntegrationSelfTest.cs`: lokalny fixture `form[data-chatgpt-composer]` + `button type=submit aria-label=Wyślij`, bez testid i obok tryb głosowy. Sprawdzono w rzeczywistym WebView2 osobnego profilu SATANAMD Session1 PASS oraz PBOX1 Session1 PASS (`--lite-webview-integration-selftest`, 13 checków, zero realnych wiadomości). Na PBOX1 proces testu zakończył po ~66s, EXIT0.
- SOURCE SATANAMD node 15/15 suites PASS; Debug .NET 0 errors, 1 historyczny MSB3277; gate dokumentacji i git diff --check PASS.

## STAGED 1.72 na PBOX1
- PBOX1 clean fast-forward branch `autosync/SATANAMD` to `7bb452587048641ff2a340a3cd941f2d7d63b55f`, bez lokalnych zmian.
- `%LOCALAPPDATA%\Programs\OpenBrowserChatGPTLite_v1.72.0_20261019_0847\BridgeBrowserAlpha0.exe`.
- ProductVersion `1.72.0+build.20261019T064733Z.7bb4525870...` (godz. 08:47:33 Warszawa).
- EXE SHA256 `DA6C2DD7B13A1B312EF2F459E7C38948F52ADE845A1A9103FC1A62C33B05159D`.
- Wydanie manifest `openbrowser-release-manifest.json` poprawny, `scripts/verify-lite-release-manifest.ps1` PASS, pełna bramka `check-lite-p0-promotion.ps1` PASS. 17/17 PBOX1 headless .NET tests PASS. Offline WebView2 interakcyjny PASS.
- Skróty PBOX1 Desktop i Start wskazują 1.72.0 `--lite-open-safely`; Pinned taskbar właściciel zmieni we własnym zakresie, nie modyfikowano. Stare techniczne zadanie Scheduler `OpenBrowser ChatGPT Lite` wciąż OB-10 (stary release) — do osobnej zgody/naprawy.
- Uruchomiono **JEDNORAZOWY pomocniczy proces** `PID 19936` w sesji użytkownika, wskazuje dokładnie `v1.72.0 --lite-await-upgrade 21560`; czeka na NATURALNE zamknięcie starego okna, bez force i bez uruchomienia drugiego profilu. Potwierdzono 1 aktywny browser. Jeśli stary proces nie wyjdzie, LIVE pozostanie 1.71.0 — nie mylić STAGED z LIVE.
- Po naturalnym przełączeniu 1.72 wróci do przypiętego czatu z DPAPI i zachowa włączoną Autopracę. Sprawdzić nowy `dev_wake_loaded`, `dev_wake_send_attempt`, `dev_wake_delivery_phase`, `dev_wake_ack` i brak duplikatu.

## Kryterium ukończenia i dalsze kroki
1. Nie określać budzenia jako potwierdzonego bez **widocznego DIL ACK** i dalszej odpowiedzi, albo innego jednoznacznego dowodu powiązanego z receipt.
2. Po naturalnym zamknięciu starej wersji uruchomiony helper powinien automatycznie wypromować 1.72; zweryfikować PID/path, UI High i brak niepewnych draftów.
3. Po następnym 25-minutowym cyklu przejrzeć zdarzenia `dev_wake_delivery_phase` i zidentyfikować ewentualny realny powód `no_ack` (kliknięcie, brak przycisku, niedopuszczalne drafty, transport).
4. Dopiero potem przejść do ulepszania stabilności, DOM i reszty 86-punktowego `docs/design/openbrowser-master-roadmap-v3-2026-10-19.md`.
5. Zero ChatGPT reminder, brak manualnego commit/push, brak obejścia skanera binariów.
