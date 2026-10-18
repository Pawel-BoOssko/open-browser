# Open Browser Lite — checkpoint po pakiecie UI/DOM/diagnostyka
Data: 2026-10-18 15:15 CEST. Projekt: D:\projects\open-browser (SATANAMD). Wydanie STAGED: PBOX1.

## Ustalona architektura
- Dowolna liczba projektów i czatów; jeden lead projektu z lease/fencing. Native WinForms project sidebar + prawy stały notes-per-chat.
- Karty docelowo bounded warm (1 aktywna + max 1–2 gorące, pozostałe uśpione/zaparkowane), nie 7 rendererów naraz. Nie przerywać strumieni modelu.
- Użytkownik nie potrzebuje starych tur po scrollowaniu w Open Browser; historia zostaje w ChatGPT / Firefox. Wersja eksperymentalna hard prune usuwa starsze elementy DOM gdy >=90 tur, zostawia 24, nie podczas generacji/draftu/selekcji. Zawiera toggle. Wymaga próby na rzeczywistym React.
- Stale otwarty natywny notes na boku w każdym chat, lokalna atomowa szyfrowana DPAPI notatka, autozapis, Win+H dictation, ręczna kopia „Informacje od Pawła”. Auto przekaźnik po zakończeniu do implementacji po wiarygodnej detekcji oraz idempotency.
- Mikrofon w ChatGPT WebView2: CoreWebView2.PermissionRequested Microphone Allow tylko dla chatgpt.com. Nie testowano jeszcze realnej zgody Windows i dyktowania.
- Znikanie okna: LiteWindowShell NotifyIcon/tray i restore; ShowInTaskbar=true. Ikona wygenerowana jako natywne .ico, PBOX1 eksport PASS. Pinning do Windows Taskbar zależy od polityk shell i może wymagać gestu użytkownika.

## Dowody
- SOURCE SATANAMD: LiteChatNotes.cs, LiteNotesUi.cs, LiteNotesSelfTest.cs, LiteDomHardPrune.js, LiteDomPruneUi.cs, LiteResponseSignal.js, LiteResponseDiagnostics.cs, LiteBrandIcon.cs, LiteWindowShell.cs; minimalne integracje ChatGptLiteForm, Program.cs i csproj.
- SATANAMD Debug build PASS, 0 error, znane MSB3277. Node testy DOM CSS 5, Hard Prune 8, Snapshot 5, Response signals 7 => 25 PASS.
- PBOX1 klon Git `autosync/SATANAMD` 4be51a79f5c8, dirty przed pull=0. `dotnet publish Release` PASS, 0 errors, znane MSB3277; osobny katalog `%LOCALAPPDATA%\Programs\OpenBrowserChatGPTLite_20261018_notes_dom_voice`.
- PBOX1 headless self-tests: --lite-notes-selftest EXIT=0, --lite-projects-selftest EXIT=0, --lite-archive-selftest EXIT=0, --lite-lease-selftest EXIT=0, --lite-journal-selftest EXIT=0, --lite-icon-export EXIT=0. Wszystko bez okna ChatGPT i bez naruszania profilu.
- PBOX1 aktualne skróty OneDrive\Pulpit i Start Menu wskazują nowe STAGED i %LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\OpenBrowser.ico, ARG=--chatgpt-lite.
- LIVE PBOX1 nadal STARA wersja telemetry PID15860 w sesji user. Nie zamykano, nie resetowano, nie zmieniono profilu. Task `OpenBrowser ChatGPT Lite` nadal na starszym exe; aktualizacja Set-ScheduledTask timeout/blocked OB-10.

## Nieudane operacje
Rejestr centralny: `docs/Rejestr nieudanych komend.md`. OB-10: LC MultiHost PBOX1 Set-ScheduledTask pojedyncza zmiana akcji, >100 s timeout, cancelled, task nadal stary. Nie ponawiać równoważnej operacji innymi narzędziami. Poprzednie odmowy OB-01…OB-09 dokładnie opisane w rejestrze.
W tym pakiecie kodowania i testów nie odnotowano nowych odmów zabezpieczeń, tylko timeout Scheduler OB-10.

## Do następnych pakietów
1. E2E WinForms (native projekty, notes, mic, tray, WebView) na bezpiecznym niezalogowanym profilu smoke, bez restartu LIVE.
2. Analiza React po hard DOM removal i ewentualnie przeniesienie trymera core-v06 do optymalizacji stanu początkowego rozmowy z testami serializacji; użytkownik chce agresywnie, lecz niezawodnie.
3. Bounded warm tabs manager + monitor per tab i adaptive RAM.
4. Durable work/event journal i per-chat notes queue z bezpiecznym idempotentnym dostarczeniem tylko po confirmed idle/finishing, bez powielania poleceń.
5. Diagnostyka „Nie udało się wczytać tej odpowiedzi”: korelacja client send/HTTP/WebSocket/renderer/render/snapshot; raport stopnia pewności.
6. Bezpieczne przełączenie STAGED -> LIVE bez niszczenia bieżącej sesji oraz kontrolowany rollback (starszy katalog telemetry istnieje).
7. Rejestr projektu/MCP/API Luna/provider i dashboard, kontynuowanie backlogu do wykonania.
