## OWNER OVERRIDE 2026-11-03: JEDNA ZAMKNIETA LISTA, KONIEC ZADANIA
NAJWYZSZY PRIORYTET. Zatwierdzone przez wlasciciela OBL nadajnik wybudzen P0 obl-5w3 jest DONE (LIVE 1.73.266). NIEAKTUALNE sa historyczne nakazy ciaglego szukania nowych usterek nadajnika i robienia kazdorazowo poprawki SOURCE/nowego LIVE. NIE WYMYSLAC nowych problemow.
Kazde nowe zadanie: jednorazowo przejrzyj faktyczne dowody, zbuduj ZAMKNIETA numerowana liste konkretnych problemow/zakresu na POCZATKU, napraw WYŁACZNIE wymienione pozycje, po czym DONE i STOP. Lista NIGDY nie rozrasta sie w kolejnych wake, checkpointach i implementacjach. Gdy brak potwierdzonych problemow, lista jest PUSTA i konczysz zadanie bez zmian. Nowa faktyczna usterka to ODDZIELNE zadanie z wlasna lista, nie przedluzenie starego. Odrzuc hipotetyczne naprawy, bezcelowe testy i prace na zapas. Nie restartuj cyklu tylko po to, by wymyslic nowa pozycje.
Aktywne harmonogramy, rotacja i wysylanie wake pozostaja wlaczone. Dla uzasadnionych zmian kodu normalny build, SHA, QUEUED/LIVE i rollback. Tylko SATANAMD, bez recznego Git i innych projektow. Kontrakt aktywny: docs/OBL_BACKLOG.md (decyzja 2026-11-03). Wszystkie starsze sprzeczne instrukcje w tym pliku sa NIEAKTUALNE.

## OWNER OVERRIDE 2026-11-01: STOP HISTORYCZNYCH TESTOW, NOWE UI I NIEZALEZNE PROJEKTY
Najnowsza decyzja wlasciciela. Dzialajace wybudzenia, rotacja i wznowienie sa AKCEPTOWANE. Zakoncz powtarzanie testow historycznych, soakow, sztucznych awarii i spekulacyjnych napraw. Powrot do diagnozy tylko po realnym, udokumentowanym bledzie LIVE lub wyraznym nowym poleceniu; nowych zmian nie wolno wdrazac bez minimalnej kompilacji/SHA/rollbacku.
Kazdy projekt ma miec niezalezny, samodzielnie ustawiany harmonogram. Nie projektuj centralnej kolejki zadan ani nie traktuj jednego workera jako wymogu uzytkownika. Nie zmieniaj innych projektow ubocznie.
ZBUDUJ OD NOWA czytelny interfejs, nie tylko retusz: pulpit projektow, czytelny stan i interwal, centralny czat, latwe notatki; funkcje administracyjne/diagnostyczne na zadanie. Projekt UI i etapy: docs/OBL_UX_MULTIPROJECT_DECISION_2026-11-01.md. Zadania: Beads obl-p1-01 i obl-p0-11. Stare sprzeczne klauzule testowe i wieloprojektowej kolejki sa uchylone.

# Open Browser Lite — operacyjny kontrakt agentów
Obowiązuje dokument docs/OPENBROWSER_LITE_OPERATING_RULES_2026-10-25.md.
Priorytet: **najpierw pracująca, sprawdzona wersja LIVE, potem następna iteracja**.
UWAGA 2026-11-01: NAJWYZSZY PRIORYTET MA IMPLEMENTACJA BRAKUJACYCH FUNKCJONALNOSCI. NIE WOLNO WPADAC PONOWNIE W PETLE TESTOW. Nadrzedna aktualna decyzja wlasciciela: OWNER OVERRIDE 2026-11-01 ponizej.
Cykl historyczny (UCHYLONY w zakresie wymuszania kolejnych testow): POPRAWKA -> TESTY -> STAGED -> WDROŻENIE SATANAMD -> WERYFIKACJA -> DOGFOOD nowego LIVE.
OBL ma działać WYŁĄCZNIE na SATANAMD. PBOX1 całkowicie poza zakresem; w LC MultiHost ZAWSZE target=satamad i sprawdzenie COMPUTERNAME.
P0: skuteczne wybudzenie/wznowienie, jeden aktywny czat wykonawczy, duplikat komunikatu wake dozwolony, właściwy dziennik i ACK tylko na podstawie dowodów silnika.
Nie używać ChatGPT Tasks; bez ręcznego git commit/push/reset. Poprawki muszą trafiać do produkcyjnego OBL z zachowaniem odwracalnego rollbacku, aby kolejna praca korzystała z nowego narzędzia.
W razie NO-GO zapisać blokadę i konkretny krok odblokowania; testy offline nie są wdrożeniem.


## Szybki start / oszczędność kontekstu
Przed wznowieniem pracy czytaj docs/OBL_CONTEXT.md, docs/OPENBROWSER_LITE_CURRENT_STATUS.md, tylko AKTYWNY docs/OPENBROWSER_LITE_CHECKPOINT.md oraz najnowsze OPERATING_RULES. Stare WORK_STATUS i dawne HANDOFF/plan/archiwum są historyczne — nie wczytuj ich automatycznie. Checkpoint max 6-12 wierszy na etap. Zachowuj pełną historię w skompresowanym archiwum.

## Retencja danych projektu
Po każdym cyklu kontroluj porządek i oszczędzanie kontekstu. Pozostaw tylko 12 ostatnich lokalnych odpowiedzi aktualnego czatu (Task co 15 min), kasuj starsze powtarzalne wyniki build po 2h (Task codziennie), utrzymuj krótki OBL_CONTEXT.md i CHECKPOINT.md. Historia tekstowa tylko w ZIP docs/history, nie wczytuj starych HANDOFF/WORK_STATUS automatycznie. Zasady i wyjątki w docs/OPENBROWSER_LITE_OPERATING_RULES_2026-10-25.md. Nie kasuj źródeł, zmian Git, LIVE/rollback, profilu/DPAPI, dziennika wybudzeń. Po sprzątaniu sprawdź LIVE, zapisz liczby i błędy.KANONICZNY BACKLOG: docs/OBL_BACKLOG.md. Przy wyborze nastepnego zadania P0 odczytaj tylko odpowiedni wiersz tego backlogu; stare roadmapy sa historyczne. Audyt: docs/OBL_REQUIREMENTS_AUDIT_2026-10-26.md (czytaj tylko na zadanie).

## OWNER OVERRIDE 2026-11-01: IMPLEMENTUJ BRAKUJACE FUNKCJE, NIE TESTUJ W KOLKO (NAJWYZSZY PRIORYTET)
To jest AKTUALNA, NADRZEDNA decyzja wlasciciela. Obowiazuje bezterminowo we wszystkich nastepnych wake, po rotacji czatu i po restarcie. ZASTĘPUJE starszy cykl nakazujacy wielokrotne testy stabilizacji, sztuczne awarie, formalne kryteria dlugich soakow i ciagle powracanie do P0-07. Dzialajace juz mechanizmy OBL przyjmujemy za wystarczajaco zweryfikowane po okolo 240 turach pracy, chyba ze NOWY REALNY incydent wskaze konkretna usterke. Nie otwieraj ponownie zrealizowanych funkcji tylko dlatego, ze historyczne kryterium testowe ma status OPEN/PARTIAL.
1. START kazdego wake: odczytaj ostatni checkpoint, ten OWNER OVERRIDE, krotki status Beads (scan/ready), a nastepnie odpowiedni fragment kanonicznego backlogu docs/OBL_BACKLOG.md. Ustal trzy stany: funkcjonalnosc GOTOWA/WDROZONA, FUNKCJONALNOSC ROZPOCZETA, FUNKCJONALNOSC BRAKUJACA. Jesli nie masz pewnosci co do stanu, krotko zajrzyj do kodu/Beads. Jesli Beads ready pokazuje glownie stare testy odbiorcze i stabilizacyjne, POMIN je: wybierz brakujaca funkcje z kanonicznego backlogu, nawet jesli ma nizszy historyczny numer priorytetu. Nie wykonuj ponownie kompleksowego audytu.
2. WYBOR: najwyzszy priorytet ma NOWA, NIEWDROZONA funkcjonalnosc z zatwierdzonego backlogu, a nie diagnostyka juz dzialajacych funkcji. Kontynuuj rozpoczeta funkcjonalnosc, gdy daje nowy rezultat. Zamykanie formalnie otwartych testow, trwalych PARTIAL/UNCERTAIN, 30-minutowych crash-testow, spekulacyjnych poprawek i kolejnych mechanizmow watchdog jest WSTRZYMANE, chyba ze zaistnial realny, udokumentowany blad produkcyjny lub nowe jawne polecenie wlasciciela.
3. WYKONANIE: w kazdym kolejnym wake przede wszystkim pisz kod i DODAWAJ BRAKUJACA FUNKCJONALNOSC. Potem zrob minimalne sprawdzenie bezposrednio zmienionego fragmentu, zbuduj STAGED i szybko WDROZ na SATANAMD z wersja/SHA/rollbackiem, o ile nie istnieje konkretny blokujacy blad. Nie tworz wydan tylko dla kolejnego testu, instrumentacji lub hipotetycznego scenariusza awarii.
4. TESTY: NIE POWTARZAJ zaliczonych testow. NIE tworz nowych obszernych suite, symulacji crash, dlugich soakow ani testow wymyslonych problemow. Dopuszczalne sa krotkie, konieczne sprawdzenia NOWEJ funkcji (np. kompilacja plus pojedynczy smoke), lub celowana diagnostyka RZECZYWISTEGO bledu z konkretnym logiem. Testy nie moga zastapic implementacji.
5. OCHRONA PRZED PETLA: jesli kolejne wake konczy sie bez dodania lub wdrozenia funkcjonalnosci, przy nastepnym wyborze zadania NIE wracaj do audytu, regresji ani stabilizacji: wybierz implementowalny brak z backlogu. Blokade jednej funkcji zapisz konkretnie i realizuj nastepna; nie zatrzymuj autonomicznej pracy. Beads STOP nie znaczy DONE, ale brak testu soak nie oznacza automatycznie, ze funkcja jest niezaimplementowana.
6. RAPORT i HANDOFF: zapisuj przede wszystkim IDENTYFIKATOR zadania, dodana funkcje, zmienione pliki, status SOURCE/STAGED/LIVE, ew. konkretny realny blad, nastepna BRAKUJACA funkcje. Nie raportuj liczby zaliczonych starych testow jako postepu funkcjonalnego. Kazdy kolejny czat i wake ma obowiazek odczytac i zastosowac te zasade.
7. Bez zmian: tylko SATANAMD, poprawny lead/wake, czytelny checkpoint/Beads, jedno rzeczywiste LIVE, rollback, bez PBOX1, ChatGPT Tasks i recznego Git. Nie naruszaj stabilnej produkcji bez potrzeby. Ten override dotyczy PRIORYTETOW pracy oraz ZAKRESU testowania, nie znosi podstawowej kontroli udanego wdrozenia.
Kiedy starsze instrukcje mowia 'najpierw testy stabilizacyjne/odpornosc/30 min/100 wake', pierwszenstwo ma ten OWNER OVERRIDE. Pytanie przy kazdym wake: 'JAKA NOWA FUNKCJA Z BACKLOGU ZOSTANIE DZIS DODANA?', a nie 'JAKA AWARIE JESZCZE SYMULOWAC?'.

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
## P0 OWNER OVERRIDE 2026-10-28 — GENERATING DOES NOT BLOCK RESTART (HIGHEST PRIORITY)
- OWNER explicitly authorizes immediate OBL restart, production deployment and automated recovery even if WebView2 reports generating=true, draft=true, toolBusy=true, or a response is incomplete. These are telemetry ONLY, never hard deployment gates.
- Save durable checkpoint BEFORE process termination, then use verified candidate SHA / interactive Windows deployment task / one GUI PID / health verification / rollback. No waiting for idle; uncertain wake may be sent again. Never block a wake only to prevent duplicates.
- A transiently lost in-progress answer is an accepted trade-off. Do not infer that a restart completed the model answer. Keep project wake scheduler enabled and end status CONTINUE, never terminal BLOCKED for a temporary failure.
- Execute SHORT, separate commands. LC MultiHost target=satamad is PRIMARY; RDC SATANAMD only if LC MultiHost isn't exposed or fails. Verify COMPUTERNAME. NEVER operate on PBOX1.
- This owner's latest rule supersedes every earlier 'wait for idle', 'do not interrupt generation', or 'no restart while generating' instruction, irrespective of historical location. Preserve old checkpoint entries as dated evidence, not as active operating instructions.

## OWNER OVERRIDE 2026-10-29 — CHECKPOINT/LOG RETENTION (LATEST, SUPERSEDES OLD ZIP TEXT)
- OBL itself automatically shortens the HOT checkpoint; complete older content remains in human-readable plain Markdown in COLD storage outside project root. Do not create ZIP archives, compression, separate indices or unnecessary hashes for checkpoint/log retention.
- Do not follow the earlier sections of this AGENTS.md which mention ZIP under docs/history: those are HISTORICAL and superseded by the owner's explicit 2026-10-28 instruction.
- Current OBL LIVE v1.73.57 has demonstrated automatic rotation to D:\OpenBrowserLiteArchive\checkpoints\obl\OBL_checkpoint_2026-10-29_040712_2321618.md, directly readable; active checkpoint remains below 32KiB. Keep plain-text history and avoid content loss. The prior legacy ZIP is a one-time historical artifact and not a format to perpetuate.

## OWNER OVERRIDE 2026-10-29 — BEADS PROJECT STATE (LATEST)
- Project task state is now in local Beads at D:\projects\open-browser\.beads. docs/OBL_BACKLOG.md provides requirements/acceptance only; its old states are historical.
- Each OBL wake: read active CHECKPOINT and AGENTS/rules, then scripts/obl-beads.ps1 -Action scan; continue in-progress unfinished work first, else -Action ready to choose an actionable LEAF issue. Do not start epic/closed/deferred task.
- START and STOP in Beads include full wake ID, and STOP means merely ceasing work; only verified DONE with -Evidence closes. A recent START (under 45min) is not abandoned. Stale START means inspect live executor, checkpoint and artifacts before taking over.
- The wake checkpoint and wake-lifecycle.jsonl remain independent evidence of wake start, activities and stop/finish. Do not forge a wake sequence for manual work.
- LIVE 1.73.59 on SATANAMD; rollback 1.73.58, wake schedule remains paused at seq49 until user explicitly re-enables it.

## OWNER OVERRIDE 2026-10-29 — ROTATION MODULO TEN
OBL project wake number increases forever without reset on lead switch. On every ending-9 wake, lead saves a best-effort HANDOFF and records any write error. Before the next ending-0 wake the OBL engine creates and verifies a new /c/ chat lead WITHOUT requiring HANDOFF. The old chat must NEVER receive the ending-0 wake. If new chat cannot be verified, keep this number unconsumed and report a concrete rotation failure; never fake a successor or resend uncertain seed. Use Beads issue obl-p0-20 for work/tracking. Do not override owner's paused schedule.

2026-10-29 FINAL RELEASE NOTE: OBL LIVE 1.73.61 on SATANAMD, PID 23624 verified responsive, rollback 1.73.60. Wake sequence=49 Enabled=false by prior owner pause. New generic modulo-ten lead rotation is built and 9/9 headless selftests pass; real new-chat /c/ ACK and numbered wake50 are not yet E2E proven. Beads obl-p0-20 is active, 20-01 closed, 20-02/20-03 open. No fabricated wake or promotion.

## OWNER OVERRIDE 2026-10-29 — BEADS PRIORITY GATE
Autonomous reliable wake/resume/handoff is the SINGLE highest OBL priority: Beads P0 (wake-core). Technical prerequisites are P1; unrelated work is P2/P3/P4 and must not displace wake. Every Beads issue contains an AUDYT_BEADS_2026-10-29 note with next action, current status and priority. DO NOT IMPLEMENT any wake task or re-enable owner-paused wake before owner + assistant explicitly discuss and agree the wake plan. A paused wake is not a failure to auto-repair. Beads is source of task state, not Markdown historical status.

## 2026-10-29 — NADRZĘDNY ZATWIERDZONY PLAN WYBUDZEŃ
Obowiązuje D:\projects\open-browser\docs\OBL_PLAN_WDROZENIA_WAKE_ZATWIERDZONY_2026-10-29.md. Starsze opisy odrębnego seed/inicjalizacji nowego czatu, zachowania starej fazy 15-minutowych slotów po rotacji, nienumerowanych komunikatów awaryjnych albo automatycznej kwarantanny bez próby handoffu są UCHYLONE. Pierwsza wiadomość nowego czatu to WYŁĄCZNIE kolejne normalne wybudzenie N+1 z pełną instrukcją startu/odzyskania. Nowy czat kotwiczy swój 15-minutowy harmonogram od pierwszego wybudzenia. Testy/wdrożenia na SATANAMD są autoryzowane przez właściciela 2026-10-29; nie zmieniaj ustawień innych projektów. Historyczne opisy wdrożeń zachować, lecz nie stosować jako aktualne wymagania.

## OWNER OVERRIDE 2026-10-30: PROCESY TESTOWE I TRWALA POWLOKA MULTIHOST
1. Nie koncz procesow masowo, wedlug nazwy ani wzorca. Zakazane jest Stop-Process lub taskkill po nazwie/wzorcu.
2. Kazdy proces testowy uruchamiaj z limitem czasu i samoczynnym zakonczeniem. W chwili startu zapisz w biezacym checkpoint: dokladny PID, czas startu (UTC) oraz pelna linie polecen.
3. Proces testowy koncz wylacznie po pojedynczym, dokladnym PID, po ponownym sprawdzeniu, ze nazwa i czas startu zgadzaja sie z zapisem checkpointu. Sprawdzaj i koncz jeden po drugim.
4. Wiszace polecenie MultiHost koncz narzedziem cancel_current_command. Jesli zgubiono znacznik konca, uzyj session_action z action=recover. Nie ubijaj procesow. Gdy narzedzia nie sa dostepne, zanotuj blokade i nie zastepuj ich zabijaniem procesow.
5. W trwalej powloce MultiHost nie ustawiaj globalnie ErrorActionPreference=Stop. Stosuj taki tryb w lokalnym bloku albo przywroc Continue na koncu polecenia, takze po bledzie.
6. Zadanie z niedokonczonym sprzataniem ma status w toku lub zablokowane, nigdy wykonane. Nie wywoluj Beads done bez weryfikacji sprzatania.
Te reguly sa nadrzedne wobec starszych sugestii masowego zabijania procesow, rowniez w skryptach testowych i przy diagnostyce. Nie zmieniaja polityki autonomicznych wake.

## OWNER OVERRIDE 2026-10-30: BRAK NARZEDZIA NIE ZATRZYMUJE WYBUDZEN
1. Chwilowa niedostepnosc LC MultiHost, odmowa narzedzia albo przejsciowy blad zapisu nie moze wylaczyc wybudzen, ustawic trwalej pauzy projektu ani wymuszac interwencji wlasciciela.
2. Zachowaj Enabled=true i dotychczasowy skonfigurowany interwal automatycznego wybudzania. Nie zmieniaj 15 minut na 30 minut ani odwrotnie bez wyraznej decyzji. Nie stosuj bezterminowego WAIT_USER, BLOCKED lub DONE z powodu przejsciowego bledu narzedzia.
3. Blokada pojedynczej operacji dotyczy tylko tej operacji. Kontynuuj inne dozwolone zadania. Gdy nic wiecej nie da sie wykonac, w odpowiedzi wybierz CONTINUE albo pomin znacznik. Silnik ma probowac ponownie przy nastepnym naturalnym wybudzeniu.
4. Przy kazdym kolejnym wybudzeniu sprawdzaj na nowo dostepnosc narzedzi, weryfikuj rzeczywisty stan i kontynuuj niedokonczona prace przez Beads i checkpoint. Nie wymagaj recznego wznowienia przez wlasciciela.
5. Odmowy wykonania zabronionej operacji nie obchodz innym kanalem. WYJATEK dla polityki dostarczania wake: niepewna wysylke komunikatu wybudzajacego WOLNO ponowic, a ryzyko duplikatu NIE moze blokowac wake. Rejestruj faktyczne proby, komunikaty i kody bledow.
6. Stan chwilowej blokady to in_progress lub operacja blocked; nie jest to DONE. Wyjatki dotyczace swiadomej pauzy wlasciciela lub zakonczonego projektu pozostaja bez zmian.
ZADANIE P0: przetestowac end-to-end niedostepnosc narzedzia i zachowanie statusu BLOCKED; potwierdzic kolejne automatyczne wybudzenia w ustalonym interwale, probe odzyskania i brak dubli. Beads przechowuje stan zadania.

## OWNER OVERRIDE 2026-10-30: WAKE PONAD UNIKANIEM DUPLIKATOW
NAJWYZSZY PRIORYTET: obudzic wlasciwego aktualnego leada OBL. Niepewna wczesniejsza wysylka (UNCERTAIN), brak ACK, powtorka tresci albo ryzyko duplikatu NIGDY nie moga zablokowac wyslania kolejnego wake. WOLNO wyslac wake ponownie. Generowanie, toolBusy, assistant_not_complete i brak odpowiedzi nie moga blokowac bezterminowo: ograniczone odroczenie, STOP/recovery/rotacja i nastepna proba. Loguj kazda probe i jej faktyczny wynik; nie falszuj ACK. Zachowaj prawidlowy aktywny chat i jednego leada. Tylko komunikaty wybudzajace maja takie przyzwolenie na powtorzenia. Kryterium 'zero duplikatow' oraz starsze 'nie ponawiaj UNCERTAIN' sa zastapione. Szczegoly: docs/OPENBROWSER_LITE_OPERATING_RULES_2026-10-25.md, najnowszy OWNER OVERRIDE.
## OWNER OVERRIDE 2026-10-30: JEDNA KOMENDA NA WYWOLANIE LC MULTIHOST
Kazde wywolanie execute_command przez Invoke-LupusMultiHost.ps1 -Target SATANAMD przekazuje DOKLADNIE JEDNA komende PowerShell w -Command. Nie lacz polecen srednikami, pipeline z dodatkowymi operacjami, blokami z wieloma instrukcjami ani poleceniami odczyt+zapis+test w jednym wywolaniu. Kazdy krok wywoluj osobno i sprawdzaj status, exit_code, stdout, stderr przed nastepnym. Uzytkownik wskazal laczenie wielu komend jako przyczyne odrzucen przez zabezpieczenia. Test pojedynczego polecenia COMPUTERNAME zakonczyl sie exit_code=0.

## OWNER OVERRIDE 2026-10-30: RDC SATANAMD PRIMARY AFTER LC FAILURE
- Owner explicitly instructed: stop using LC MultiHost for this project and continue via connected Remote Desktop Commander (RDC).
- RDC device is SatanAMD (id 3561b9cc-556d-4215-a8cc-c6b36d1e311f); verify `hostname`=SATANAMD before operations, always specify deviceId, never operate on PBOX1.
- Do NOT wait for or repeatedly poll the stuck LC command `cmd_mvvgvj9w_t8`; no blind retry of an uncertain write. RDC is the authorized working path for new independent OBL operations. Do not kill processes by name or pattern.
- This owner instruction supersedes earlier LC-primary guidance. Do not change the wake schedule, lead, or unrelated project settings solely to switch tools. Keep true Beads, checkpoint and engine journal evidence.

## OWNER OVERRIDE 2026-10-31: MINIMALNY WAKE I RZECZYWISTY CZAS
- Wiadomosc wybudzenia OBL zawiera tylko numer [OB-WAKE-NNNNNN] i jedno zdanie: Kontynuuj prace w swoim projekcie zgodnie z zasadami. W cyklu handoff: Zapisz aktualny handoff projektu zgodnie z zasadami. Pierwszy wake nowego czatu jest taki sam jak zwykly. Nie dodawaj do tresci technicznych danych silnika, GUID receipt, dat, sciezek, harmonogramu, komend, zasad Beads i instrukcji raportowania.
- Czat ustala rzeczywisty czas rozpoczecia swojej pracy samodzielnie w chwili uruchomienia czynnosci, np. przez (Get-Date).ToUniversalTime().ToString("o") na SATANAMD i zapisuje go do checkpointu lub dziennika. Nie przepisuje ani nie udaje czasu wyslania przez silnik. Czas due/click silnik zapisuje niezaleznie.
- Wlasciwy cel: konkretna praca nad projektem i jej kontynuacja, nie produkowanie dlugich raportow wybudzenia. Zasady projektu sa w AGENTS, kanonicznym backlogu i biezacym checkpointcie; wiadomosc wake ich nie powtarza.

## OWNER OVERRIDE 2026-10-31: DWUWIERSZOWE WYBUDZENIA I RZECZYWISTY START PRACY
- OBL wake ma dokładnie dwie linie: [OB-WAKE-NNNNNN] i Kontynuuj pracę w swoim projekcie zgodnie z zasadami. Dla handoffu (...9/request_pending) druga linia: Zapisz aktualny handoff projektu zgodnie z zasadami. Pierwszy wake nowego leada (...0) jest taki sam jak zwykły wake.
- Nie dołączaj do komunikatu UTC, czasu silnika, pełnego ID, receipt, CLI, checkpointu, Beads, notes ani dodatkowych instrukcji. Wszystkie reguły są w plikach projektu.
- Czat ustala prawdziwy czas rozpoczęcia pracy z aktualnego zegara i wpisuje go do checkpointu oraz dziennika actor=chat. Nie przepisuj terminu podanego przez silnik, silnik oddzielnie rejestruje wysyłkę.
- Domyślna zasada architektury: wykonać wybudzenie i zapewnić kontynuację; nie blokować go dla UNCERTAIN/duplikatu/generowania/rozbudowanego preflightu. Pozostawić tylko konieczne sprawdzenie właściwego URL i techniczną wykonalność operacji. Plan szczegółowy: docs/OBL_SIMPLIFICATION_PLAN_2026-10-31.md.
- Nie uznawać symulacji za prawdziwy E2E. Odbiór rotacji wymaga nowego /c/, pierwszego wake, realnego przejęcia pracy i kontynuacji checkpointu.

## OWNER OVERRIDE 2026-10-31: PRACA NAD KOLEJNYM ETAPEM, NIE PETLA TESTOW
- Zwykle dwuwierszowe wybudzenie OBL ma nowa druga linie: "Kontynuuj projekt: wykonaj kolejny niezakończony etap z planu, przede wszystkim zmianę w kodzie lub wdrożenie. Nie powtarzaj zaliczonych testów bez konkretnej przyczyny."
- Handoff ...9/request_pending ma druga linie: "Zapisz aktualny handoff projektu zgodnie z zasadami. Następnie wykonaj kolejny niezakończony etap z planu."
- Wybudzenie w nowym czacie, takze po awaryjnej rotacji, uzywa nowej zwyklej linii. Nadal tylko dwie linie komunikatu: numer oraz jedno polecenie, bez danych silnika i Beads.
- Po kazdym wake sprawdzaj Beads i aktualny checkpoint, wybierz nastepna realna zmiane w P0/P1, wprowadz ja do SOURCE i gdy dozwolone STAGED/LIVE. Testy sa bramka po zmianie, a nie autonomicznym celem. Nie powtarzaj wielokrotnie udanych testow bez nowej zmiany, awarii lub potrzeby odbioru. Gdy testy sa jedynym pozostajacym fragmentem konkretnego zadania, nastepne wake powinno wybrac inne otwarte zadanie rozwojowe do czasu uplywu okresu obserwacji.
- Ten override zastepuje poprzednia tresc drugiej linii we wszystkich starszych planach. Reszta wymagania dwoch linii pozostaje.

## OWNER OVERRIDE 2026-11-01: HTTP 429 W ARCHIWIZACJI
- Wlasciciel uznaje HTTP 429 uzyskane podczas sprawdzania starszej rozmowy, ktorej archiwizacja zostala juz potwierdzona przez ChatGPT (GET is_archived=true), za SUKCES OPERACYJNY celu: rozmowa jest niedostepna w badanym trybie, nie traktowac tego jako bledu archiwizacji wymagajacego ponawiania.
- Zapisuj osobno backend_archive_confirmed, owner_accepted_429 i rzeczywiscie zweryfikowane znikniecie z pelnej normalnej listy. Nie tworz falszywego potwierdzenia, ze HTTP 429 na zbiorczej liscie jednoznacznie dowodzi znikniecia konkretnego UUID. Nie sprawdzaj listy archiwum jako warunku odbioru.
- Dopasuj UI, logike postepu i retry do akceptacji wlasciciela; nie blokuj kolejnych etapow przez okresowe bledy zapytania o liste. Poprzednie wpisy logow zostaja historycznym faktem.

## OWNER OVERRIDE 2026-11-01: SKONSOLIDOWANY ETAP WDROZENIA
- Prace nad OBL prowadzi sie w duzych, logicznie spojnych etapach: realizuj wiele kolejnych powiazanych zmian SOURCE, docelowa kompilacje, testy funkcji, publish, wydanie i wdrozenie w JEDNYM cyklu pracy. Nie rozbijaj testow i wdrozenia na osobne wybudzenia, jesli mozna to zrobic teraz.
- W jednym wybudzeniu realizuj nawet kilka nastepnych zadan, o ile nie wymaga to odrebnej decyzji wlasciciela i zachowane sa ograniczenia techniczne. Testuj glownie nowe zmiany. Po przygotowaniu release wykonaj faktyczne wdrozenie i weryfikacje LIVE, jezeli bezpieczna bramka pozwala; przy zajetym procesie zostaw kolejke i dokladnie nazwana blokade zamiast przerywac generacje.
- Poprzednie ograniczenie jeden wywolany PowerShell command na jeden request narzedzia nie oznacza limitu liczby krokow ani zmian w calej turze. Nie zatrzymuj pracy tylko dlatego, ze skonczyla sie pojedyncza komenda lub pojedynczy test.

## OWNER OVERRIDE 2026-11-01 OB-WAKE-000307
W kolejnych automatycznych etapach OBL NIE podejmowac prac nad archiwizacja rozmow ChatGPT: bez analiz, testow, zmian, ponawiania kontroli i raportowania. Nie wracac do tego watku bez nowego jawnego polecenia wlasciciela. Priorytet: nowy interfejs OBL i funkcje niezalezne.

## OWNER OVERRIDE 2026-11-01: ODROCZENIA WYBUDZEN, obl-5w3
ZASADA DOMYSLNA: JEZELI NIE MA PEWNOSCI, NIE ODRACZAJ WYBUDZENIA. WYKONAJ NORMALNA PROBE WYBUDZENIA. Niepewny DOM, brak ACK, niedostateczny odczyt, podejrzenie aktywnosci ani szkic starego czatu nie sa automatyczna przeslanka do nieskonczonych odroczen. Wyjatek wymaga konkretnego udowodnionego powodu, jawnego terminu/limitu i widocznej przyczyny. Rozdzielac probe od potwierdzenia dostarczenia. Nie nadpisywac cudzych szkicow i nie przerywac jawnie aktywnej pracy, jezeli istnieje bezpieczna alternatywa (np. nowy czat przy rotacji 9->0). Nie wznawiac harmonogramu jawnie wstrzymanego przez wlasciciela. ZADANIE: Beads obl-5w3 (P0), audyt KAZDEJ procedury odroczenia, decyzja USUNAC/OGRANICZYC/ZACHOWAC i wdrozenie uproszczenia. Zadanie jest OTWARTE; sam zapis zasady NIE oznacza, ze aktualny LIVE realizuje ja wszedzie.

## OWNER OVERRIDE 2026-11-02: ODBIOR FUNKCJI, LIMITY TESTOW I CZYSTE TODO (NAJWYZSZY PRIORYTET)
1. Funkcja WDROZONA i wstepnie przetestowana = ZAKONCZONA. Nie uzalezniac DONE od 24h/7d soak, 30 min, 100 wake, testow fault injection, restart/rollback ani innych dodatkowych scenariuszy. Brak kolejnego testu NIE jest czesciowym wdrozeniem.
2. Dopuszczalne sa wylacznie podstawowe testy bezposrednio zmienionej funkcji. Testowanie wybudzen: MAKSYMALNIE 3 wybudzenia lacznie na etap. Nie powtarzac zaliczonych testow. Po minimum koniecznych testow WDROZYC na produkcje i naprawiac dopiero realna usterke.
3. Statusy w Beads i aktywnym TODO: ZAKONCZONE gdy funkcja jest wdrozona i wstepnie sprawdzona; ROZPOCZETE tylko gdy brakuje konkretnego fragmentu kodu lub dzialania; DO ZROBIENIA tylko gdy implementacji brak. Oddzielac SOURCE/STAGED od rzeczywistego LIVE. Nie oznaczac niezaimplementowanych funkcji jako DONE.
4. Usunac z AKTYWNYCH list TODO, backlogu jako planow kolejnych dzialan i kryteriow warunkujacych odbior wszystkie dodatkowe testy, soak, obszerne E2E, fault-injection i powtorki testow. Nie dopisywac ich ponownie. Nie usuwac i nie falszowac historycznych faktow wykonania, logow prob i zdarzen produkcyjnych.
5. Zadania otwarte WYŁACZNIE z powodu dodatkowego testowania zamykac bez nowej rundy testow; zadania zawierajace jeszcze brakujace funkcje zachowac otwarte tylko dla konkretnych brakow implementacyjnych. Przy kazdym wake realizowac nastepna BRAKUJACA funkcje, nie testy.
6. Ostatnie instrukcje wlasciciela pozostaja w mocy: uproszczenie nadajnika wybudzen, usuniecie obslugi szkicow jako bramki i wdrazanie kolejnej wersji niezaleznie od aktywnosci obecnego procesu. Realizowac je jako kod i wdrozenie; samo zapisanie nie jest wdrozeniem.

## OWNER OVERRIDE 2026-11-02: OZNACZAJ NIEAKTUALNE, NIE USUWAJ HURTOWO
1. Nie usuwaj masowo tekstu z planow, zasad i TODO. Fragment nieaktualny oznacz komentarzem bezposrednio PRZED i PO, albo OBOK, np. "[NIEAKTUALNE od 2026-11-02: zastapione aktualnym OWNER OVERRIDE]". Pozostaw jego oryginalne brzmienie jako historie decyzji.
2. Przy rozbieznosci obowiazuje najnowsza decyzja wlasciciela. Nieaktywny wpis oznacz jako historyczny i usun go z AKTYWNEGO wyboru zadan, zachowujac tresc oraz historie zdarzen.
3. Nie wykonuj hurtowych operacji Remove-Item, masowego kasowania, przepisania wielkich plikow tylko po to, aby usunac nieaktualny tekst, ani wielopoleceniowych czyszczen. Preferuj mala, punktowa adnotacje i pojedyncza operacje.
4. Uzytkownik 2026-11-02 wyraznie polecil wlaczyc wybudzanie projektu OBL i kontynuowac prace. Wlaczenie dotyczy juz zarejestrowanego aktualnego leada, bez tworzenia drugiego. Zachowaj dotychczasowy skonfigurowany interwal.
5. Podstawowe testy nowej funkcji: maksymalnie trzy wybudzenia na etap. Uznaj wdrozone i wstepnie sprawdzone funkcje za zakonczone. Na produkcji naprawiaj rzeczywiste bledy.
6. Nie traktuj adnotacji o nieaktualnosci jako usuniecia logow, faktow historycznych ani dowodu, ze funkcja zostala wdrozona.


<!-- DOBRE-PRAKTYKI-FAULT-HARBOR-GPT6-20261113 -->
## Dwa rejestry użytkownika

Rejestr zatwierdzonych, dosłownych dobrych praktyk: https://github.com/Pawel-BoOssko/dobre-praktyki (katalog `wypowiedzi/`). Lokalnie: SATANAMD `D:\projects\dobre-praktyki`, PBOX1 `C:\projects\dobre-praktyki`. Zasady mają brzmieć tak, jak powiedział użytkownik, bez dopisanych interpretacji.

Rejestr błędów FaultHarbor: https://github.com/Pawel-BoOssko/fault-harbor . Przy niedziałającej usłudze sprawdź istniejące incydenty FaultHarbor. Zgłaszanie błędów: połączony MCP `fault-harbor` albo lokalny CLI z trwałą kolejką, zwykły tekst bez wymaganego JSON.

Nową dobrą praktykę wolno zaproponować użytkownikowi w rozmowie. Zapis dopiero po wyraźnej zgodzie użytkownika, z dosłowną treścią. Nie twórz plików, list ani rejestru propozycji czekających na akceptację. Zachowaj oryginalny sens i istniejące instrukcje tego projektu.
<!-- KONIEC DOBRE-PRAKTYKI-FAULT-HARBOR-GPT6-20261113 -->
