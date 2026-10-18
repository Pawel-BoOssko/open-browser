# Open Browser Lite — checkpoint implementacji 2026-10-18 15:52

## Źródła i obowiązujący proces
- SOURCE: SATANAMD `D:\projects\open-browser`, git status z 15:38 wykazał istniejącą dirty migrację kilkudziesięciu źródeł; nie resetowano/stage nie dotykano. Wszystkie nowe pliki w istniejącym `src/BridgeBrowserAlpha0` i `docs`.
- Preferowane narzędzia: LC MultiHost do komend, Remote Desktop Commander do modyfikacji plików, Autosync do synchronizacji (bez ręcznego push).
- LIVE PBOX1 (przy poprzedniej kontroli): `OpenBrowserChatGPTLite_20261018_telemetry` PID 15860; aktywne okno w tym czacie, nie zamykać. STAGED PBOX1 (z poprzedniego pakietu): `OpenBrowserChatGPTLite_20261018_notes_dom_voice`, testy PASS, skróty wskazują staged, zadanie Harmonogramu pozostało stare po timeout OB-10.

## Pakiet tej iteracji
1. [SOURCE + PASS] `LiteNotesOutbox.cs`: per-chat DPAPI encrypted outbox; atomowy zapis `File.Replace` z kopią backup, idempotentne EnqueueDraft, diff nowych dopisków, stany `Queued` → `Prepared` → `DeliveryUncertain` → `Confirmed`; nie ponawia prób o nieznanym wyniku; żadnego dostarczania promptów.
2. [SOURCE + PASS] `LiteNotesOutboxSelfTest.cs` i flaga `--lite-outbox-selftest`: testy FIFO, wersji, restartu, szyfrowania, deduplikacji, blokady double-send po crash oraz ręcznego potwierdzenia; EXIT=0 SATANAMD.
3. [SOURCE + PASS] `LiteOutboxWindow.cs`: natywne okno odczytu oczekujących notatek i kopiowania wybranej treści do schowka; żadnego auto-send. **Nie jest jeszcze wywoływane z UI**, bo oddzielny plik panelu przycisków został odrzucony przez narzędzie.
4. [SOURCE + PASS] `LiteTabSwitchGuard.cs` integracja z `LiteProjectsUi.cs`: przed ręczną zmianą czatu zapis notatek; blokada gdy trwa generowanie, jest szkic, stan niepewny lub renderer nie odpowiada; bez zabijania aktywnej generacji. Build C# PASS; test E2E WebView2 pozostał do wykonania.
5. [SOURCE + PASS] `LiteTabResidencyPlanner.cs` + `LiteTabResidencySelfTest.cs`: planowanie max 0/1/2 ciepłych kart według dostępnej RAM, ochrona generating/pending-tool/draft/unflushed-notes, jawna informacja o przekroczonym limicie. Test EXIT=0. **Nie zarządza jeszcze fizycznymi WebView2**.
6. [SOURCE + PASS] `LiteTransportDiagnostics.cs` i `LiteTransportSelfTest.cs`: metadata-only klasyfikator CDP conversation HTTP/transport/websocket; bez body, headers, cookies i pełnych URL; test klasyfikacji 6 warunków exit 0. **Nie jest jeszcze aktywowany w ChatGptLiteForm**, blokada narzędzia OB-14.
7. [PASS] `dotnet build src/BridgeBrowserAlpha0/BridgeBrowserAlpha0.csproj -c Debug --no-restore`: 0 błędów, jedno znane MSB3277. Headless `--lite-outbox-selftest`, `--lite-tabs-selftest`, `--lite-transport-selftest` wszystkie EXIT=0. Wcześniejsze pozostałe testy w projekcie były PASS.

## Nowe blokady / rejestr
- Plik `docs/Rejestr nieudanych komend.md`, wpisy OB-11…14:
  - OB-11: zablokowane jedno połączone wywołanie dwóch rejestracji LC; odrębna rejestracja SATANAMD później PASS.
  - OB-12: zablokowana pojedyncza zmiana inicjalnego klucza notatek `_noteKey` (pozostaje inbox); pierwsze dodanie nowego pola outbox w tej samej iteracji jest potwierdzone.
  - OB-13: zablokowane utworzenie interfejsu dwóch przycisków kolejki; nie ponawiano obejściem.
  - OB-14: zablokowane podłączenie pasywnej diagnostyki CDP do głównego WebView2; nie ponawiano obejściem.
- Nie utożsamiać tych odmów z awarią dotychczasowego build lub obecnego wydania.

## Następne bezpieczne pakiety
1. Test pełnego zestawu JS + .NET i audyt dodatkowych błędów wewnętrznego UI. Aktualizacja dokumentacji architektury; sprawdzić Autosync.
2. Zbudować nowy katalog side-by-side PBOX1, uruchomić wszystkie selftests, NIE przełączać aktywnego ChatGPT podczas odpowiedzi.
3. Zdiagnozować czy **strona** generuje, aby planować bezpieczne wydanie; udokumentować Live/Staged.
4. Trwały, minimalnozasobowy WebView2 tab manager z wykonalną kontrolą pool, bez ryzykownego zamykania aktywnych kart.
5. Zatwierdzić podłączenie kolejki w UI oraz diagnostyki CDP po analizie wpisów OB-13/14 bez obchodzenia zabezpieczeń; dopiero potem rozważać auto-delivery z potwierdzonym idle i idempotency.
6. Włączyć zaawansowaną regresję agresywnego DOM core-v06 vs hard-prune na syntetycznej i realnej długiej rozmowie, z automatycznym wyłączeniem przy React mismatch i pełnym rollback.
7. Rozwijać dashboard, model API, MCP i handoffy z uwzględnieniem nieograniczonej liczby projektów.

## Status wydania
SOURCE aktualny + BUILD Debug PASS; STAGED PBOX1 jeszcze nie zmienił się w tej iteracji; LIVE PBOX1 nadal stara telemetry dopóki nie potwierdzi się bezpieczne przełączenie. Ani zakończenia pełnej aplikacji, ani prawidłowego działania mikrofonu w realnym UI nie zweryfikowano.
