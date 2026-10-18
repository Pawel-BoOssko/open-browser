# Open Browser Lite — inwentaryzacja, punkty przywracania, stan prac
Data: 2026-10-18 18:28 CEST. Dokument operacyjny, źródło prawdy dla bieżącej rozmowy.

## Najważniejsza decyzja operatora — BEZWZGLĘDNA
- Do odwołania: **NIE uruchamiać ani nie tworzyć żadnej automatyzacji/przypominajki ChatGPT**. Rozwój WYŁĄCZNIE w aktualnej rozmowie prowadzonej świadomie przez operatora. Nie włączać zadań samodzielnie; zmiana tylko na wyraźne polecenie właściciela.
- Weryfikacja stanu z aplikacji ChatGPT o 18:16: 32 zapisane automatyzacje is_enabled=false, w tym Open Browser. Zadania Windows monitor zasobów i Autosync to techniczne usługi użytkownika, NIE przypominajki ChatGPT; pozostawione bez zmian.
- Automatyzacja z godz. 17:52 prowadziła do osobnego czatu. Ostatni jej odrębny plik na źródłach 18:04: `docs/openbrowser-checkpoint-2026-10-18-1804-readonly-audit.md`. Z dzisiejszych czasów ostatnich modyfikacji .cs/.js przed audytem: nie stwierdzono ingerencji tej sesji w kod po 17:28. Zapis źródła może mieć inne równoległe migawki Autosync.
- Na PBOX1 zidentyfikowano wyłącznie pomocniczy, czekający na wyjście starej wersji proces `BridgeBrowserAlpha0.exe --lite-await-upgrade 21532` PID19760 z wydania 1.61.0. Proces anulowano po potwierdzeniu commandline; NIE dotykano produkcji PID21532. Nie zostawiamy ukrytego oczekującego upgrade po audycie i nie uruchamiamy nowego.

## Inwentaryzacja Git / źródła
- Jedyny kanoniczny katalog SOURCE: SATANAMD `D:\projects\open-browser`; branch master HEAD `374ed2981153c81fed744fd9d7c74e743ccb3bda`. Uwaga: lokalny master NIE jest tożsamy z nowszym oddzielnym autosync/SATANAMD, który posiada snapshoty niezacommitowanych plików.
- Stan PRZED DZISIEJSZYMI POPRAWKAMI: 38 staged rename/migration plików z releases do src (9 z dodatkowymi unstaged edits), 10 innych modified tracked i około 91 untracked. Katalog src zawiera 43 nowe Lite*.cs i 9 nowych JS test suites. Brak Git stash. **Nie resetować ani nie commitować całej historii**; w repo są nierozliczone zmiany wielu wcześniejszych czatów.
- PBOX1 czysty klon `C:\Users\pboru\Documents\Projects\open-browser-lite-source`, branch autosync/SATANAMD, ostatnio zweryfikowany SHA `020d67e`; przy ostatnim remote odczycie autosync/SATANAMD `72dc83d17971ddb133d852a6d3df638619c5cc71`. Aktualizacja fast-forward jest planowana po zakończeniu tej inwentaryzacji i testów.
- Drzewo repo: główne pliki w src/BridgeBrowserAlpha0, tests, docs i scripts. Zmiany pozostają w tym drzewie; GitHub Autosync zapewnia niezależne migawki, ale nie porządkuje staged index automatycznie.

## Zweryfikowane punkty przywracania — nie naruszać
1. SATANAMD lokalne archiwum SPRZED napraw (zawiera src/docs/scripts/tests i .gitignore, bez bin/obj, poza Git): `%LOCALAPPDATA%\OpenBrowser\Recovery\open-browser-worktree-20261018-1820-before-fixes.tar.gz`. SHA256: `E1105A88F4206061AD00EC2A9AC52F698DDC799E139DF481CAF358966B09F7D3`, 231626 bajtów, 242 wpisy tar, test listowania OK. To kopia źródeł, NIE backup profilu ChatGPT.
2. PBOX1 prywatne dane projektu i lokalne zaszyfrowane notatki: `%LOCALAPPDATA%\OpenBrowser\Recovery\pbox1-20261018-1825`. Zawiera kopię `projects.json` (SHA256 `C646B01909FE5B3591547A5D763D0340285692674DFF3C539E0259DF4AB193D2`) i katalog `notes`. Profil aktywnej sesji WebView2 i cookies nie były kopiowane podczas pracy renderera.
3. Nienaruszone binarki wydania PBOX1: LIVE `v1.60.0_20261018`, SHA256 EXE `F0B3151A5BAD6A93545D2B46061EA86402BAB0F50E055E03B8C6B0A91C05322A`; STAGED `v1.61.0_20261018`, SHA256 EXE `8E1B14EB4242AEB559FD9BEAF449E951AE5A81BD33FA32D7026D203CE8F2F25F`.
4. Starsze katalogi PBOX1: notes_dom_voice, 1451_archive_leases, archive_dashboard, projects_dom, telemetry. Pozostawić do czasu zweryfikowanego wydania i rollbacku. Oddzielne snapshots origin/autosync/SATANAMD; lokalny Git master HEAD 374ed2.
5. Logi diagnostyczne PBOX1 w `%LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\diagnostics\lite-events-2026-10-18.jsonl` i `resources-2026-10-18.jsonl`; monitor Windows Task Scheduler „OpenBrowser Lite Resource Monitor” był aktywny i generował dane.

## Wdrożenia i błędy
- LIVE PBOX1: PID21532, EXE `OpenBrowserChatGPTLite_v1.60.0_20261018\BridgeBrowserAlpha0.exe`, od 16:59:36, Responding=true przy kontroli 18:17, nie oznacza to responsywnego renderera. Osobny updater PID19760 zatrzymany, brak oczekujących helperów.
- STAGED PBOX1 v1.61.0 zawiera Luna/High i wcześniejszego sendera z błędem wyścigu; **NIE PROMOWAĆ**. Harmonogram Windows zadania ręcznego startu OpenBrowser ChatGPT Lite nadal wskazuje stary telemetry EXE (timeout wcześniejszej operacji OB-10). Skróty pulpit/Start wskazują 1.61.0: po manualnym uruchomieniu można dostać niezakwalifikowaną wersję — do aktualizacji po nowym staging.
- WebView2 `RenderProcessUnresponsive` 17:02:35, poprzedzony archive/dom probe timeout. WinForms host żywy, nie dowodzi poprawności Chrome/React. W przeszłości RAM systemowy spadał do 85MiB; bieżąco ok. 8GiB wolnego. Nie ma dowodu na wyciek pamięci.
- W automatycznym teście książki OpenFill odnotował pojedynczy prompt `Nocna przeprawa`, High/Wysoki widoczny, odpowiedź była w trakcie generowania. Nie jest to dowód poprawnego autonomicznego wznowienia przez Lite.
- Luna API rzeczywiście przetestowana na syntetycznym rozdziale: gpt-6-luna i Credential Manager `FileScannerAI/OpenAIAPIKey`, test HTTP EXIT0 i decyzja `continue`; żadnych sekretów w repo ani w notatkach.

## Spis funkcji (realny status)
- Podstawowe WebView2/logowanie/profil / ikona/tray / skróty: LIVE w zakresie v1.60, smoke testy i telemetry. Wersja + data buildu: LIVE v1.60, staged v1.61.
- Projekty: rejestr bez limitu liczby + panel/bukmarki: SOURCE / częściowo LIVE; brak automatycznej integracji wszystkich czatów/projektów ChatGPT i wielokartowego poolu.
- Notatki per chat: szyfrowane, panel boczny i schowek: SOURCE, wcześniejsze staged/live; brak potwierdzonej w UI end-to-end kolejki niezawodnej dostawy.
- Samoodzyskiwanie odpowiedzi: DPAPI lokalne migawki i log zdarzeń — częściowe, brak potwierdzenia kompletnych odpowiedzi utraconych na backendzie.
- High: selektor i guard w SOURCE/STAGED v1.61; prawdziwy UI ChatGPT Wysoki sprawdzany w OpenFill, nie w Lite. Nie wolno twierdzić, że przełączono jeśli nie potwierdzono.
- Luna planner i ledger niepowtarzalnej wysyłki: SOURCE/STAGED v1.61 i test API; autonomiczny send E2E nieukończony, niepromowalny.
- Agresywny DOM: podstawowa optymalizacja w LIVE, hard pruning w SOURCE, testy syntetyczne; po poprawce SOURCE domyślnie wyłączony do realnego testu React.
- Karty 1 aktywna + do 2 warm: SOURCE planner, brak fizycznego multi-WebView2 i QA.
- Własny MCP/panel instalacji MCP, panel technologiczny, końcowy updater/rollback, duży soak: backlog, nie LIVE.

## Naprawy wykonane po zabezpieczeniu kopii
- `LiteResumeSender.js`: po 250ms recheck URL czatu, identycznego edytora, identycznego payloadu i busy; eliminuje nadpisanie prywatnego szkicu lub wysyłkę do innej rozmowy. Node lite-resume-send-race 5 PASS, lite-resume-agent 7 PASS.
- `LiteDomPruneUi.cs`: default false = JS false, log default false. W `ChatGptLiteForm.cs` ponownie aplikowany toggle po NavigationCompleted. Node lite-dom-default-contract 4 PASS, lite-dom-hard-prune 9 PASS. To świadomy tymczasowy bezpieczny default; wymaganie operatora ostrego przycinania pozostaje TODO E2E.
- Wersja SOURCE podniesiona do 1.62.0 (nowy build wymaga staged PBOX1). Debug build 0 błędów, jedno historyczne MSB3277.
- Cały lokalny test matrix: **9/9 zestawów JS PASS** i **13/13 headless .NET PASS** na SATANAMD. `check-lite-p0-promotion.ps1` bez stage: PROMOTION_GATES_PASS.
- Bieżące poprawki nie usuwają pierwotnych ryzyk niewysyłki i współpracy wielu czatów; **nie publikować w LIVE wyłącznie na podstawie unit testów**.

## Dalsza kolejność, wyłącznie manualny czat
1. Domknąć niezależne testy i dokumentację, stworzyć PBOX1 side-by-side v1.62.0 z manifestem i pełną bramką regresji; nie restartować aktywnej sesji bez dowodu idle.
2. Poprawić ręczne skróty/timing w ramach kontrolowanego release; nie uruchamiać ukrytego watchera do automatycznego promotion bez oceny.
3. Prawdziwy E2E High + Luna + testowy czat z książką, 1 wysyłka, idempotency, potwierdzenie niepowtórzenia; wymaga kontrolowanego użycia LIVE.
4. Skorelować renderer CDP/timeout/React/snapshot, ograniczyć pamięć, realne testy DOM i stopniowo wdrożyć agresywne przycinanie starszych wiadomości.
5. Projekty, notatki/dyktowanie, bounded WebView tab pool, MCP, handoff, kontrolowany updater/rollback, QA+soak wg planu `docs/design/openbrowser-full-delivery-plan-v2-2026-10-18-1626.md`.
6. Weryfikować source/test/staged/live osobno. Kolejny commit decyzja DO NOT COMMIT, dopóki nie rozliczono zastanej migracji i nie zakwalifikowano zakresu. Nie zmieniać historii cudzych prac.

## Potwierdzenie wydania STAGED 1.62.0 — 18:35 CEST
- PBOX1 czysty klon `autosync/SATANAMD` z SHA `bf71fb1fd1b3a4f705cf41eeb86cc0f072f936df` przed buildem. `dotnet publish Release` do `%LOCALAPPDATA%\Programs\OpenBrowserChatGPTLite_v1.62.0_20261018`: 0 błędów, 1 historyczne MSB3277. ProductVersion `1.62.0+build.20261018T163352Z.bf71fb1...`, czyli 18:33:52 czasu Warszawy; exe SHA256 `6278282a15e66bf81b03cd1153a34d4156a04febfd718ccfe05e8d1d91ff413a`.
- Utworzony obok EXE `openbrowser-release-manifest.json` z wersją, prawdziwą godziną kompilacji, SHA kodu i EXE; weryfikator hash PASS. Bramka `scripts/check-lite-p0-promotion.ps1 -StageDirectory ...`: **PROMOTION_GATES_PASS**, wszystkie 5 wymaganych zestawów JS i manifest.
- PBOX1 headless `--lite-*-selftest`: 13/13 PASS; SATANAMD 9/9 zestawów JS PASS, 13/13 .NET PASS. Realny renderer/React/High/auto-resume E2E nadal niezweryfikowane, więc STAGED nie LIVE.
- Skróty PBOX1 Pulpit i Start wskazują 1.62.0 z `--lite-open-safely`; nie uruchomiono nowego WebView2 ani procesu upgrade, produkcja v1.60.0 PID21532 nienaruszona. Windows Task Scheduler `OpenBrowser ChatGPT Lite` nadal wskazuje starszą wersję telemetry, do naprawienia po ocenie wcześniejszej blokady OB-10; nie obchodzono jej.
- Test odtworzenia archiwum SATANAMD: `tar.exe -xzf` do `%LOCALAPPDATA%\OpenBrowser\Recovery\restore-smoke-20261018-1837` EXIT 0, 210 plików, kluczowe źródła/testy/dokumenty obecne. SHA256 archiwum potwierdzony drugi raz. Nie było zmiany źródeł przez operację przywracania.
- Dodatkowe błędy techniczne znalezione/naprawione: w generatorze manifestu niepoprawne statyczne `[TimeZoneInfo]::GetUtcOffset(...)` zastąpiono `$zone.GetUtcOffset($built)`; test generatora+walidatora SAT i PBOX PASS. Test walidacji manifestu poza katalogiem EXE celowo zwrócił `Manifest executable is missing` — kontrakt wymaga manifestu przy EXE, co jest poprawne.
- **BEZWZGLĘDNA ZASADA:** 32 automatyzacje ChatGPT wyłączone; nie tworzyć nowych ani nie włączać istniejących bez nowego wyraźnego polecenia użytkownika. Autosync pozostaje wyłącznie technicznym synchronizatorem źródeł.

## Decyzja o dalszym wdrożeniu
`COMMIT: DO NOT COMMIT` — w master pozostało 38 staged relocations oraz liczne wcześniejsze modified/untracked; nie mieszać tych cudzych zestawów bez osobnej konsolidacji i selektywnego rozliczenia. Dokumentacja i testy sprawdzone; głównym kanałem kopii jest nadal Autosync. Do LIVE 1.62 nie przełączać w sposób wymuszający zamknięcie obecnego okna lub niezakończonego zadania, a przed auto-send nadal wykonać prawdziwe E2E React.
