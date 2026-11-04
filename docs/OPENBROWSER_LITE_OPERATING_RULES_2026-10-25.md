# Open Browser Lite — obowiązujące zasady operacyjne
Data decyzji: 2026-10-25. Właściciel: projekt Open Browser Lite.

## NADRZEDNY OWNER OVERRIDE 2026-11-01: ROZWOJ FUNKCJONALNY ZAMIAST NIESKONCZONYCH TESTOW
NAJWYZSZY PRIORYTET, obowiazuje wszystkie przyszle wybudzenia i nowo utworzone czaty. Pelna obowiazujaca wersja znajduje sie NA POCZATKU D:\projects\open-browser\AGENTS.md pod naglowkiem OWNER OVERRIDE 2026-11-01. Ta decyzja uchyla starsze wymagania wielokrotnego QA, sztucznych awarii, 30-minutowych/24h/100-probowych testow stabilizacyjnych i powrotow do formalnych kryteriow juz funkcjonujacych mechanizmow, o ile nie zgloszono nowego realnego incydentu.
- Przyjmij, ze dotychczasowe okolo 240 tur pracy i dzialajacy LIVE daja wystarczajace potwierdzenie dotychczasowych funkcji. Nie szukaj wymyslonych bledow i nie generuj kolejnego wydania tylko dla testow.
- Na poczatku kazdego wake: krotki checkpoint + Beads scan/ready + istotny fragment kanonicznego docs/OBL_BACKLOG.md. Rozroznij rzeczywiscie GOTOWE, ROZPOCZETE i BRAKUJACE FUNKCJE, niezaleznie od historycznych otwartych testow odbiorczych.
- Wybierz najwyzej priorytetowa brakujaca funkcjonalnosc, napisz implementacje, wykonaj tylko nieodzowne krotkie sprawdzenie ZMIENIONEGO KODU i wdroz na SATANAMD z zachowaniem SOURCE/STAGED/LIVE/SHA/rollback. Przejdz do kolejnej brakujacej funkcji bez zwloki.
- P0-07 realny 30-minutowy crash/UNCERTAIN E2E, kolejne poprawki hipotetyczne i duze suite testow sa WSTRZYMANE jako samodzielny cel. Wroc do nich tylko po faktycznym, konkretnym bledzie produkcyjnym lub nowym poleceniu wlasciciela.
- Kontrola anty-petli: kolejne wake nie moze byc ponownie 'tylko sprawdzilem i poprawilem test'. Po braku postepu funkcjonalnego wybierz inny niezaimplementowany element. Blokady poszczegolnych funkcji zapisz z przyczyna, pracuj nad nastepnymi.
- Raport oraz handoff musza wymieniac nazwe nowej funkcji, zmienione pliki, status wdrozenia i nastepna brakujaca funkcje. Nie myl liczby testow z postepem projektu. Decyzja jest trwala i ma pierwszenstwo przed wszystkimi historycznymi opisami cykli QA w tym dokumencie.

## 1. Jedyne środowisko produkcyjne
- **SATANAMD**: jedyny host SOURCE, kompilacji, testów GUI, LIVE, silnika wybudzeń, wdrożeń i rollbacku.
- **PBOX1 jest poza zakresem**. Żadnych wdrożeń, restartów ani skryptów OBL na PBOX1. Stare prompty mówiące „wdrażaj na PBOX1” są nieaktualne i nie wolno ich wykonywać.
- Dostęp operacyjny: **LC MultiHost, target=satamad**, sprawdzać COMPUTERNAME=SATANAMD. Nigdy nie używać domyślnego target=pbox1 dla tej pracy.

## 2. Najważniejsza zasada: ostrzony nóż ma być używany
**Cyklem podstawowym jest: POPRAWKA → TESTY → STAGED → WDROŻENIE NA SATANAMD → POTWIERDZENIE LIVE → KORZYSTANIE Z NOWEGO LIVE PRZY TWORZENIU NASTĘPNEJ POPRAWKI.**
- Nie kumulować wielu ukończonych poprawek offline bez aktualizowania działającej wersji.
- Nie kończyć iteracji po samych testach. Jeżeli przejście na LIVE jest możliwe i akceptowalne, należy uruchomić nową wersję i pracować odtąd właśnie w niej.
- Gdy release nie jest bezpieczny, zapisać jawnie **NO-GO: powód, aktualne LIVE, kandydat, rollback i krok odblokowania**. Nie udawać wdrożenia.
- Decyzja właściciela: preferować szybkie, małe wdrożenia z odwracalnym rollbackiem zamiast długiego czekania na perfekcję.
- Rozróżniać cztery dowody: testy źródeł, testy uruchomionego EXE, **rzeczywisty proces LIVE na SATANAMD**, efektywność odczuwalna w codziennej pracy.

## 3. Niezawodność wybudzeń i samodoskonalenie
- OBL jest narzędziem PRACY TEGO CZATU. Celem jest ciągła, samodzielna i wydajna praca nad OBL, a nie samo generowanie raportów czy kolejnych testów offline.
- P0: wybudzenie, bezpieczne wznawianie zadania, tylko jeden aktywny czat wykonawczy, priorytet wake ponad unikaniem duplikatow, CHECKPOINT i ACK potwierdzony dowodem silnika.
- Silnik wybudzania OBL jest źródłem harmonogramu; nie używać ChatGPT Tasks.
- Dziennik silnika: %LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\diagnostics\wake-lifecycle.jsonl na SATANAMD. Nigdy nie fabrykować ACK.
- Status realizacji i checkpoint: D:\projects\open-browser\docs\OPENBROWSER_LITE_CHECKPOINT.md oraz OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md na SATANAMD.
- Każde wybudzenie rozpoczyna się od odczytu bieżących LIVE, SOURCE, stanu silnika i checkpointu; kończy zapisem faktycznie wykonanych czynności, ich weryfikacją oraz następnym krokiem.
- Po faktycznym restarcie LIVE konieczne potwierdzenie pojedynczego PID, wersji, WebView2, działania UI, dziennika i rollbacku. Nie uruchamiać równolegle drugiego LIVE.

## 4. Kryteria prac i zasoby
- RAM/DOM/CPU: ostre ograniczanie starej zawartości DOM, archiwizowanie do trwałych plików bez utraty danych, testy na długich rozmowach, kontrola kosztów zasobów.
- Nowe funkcje weryfikować w prawdziwej interaktywnej sesji Windows; testy statyczne w Pythonie nie dowodzą działania PowerShell/C#/WebView2.
- Nie robić ręcznie git commit/push/reset. Nie przerywać niepewnej wysyłki i nie powtarzać jej bez dowodu.
- Rzeczywiste wersje: od 2026-10-25 13:50:59 CET LIVE 1.72.95 PID51892, poprzednia 1.72.92 jako fallback (potwierdzić przy następnym odczycie, bo PID może się zmienić).

## 5. Zakaz regresji konfiguracji
- Każda aktualizacja instrukcji wybudzeń musi wymieniać tylko SATANAMD. Błędne, starsze opisy hostów nie są wiążące.
- Naprawić zapisany prompt harmonogramu OBL u źródła przez zweryfikowane API/serializację i odczyt po zapisie; nie edytować DPAPI jako zwykłego tekstu.
- Przy każdym wydaniu sprawdzić, czy nowa instancja jest uruchomiona i nadaje się do dalszego własnego rozwoju; dopiero wtedy przejść do następnej poprawki.

## 2026-10-26 17:25 CET — Nowsza decyzja właściciela: wymuszone wdrażanie i auto-recovery (NADRZĘDNE)
- Właściciel jednoznacznie zmienił wcześniejszą zasadę czekania na idle: wdrożenie NIE czeka na zakończenie generowania, szkic, toolBusy ani stare odpowiedzi. Proces OBL należy wymuszenie zakończyć, potem uruchomić nowy sprawdzony EXE, zweryfikować jeden PID i GUI, a przy błędzie wykonać rollback.
- Obowiązuje wyłącznie host SATANAMD, LC MultiHost target=satamad. Zakaz PBOX1, ChatGPT Tasks, ręcznego Git commit/push.
- Kanoniczny skrypt wymuszonego wdrożenia: D:\projects\open-browser\scripts\obl-force-deploy-satanamd.ps1 -Mode Deploy. -Mode Restart dotyczy obecnego LIVE i napraw awarii. Skrypt używa wersjonowanego EXE/SHA, zabija wyłącznie BridgeBrowserAlpha0 z jego procesami potomnymi, stosuje blokadę Keepalive, sprawdza zdrowie GUI i atomowo zapisuje release state.
- Windows Scheduled Task OBL Force Recovery SATANAMD co 5 minut uruchamia scripts\obl-watch-recover-satanamd.ps1. Wykrywa queued release >1min, brak procesu, brak świeżego preflight >5min, wybudzenie z ponad trzema odroczeniami i brakiem ACK >=25min. Wtedy wymusza procesowy restart; 10min cooldown ogranicza restart loop.
- Zachowaj w dzienniku fakty, nie wymyślaj ACK. Przed restartem checkpoint, ale nie czekaj na stop/generating/draft/toolBusy. Użytkownik zaakceptował utratę trwającej niezapisanej odpowiedzi w zamian za brak wielogodzinnej blokady.
- Pozostałe dotychczasowe instrukcje o bezwzględnym wymogu idle przed restartem są uchylone w zakresie wdrożeń i auto-recovery.

## 2026-10-26 18:17 CET — Stałe sprzątanie projektu i ograniczenie kontekstu
- Obowiązkowe po każdej iteracji. Podczas wybudzenia czytaj tylko docs/OBL_CONTEXT.md, docs/OPENBROWSER_LITE_CURRENT_STATUS.md, kilka ostatnich sekcji docs/OPENBROWSER_LITE_CHECKPOINT.md, AGENTS.md i OPERATING_RULES. Nie wczytuj automatycznie starych WORK_STATUS, HANDOFF, dziesiątków plików docs ani historycznych ZIP.
- Archiwum odpowiedzi: przechowuj tylko 12 ostatnich unikatowych tur bieżącego czatu, najnowszy FINAL lub aktualny PARTIAL. Stare duplikaty i wszystkie nieaktywne czaty lokalnego archiwum odpowiedzi usuwaj; nie dotyczy historii ChatGPT na serwerze.
- Ochrona bieżącego zapisu: pozostaw pliki aktualizowane w ostatnich 180 sekundach. Indeks index.jsonl porządkuj dopiero po 180 sekundach bez aktualizacji, bez dotykania zapisu w toku. Gdy nie znamy aktywnego URL albo preflight jest przeterminowany, fail closed, bez usuwania.
- Automatyczny cleaner: scripts/obl-archive-retention-satanamd.ps1 -Mode Apply -KeepTurns 12 -GraceSeconds 180, Windows Task OBL Archive Retention SATANAMD co 15 min. Zweryfikuj aktualny URL i SHA256 katalogu przed kasowaniem; zapisuj wyniki w LOCALAPPDATA/OpenBrowser/ChatGPT-Lite/diagnostics/obl-archive-retention.jsonl.
- Artefakty build: scripts/obl-clean-generated.ps1 -Mode Apply, Windows Task OBL Generated Cleanup SATANAMD codziennie 03:30. Wolno usuwać tylko odtwarzalne diagnostics/build-##### gdy istnieje sprawdzone wydanie i katalog jest starszy niż 2h.
- Checkpointy: docelowo nie więcej niż 32 KiB w aktywnym CHECKPOINT.md. Po przekroczeniu limitu archiwizuj PEŁNĄ historię do docs/history ZIP, zweryfikuj hash SHA256 i pozostaw ostatnie kilka etapów z odsyłaczem. WORK_STATUS to tylko skrót/odnośnik. W checkpointach pisz maksymalnie 6–12 krótkich wierszy na etap.
- NIGDY nie kasuj src, zmian Git, działającego i poprzedniego EXE release, profilu przeglądarki, DPAPI i credentiali, konfiguracji wybudzeń, wake-lifecycle.jsonl ani ostatnich checkpointów. Wątpliwe pliki diagnozuj, nie usuwaj na chybił trafił.
- Przed i po sprzątaniu sprawdzaj pojedynczy LIVE, WebView2, działanie watchdog i stan archiwum. Bez zmian na PBOX1, bez ChatGPT Tasks i ręcznych Git commit/push/reset. Błędy zadania czyszczenia zapisz w checkpointcie i diagnozuj.2026-10-26 19:13 CET: KANONICZNY BACKLOG wszystkich znanych wymagan wlasciciela = docs/OBL_BACKLOG.md (34 ID, P0/P1/P2, status i acceptance). Przy wyborze zadania porownuj z nim; dawne EXECUTION_PLAN/MASTER_PLAN to historia, nie drugi aktywny backlog. Szczegolowy audyt = docs/OBL_REQUIREMENTS_AUDIT_2026-10-26.md; nie wczytywac go w kazdym wake.

## 2026-10-27 05:50 CET - KRYTYCZNE: poprawny kontekst sesji Windows przy wdrozeniu GUI
LC MultiHost execute_command target=satamad uruchamia PowerShell w SessionId=0; uzytkownik pboru/Explorer i dzialajacy OBL sa w INTERAKTYWNEJ SessionId=1. Uruchamianie OBL przez Start-Process z LC Session0 i wywolywanie obl-force-deploy-satanamd.ps1 -Mode Deploy bezposrednio przez LC jest ZABRONIONE: WebView2 inicjalizuje sie z COMException HRESULT 0x80070578 i GUI_HEALTH_TIMEOUT, nawet dla poprawnego poprzedniego EXE. Test 2026-10-27 05:36-05:40: oba uruchomienia nieudane, stan launching i brak sprawnego okna.
POPRAWNE WDROZENIE: (1) zweryfikuj jeden dotychczasowy BridgeBrowserAlpha0.exe w SessionId=1 i SHA, (2) release-manager-satanamd.ps1 -Mode Queue -CandidateExe <EXE> -ExpectedSha <SHA>, (3) Start-ScheduledTask -TaskName 'OBL Force Deploy 17299', skonfigurowany Principal.LogonType=Interactive i uruchamiajacy obl-force-deploy-satanamd.ps1 -Mode Deploy, (4) sprawdz dziennik obl-force-deploy.jsonl stage deploy_success, phase trial, jeden PID w SessionId=1, rzeczywisty WebView2 i live-preflight; w razie awarii rollback poprzedniego EXE w INTERAKTYWNEJ sesji przez istniejace zadanie Keepalive. Nie tworz nowych zadan Windows i nie uruchamiaj EXE z Session0. Zweryfikowany LIVE v1.73.30 PID12096 Session1 i backend HTTP200.
To uzupelnienie ma pierwszenstwo przed starszymi instrukcjami mowiacymi, by z LC bezposrednio wywolywac force-deploy. Te instrukcje dotycza mechanizmu, ale NIE jego uruchamiania w sesji bez pulpitu.
## OWNER RULE 2026-10-28 PERMANENT AUTOSTART AND STABLE LAUNCHER
- Każde wdrożenie nowej wersji OBL MUSI zaktualizować jeden kanoniczny launcher i skróty w menu Start, na pulpicie oraz autostart Windows. Nie pozostawiać skrótów prowadzących do SOURCE/starego EXE. Wydanie jest niekompletne bez prawdziwego sprawdzenia uruchamiania ze skrótu.
- Po restarcie komputera i zalogowaniu właściciela OBL MUSI sam wystartować z aktualnie zatwierdzonego release. Istniejący watchdog ma odtwarzać aplikację po zniknięciu procesu; jednorazowa pauza nie może trwale wyłączyć autostartu. Odrębny świadomy nakaz właściciela wyłączenia autostartu może ten stan zmienić.
- Launcher powinien rozpoznać i zastąpić nieaktualny działający EXE, zweryfikować SHA, obsłużyć rollback i raportować konkretne przyczyny błędów. Jeden GUI w interaktywnej sesji Windows, nie uruchamiać GUI z sesji usługowej.
- Podczas sprzątania nie usuwać profilu, credentiali, aktywnego release, jedynego sprawdzonego rollbacku ani danych projektu. Dawne wpisy autostartu i nieaktualne skróty naprawiać. Utrzymywać zawsze zdolność ręcznego uruchomienia z menu Start.
- Po przejściowych błędach narzędzi nie kończyć pracy statusem BLOCKED, pozostawić dalsze wybudzenia i CONTINUE.
## 2026-10-28 12:28 CET — CANONICAL LAUNCH / AUTOSTART FILES
- Canonical launcher: D:\projects\open-browser\scripts\obl-launch-current-satanamd.ps1 (reads verified releases from LOCALAPPDATA/OpenBrowser/ChatGPT-Lite/diagnostics/lite-release-satanamd-state.json; SHA check; single interactive GUI; removes stale owner pause as autostart always enabled; repairs wrong EXE; rollback; PID synchronization).
- Idempotent repair/install: D:\projects\open-browser\scripts\obl-repair-startup-satanamd.ps1 -RunNow (refreshes Start Menu, Desktop, Startup-folder shortcuts, keeps preexisting shortcut backups, clears pause, triggers existing interactive Keepalive). Must be run on SATANAMD when installing each new version; do not redirect shortcuts directly to SOURCE/bin/Release.
- Keepalive: D:\Apps\OpenBrowserLite\keepalive-satanamd.ps1 delegates to canonical launcher. Windows Task "Open Browser Lite SATANAMD Keepalive" has an enabled logon trigger and periodic trigger under interactive pboru. The Startup shortcut is an independent login recovery path.
- The obsolete Windows task "OpenBrowser ChatGPT Lite" points to an old SOURCE EXE but has ZERO triggers. Attempt to alter it failed with Windows access denied HRESULT 0x80070005; this task does not autostart. Do NOT report it as corrected. If rights later permit, retire/redirect it with ordinary owner authorization. No attempt to bypass denial.
- Do not delete rollback binaries or project SOURCE. Revert via release state and stable launcher if necessary. No Windows reboot was performed to test logon; only explicit interactive task execution, process and shortcut configuration were verified.
## P0 OWNER OVERRIDE 2026-10-28 — RESTART WHILE GENERATING IS ALLOWED (NADRZĘDNE)
- generating / draft / toolBusy / incomplete answer are diagnostic only and MUST NOT delay an approved LIVE deployment, app restart or auto-recovery. Restart immediately after a durable checkpoint, even if generation is active. The user accepts losing an in-progress answer.
- Keep SHA verification, one interactive GUI, rollback and journal truthfulness. Uncertain wake delivery can be retried: duplicates are acceptable, and missing delivery must not block the next wake.
- Supersedes all earlier idle-required/no-restart-while-generating language in current plans and scripts. Historical logs remain immutable evidence. Use short separate commands, LC MultiHost satamad first, RDC SatanAMD fallback only; no PBOX1. Always CONTINUE for transient tool failures.

## OWNER P0 2026-10-28 — DELIVERY OUTCOME + SELF-HEALING WEBVIEW
- A receipt_uncertain due to DOM timeout is only an intermediate historical observation. When engine sees matching receipt AND chat T2=chat_started, append receipt_resolved=confirmed_by_chat_started; the effective status is CONFIRMED_BY_CHAT. Preserve raw DOM diagnostics. Never claim a DOM ACK not observed; an uncertain wake may be retried with truthful per-attempt logging.
- If a target chat reports no_composer or response_error and there is no draft, recover by navigating/reloading the EXACT target chat URL, route-verified, once per 5min. The recovery operation does not send or click anything. Defer 2min and recheck identity/hydration before a later send. Do not do this on generating, foreign route or draft.
- This augments existing backlog, it does not replace handoff, rollback or logging rules. LIVE v1.73.45, 9 tests and verified rollback 1.73.44. Owner explicitly requested wake reenable, completed via --lite-project-wake on obl; later off requires new owner instruction.

## 2026-10-29 — nadrzędna zasada właściciela: checkpoint/logi bez ZIP
- Nie tworzyć nowych ZIP ani kompresji, dodatkowych indeksów i zbędnych SHA na potrzeby rotacji checkpointów i logów. Mechanizm retencji wykonuje sam OBL. HOT — bieżący krótki checkpoint, COLD — kompletna historia jako bezpośrednio czytelne Markdown poza katalogiem projektu.
- Starsze wzmianki o `docs/history/*.zip` w AGENTS są historyczne, zastąpione niniejszą regułą. Nie usuwać wcześniejszej historii bez oddzielnej kontroli; nie zmieniać harmonogramu/leada przy retencji.

## 2026-10-29 OWNER OVERRIDE — BEADS TASK STATE
Beads v1.3.2 in D:\projects\open-browser\.beads is the single authoritative project task-status database. Backlog Markdown describes original requirements and acceptance criteria; avoid rewriting its historical statuses as active task states. Use scripts/obl-beads.ps1 -Action scan then -Action ready, work on specific leaf issue, register START, STOP and DONE (-Evidence). Never close task at STOP; check old START older than 45min against live work and actual files before resuming. Do not repeat a verified closed subtask. Current wake auto-handoff snapshot instruction does not recur at all wakes >=20. Wake checkpoint continues recording actual wake work; no fake START/FINISH for manual chats. Preserve owner pause Enabled=false seq49.

## 2026-10-29 — OWNER OVERRIDE: project wake sequence and lead rotation (Beads obl-p0-20)
1. WakeSequence is a monotonically increasing project-owned long integer. Rotating a chat NEVER resets it. Full ID remains OBL-000050.1.HHMMSS etc.; six digits is a MINIMUM, not a cap.
2. Wake N ending in 9 (9,19,29,39,49,59,...) tells the CURRENT lead to save and verify D:\projects\open-browser\docs\OPENBROWSER_LITE_AUTO_HANDOFF_CURRENT.md. Save result/error to checkpoint, continue normal Beads work and STOP, do not declare project completed.
3. The NEXT wake N+1 ending in 0 belongs to a freshly created, verified NEW ChatGPT conversation, registered as sole project lead. Engine creates this chat using logged-in WebView2, a real server /c/ URL, exact assistant ACK and a stored one-shot receipt. The engine creates it whether handoff file exists or not.
4. If HANDOFF save fails, next lead reads Beads + active checkpoint + AGENTS; HANDOFF is optional context, NEVER a prerequisite for next-chat creation.
5. If the NEW chat cannot actually be created/verified, the engine MUST NOT send wake N+1 into the old chat, MUST NOT fabricate a /c/ URL, and MUST NOT consume/increment N+1. It logs the concrete rotation error and awaits recovery. Old separate seeds are obsolete; the normal numbered wake to a verified successor may be retried if delivery was uncertain.
6. Old project chat becomes archive only after the genuine verified server successor becomes sole lead. Retain the one schedule and its due/slot; do not run two lead chats.
7. Beads task START/STOP/DONE and wake CHECKPOINT remain independent. A wake-ending 9 still finishes its normal tasks. On successor start, inspect unfinished Beads work and resume rather than restart.
8. Owner pause remains authoritative; this policy never enables a paused schedule. An actual 49->50 E2E production test needs a genuinely enabled project wake.

2026-10-29 ROTATION MULTIPROJECT ADDENDUM: For OBL, ending-9 handoff path remains docs/OPENBROWSER_LITE_AUTO_HANDOFF_CURRENT.md. For other project leads, HANDOFF path is D:\OpenBrowserLiteHandoffs\<projectKey>\HANDOFF_CURRENT.md. Ending-0 successor seed reads that project's own state, not OBL Beads. This is the same universal per-project modulo-10 wake policy, with no cross-project state mixing.

## 2026-10-29 OWNER DECISION — ABSOLUTE WAKE PRIORITY / PAUSE FOR DISCUSSION
Beads at D:\projects\open-browser\.beads: every issue has PRIORYTET_BEADS, STATUS_W_CHWILI_AUDYTU, NASTEPNY_KROK, WARUNEK_REALIZACJI. P0=only reliable wake, real ACK, recovery, sole lead and chat rotation; P1=supporting prerequisites; P2-P4=after wake. Do not start implementation or tests involving actual wakes before discussing the complete wake plan with owner. Preserve paused schedule (Enabled=false, seq49) until explicit authorization. Audit issue obl-p0-21 records verified all 67 tasks. Never treat an item labelled legacy OBL-P0 in its title as necessarily current Beads P0; numeric Beads priority controls.

## 2026-10-29 — OWNER OVERRIDE: JEDYNY ZATWIERDZONY PLAN WYBUDZEŃ (NADRZĘDNY)
Autorytatywny plik: docs/OBL_PLAN_WDROZENIA_WAKE_ZATWIERDZONY_2026-10-29.md. Obowiązuje format OBL-000043-A01-HHMMSS dla normalnych i OBL-000043.1-A01-HHMMSS dla awaryjnych komunikatów. Dotychczasowe identyfikatory .1.HHMMSS (numer próby), osobny seed przed pierwszym wake następcy oraz wymóg dokładnej odpowiedzi asystenta tylko na seed są HISTORYCZNE i UCHYLONE. Pierwsza wiadomość nowego leada jest już normalnym wybudzeniem 44, 50 itd., z informacją o poprzedniku, ewentualnym handoffie i instrukcją kontynuacji. Po rotacji nowy 15-minutowy zegar od pierwszego wybudzenia, a nie zachowanie starej fazy. Przy terminalnym czerwonym limicie: jeden numerowany komunikat N.1 o handoff do bieżącego pliku; wybudzenie N+1 natychmiast po nowym poprawnym pliku lub najpóźniej po 30 min. Pełnoekranowy problem i zerwane połączenie: natychmiast wake N+1 w nowym czacie bez handoffu. Przy restarcie odczytaj trwale zapisany start i deadline; ponawianie niepewnego wake jest dozwolone, a brak ACK nie blokuje kolejnego wybudzenia. Stare pliki handoff czytać tylko jako historię, nie przyjmować jako aktualne. Testy wykonywać na SATANAMD, rollback i jeden lead obowiązkowe.

## OWNER OVERRIDE 2026-10-30: PROCESY TESTOWE I TRWALA POWLOKA MULTIHOST
1. Zakaz masowego konczenia procesow wedlug nazwy lub wzorca; zakaz Stop-Process/taskkill po nazwie/wzorcu.
2. Proces testowy musi miec limit czasu i konczyc sie sam. W chwili startu zapis w aktywnym checkpoint: PID, UTC czas startu, pelna linia polecen.
3. Przed ewentualnym zakonczeniem procesu testowego ponownie porownaj jego dokladny PID, nazwe i czas startu z checkpointem; koncz tylko ten jeden PID. Nastepny proces sprawdzaj osobno.
4. Wiszace polecenie MultiHost: cancel_current_command. Brak znacznika konca: session_action action=recover. W obu przypadkach nie ubijaj procesow; gdy narzedzie nie jest udostepnione, odnotuj blokade.
5. Nie ustawiaj globalnie ErrorActionPreference=Stop w trwalej powloce MultiHost. Ogranicz do bloku albo przywroc Continue na koniec, rowniez w sciezce bledu.
6. Niedokonczone sprzatanie oznacza status w toku lub zablokowane, nigdy wykonane. Nie zamykaj zadania bez zweryfikowanego sprzatania.
Pierwszenstwo przed historycznymi zaleceniami masowego konczenia procesow. Zasady obowiazuja wszystkie przyszle wybudzenia i testy.

## OWNER OVERRIDE 2026-10-30: ODPORNOSC HARMONOGRAMU NA BRAK NARZEDZIA
- Tymczasowy brak dostepu do konektora, odrzucone wywolanie, brak zapisu lub awaria narzedzia nie sa powodem do zatrzymania calego harmonogramu Open Browser Lite ani oczekiwania, az wlasciciel wznowi prace.
- Przy awarii pojedynczego narzedzia nie ustawiaj Enabled=false, nie wywoluj trwalego WAIT_USER ani DONE. Dla autonomicznego leada tymczasowy BLOCKED nie moze stac sie trwala pauza. Raportuj CONTINUE lub brak znacznika, z opisem konkretnej operacji.
- Utrzymuj aktualny ustalony interwal automatycznych wybudzen bez samowolnej zmiany. Podczas kolejnych wake sprawdzaj, czy narzedzia ponownie dzialaja; odtwarzaj stan z checkpointu i Beads i kontynuuj.
- Odrzuconej operacji nie obchodz innym kanalem w tym samym wake. Ponowienie wake o statusie UNCERTAIN jest dozwolone i nie moze blokowac wybudzenia. Kolejny normalny wake jest nowa okazja do kontroli dostepnosci narzedzi.
- Pozostaw niedokonczone zadania in_progress lub zapisuj blokade operacji. Zadanie mozna oznaczyc done dopiero po faktycznej weryfikacji. Swiadoma pauza wlasciciela i prawdziwe zakonczenie calego projektu pozostaja odrebnymi przypadkami.
- Test odbiorczy P0: co najmniej trzy kolejne planowe wybudzenia podczas symulowanej niedostepnosci narzedzia i po odzyskaniu dostepu, bez utraty Enabled, bez wymogu recznej interwencji, bez duplikacji, z prawdziwym logiem zdarzen.

## OWNER OVERRIDE 2026-10-30: WYBUDZENIE MA ABSOLUTNY PRIORYTET, DUPLIKAT JEST AKCEPTOWALNY
- Najwyzszy priorytet Open Browser Lite: faktycznie WYBUDZIC aktualny czat-lead i utrzymac ciaglosc jego pracy. Ewentualny duplikat komunikatu wybudzenia jest akceptowalny i ma mniejsza wage niz niewyslane wybudzenie.
- NIE WOLNO blokowac, wylaczac ani bezterminowo odraczac wybudzenia z obawy przed ponowna wysylka, identyczna trescia, UNCERTAIN, brakiem ACK/visible_ack/chat_started, poprzednia proba lub ryzykiem duplikatu. Jezeli nie ma pewnego potwierdzenia dostarczenia, wolno ponowic komunikat wybudzenia. Brak pewnosci NIE jest zakazem ponowienia.
- Generating, toolBusy, assistant_not_complete, draft_present, brak gotowej odpowiedzi i przejsciowe bledy narzedzi nie moga stac sie nieskonczona blokada. Stosuj ograniczone czasowo odroczenie i mechanizmy STOP/recovery/rotacji, po czym podejmij probe wyslania wybudzenia. Priorytetem jest skuteczna kontynuacja, nie unikniecie powtorki.
- Zapisuj kazda faktyczna probe, numer/ID, receipt, wynik i diagnostyke. Powtorzenie ma byc rozpoznawalne w logu; nie fabrykuj ACK ani wyniku. Zachowaj aktualny prawdziwy /c/ URL, jednego leada i skierowanie tylko do wlasciwego czatu. Nie powielaj niezaleznych dzialan biznesowych; zgoda na powtorki dotyczy komunikatow wake.
- Kryterium 'zero duplikatow' i wszelkie historyczne zakazy 'nie ponawiaj UNCERTAIN' sa UCHYLONE w zakresie wysylania wybudzen. Zmien testy i logike silnika zgodnie z ta decyzja; ewentualny duplikat nie jest bledem odbioru, natomiast brak wybudzenia jest. Nie wymaga to zmiany harmonogramu ani wylaczania Enabled.
- Ta decyzja wlasciciela ma pierwszenstwo przed AGENTS.md, starszymi zasadami, kanonicznym backlogiem i planem z 2026-10-29 w kazdym sprzecznym fragmencie dotyczacym powtarzania wake. Dotychczasowe wpisy zdarzen sa historia i pozostaja niezmienione.

## OWNER OVERRIDE 2026-10-30: RDC SATANAMD PRIMARY AFTER LC FAILURE
- Owner explicitly instructed: stop using LC MultiHost for this project and continue via connected Remote Desktop Commander (RDC).
- RDC device is SatanAMD (id 3561b9cc-556d-4215-a8cc-c6b36d1e311f); verify `hostname`=SATANAMD before operations, always specify deviceId, never operate on PBOX1.
- Do NOT wait for or repeatedly poll the stuck LC command `cmd_mvvgvj9w_t8`; no blind retry of an uncertain write. RDC is the authorized working path for new independent OBL operations. Do not kill processes by name or pattern.
- This owner instruction supersedes earlier LC-primary guidance. Do not change the wake schedule, lead, or unrelated project settings solely to switch tools. Keep true Beads, checkpoint and engine journal evidence.

## OWNER OVERRIDE 2026-10-31: MINIMALNE KOMUNIKATY OBL
Wiadomość do czatu ma zawierać tylko dwie linie: [OB-WAKE-NNNNNN] oraz Kontynuuj pracę w swoim projekcie zgodnie z zasadami. W terminie handoffu lub przy żądaniu handoffu druga linia to Zapisz aktualny handoff projektu zgodnie z zasadami. Pierwsza wiadomość nowego leada jest zwykłym minimalnym wybudzeniem. W treści nie ma danych technicznych, timestampu silnika, receipt, Beads, CLI ani dodatkowych notatek. Wewnętrzna telemetria dostarczenia i rotacji pozostaje poza komunikatem. Czat sam odczytuje bieżącą godzinę rozpoczęcia pracy; nie przepisuje czasu wybudzenia z silnika. Celem systemu jest nieprzerwana praca, dlatego zbędne odroczenia i kontrole duplikatów mają być usuwane. Szczegółowy plan: docs/OBL_SIMPLIFICATION_PLAN_2026-10-31.md.

## OWNER OVERRIDE 2026-10-31: PRACA NAD KOLEJNYM ETAPEM, NIE PETLA TESTOW
- Zwykle dwuwierszowe wybudzenie OBL ma nowa druga linie: "Kontynuuj projekt: wykonaj kolejny niezakończony etap z planu, przede wszystkim zmianę w kodzie lub wdrożenie. Nie powtarzaj zaliczonych testów bez konkretnej przyczyny."
- Handoff ...9/request_pending ma druga linie: "Zapisz aktualny handoff projektu zgodnie z zasadami. Następnie wykonaj kolejny niezakończony etap z planu."
- Wybudzenie w nowym czacie, takze po awaryjnej rotacji, uzywa nowej zwyklej linii. Nadal tylko dwie linie komunikatu: numer oraz jedno polecenie, bez danych silnika i Beads.
- Po kazdym wake sprawdzaj Beads i aktualny checkpoint, wybierz nastepna realna zmiane w P0/P1, wprowadz ja do SOURCE i gdy dozwolone STAGED/LIVE. Testy sa bramka po zmianie, a nie autonomicznym celem. Nie powtarzaj wielokrotnie udanych testow bez nowej zmiany, awarii lub potrzeby odbioru. Gdy testy sa jedynym pozostajacym fragmentem konkretnego zadania, nastepne wake powinno wybrac inne otwarte zadanie rozwojowe do czasu uplywu okresu obserwacji.
- Ten override zastepuje poprzednia tresc drugiej linii we wszystkich starszych planach. Reszta wymagania dwoch linii pozostaje.

## OWNER OVERRIDE 2026-11-01: ZINTEGROWANY CYKL PRACY I WYDANIA
Nadrzedna zasada wykonywania: w jednym wybudzeniu realizowac mozliwie duzo powiazanych prac: zmiana SOURCE, testy konkretnej poprawki, build, publish, immutable release, queue/promotion i dokumentacja. Nie zatrzymywac sie na pojedynczym mikrokroku bez technicznej przyczyny. Jezeli LIVE jest zajety, zlec bezpieczna promocje bez wymuszania restartu, kontynuuj inne zadania. Nie powtarzac zaliczonych testow bez zmiany lub nowego bledu. Zachowac pojedyncze polecenie terminalowe na wywolanie, jeden lead, rollback i prawdziwy stan LIVE.
## OWNER OVERRIDE 2026-11-01: SKONSOLIDOWANA IMPLEMENTACJA I ARCHIWIZACJA 429
- W ramach jednego etapu roboczego realizuj wiele powiazanych zmian kodu, testy tylko nowych funkcji, kompilacje, publish oraz wdrozenie i weryfikacje LIVE. Nie przenos gotowych testow/wdrozen do osobnych wybudzen bez konkretnej blokady; przy aktywnym generowaniu zachowaj bezpieczna kolejke Release Managera zamiast wymuszac restart.
- Przy natywnej archiwizacji gdy GET konkretnego czatu uprzednio potwierdzil is_archived=true, a normalna lista zwraca HTTP 429, uznaj to za operacyjny sukces ACCEPTED_BY_OWNER. Zapisz oddzielny znacznik od pelnego potwierdzenia braku UUID na zwyklej liscie; nie wymagaj obecnosci w archiwum. Pomin ponowne zapytania dla zaakceptowanych czatow. Nie uznawaj 429 od niezwiazanej sesji za status konkretnego czatu.
