# OBL — audyt poleceń właściciela a backlog (2026-10-26)
Przeprowadzony na SATANAMD przez LC MultiHost target=satamad. Stan źródeł i logów do około 19:05 CET.

## Werdykt
NIE: dotychczasowe plany NIE zawierały wszystkich aktualnych decyzji w jednym backlogu. Stan był rozproszony w dok. 19–24.10 i checkpointach. Odczytane dokumenty nie dawały wiarygodnego rozróżnienia wykonane / część / brak dowodu.
UTWORZONO: docs/OBL_BACKLOG.md — kanoniczne 34 identyfikatory (18 P0,13 P1,3 P2), status i kryterium odbioru. Krótki kontekst ma wyłącznie LINKOWAĆ do backlogu.
Nie ma twierdzenia o przeczytaniu każdej rozmowy użytkownika: sprawdzono dostępny kontekst, pamięć, bieżące instrukcje i lokalne plany, nie ma certyfikacji absolutnej kompletności.

## Źródła porównania
1. MASTER_PLAN_MIGRACJI_I_ROZWOJU_2026-10-24 (P0-A1..A6, P1 B1..B6, migracja M00..M14).
2. EXECUTION_PLAN_2026-10-21 (etapy 0/1/2, statusy).
3. AUDYT_STANU_2026-10-24, CHAT_HANDOFF_ROTATION_2026-10-23, UI_IMPROVEMENT_PLAN_2026-10-23.
4. Starszy 86-elementowy docs/design/openbrowser-master-roadmap-v3-2026-10-19, jako historyczna lista techniczna, NIE aktualne polecenia.
5. AGENTS, OPERATING_RULES_2026-10-25, CURRENT_STATUS, CHECKPOINT, logi live-preflight/wake-lifecycle na SATANAMD.
6. Potwierdzone późniejsze wskazówki właściciela: tylko SATANAMD, FORCE proces+rollback, krótkie komendy, 12 lokalnych odpowiedzi, porządki w projekcie i oszczędny kontekst, jeden lead, bez ChatGPT Tasks.

## Braki krytyczne w dotychczasowym rozliczaniu
- P0-08: automatyczne tworzenie nowego czatu, weryfikacja URL, seed i promocja leada. Tylko ręczna sukcesja była potwierdzona. Ryzyko: ChatGPT osiąga limit rozmowy mimo sprawnego harmonogramu.
- P0-11 i P0-18: realna praca 5–10 projektów jednym workerem oraz archiwizacja odpowiedzi z background worker bez pomylenia projektu. Brak E2E na SATANAMD.
- P0-04/P0-05: DOM <=10 i stabilność pamięci bez 8/24h soak. LIVE 1.73.01 pokazuje <=10, ale nie udowodniono obcięcia prawdziwego 11+ ani długotrwałego RAM.
- P0-07/P0-13: odtwarzanie PARTIAL, checkpointu i stanu UNCERTAIN po awarii w środku długiej pracy nieprzetestowane.
- P0-09/P0-10: brak dostatecznej próbki rzeczywistego DIL/ACK i odporności na regresje/rollback; samo HTTP200 nie wystarcza.
- P0-16: migające okna PowerShell/cmd na SATANAMD zgłoszone, bez ustalonego procesu źródłowego.
- P1-01..08: uproszczenie UI/statusów, powiadomienia WAIT_USER/BLOCKED, wersja/timestamp, możliwa nadmiarowość DPAPI, notatki i handoff bez bieżącego produkcyjnego QA.
## Wykonane i zweryfikowane (zakres ograniczony)
- Seq000006: engine due 17:52:25Z, attempt reserved, submission_attempted i visible_ack 17:52:28.718Z; chat_started 17:53:47Z.
- 2026-10-26 LIVE v1.73.01, ostatni sprawdzony jeden proces PID17532, SHA 07D85F6825FE75515E58BA2AABF8B0E0F436DCBEFA563C197CE276423DDE7C3F, rollback v1.73.00.
- OBL watchdog przeprowadził prawdziwy restart 1.72.99 wcześniej; obecny automatyczny monitoring nie ma 24h soak.
- Local response retention: 422 -> 13 w pierwszej czystce; indeks bez orphan; zadanie Windows co 15 min. Archive starszych czatów lokalne usunięte na polecenie właściciela.
- Generated build cleanup: 150 artefaktów, ~17,06MB; codzienne zadanie Windows 03:30; checkpoint z historią ZIP SHA.
- Prace nad DOM v1.73.00 i telemetry v1.73.01, Node 3 scenariusze, 7 EXE selftests; ostatnie realne DOM7 tur, zdrowa telemetria. Nie udawać testu 11->10 na żywej stronie.

## Sprzeczności wyjaśnione
1. Stare idle-before-deploy — SUPERSEDED 26.10 przez jawne polecenie FORCE KILL/START/ROLLBACK. NIE ponawiać UNCERTAIN.
2. Stare długoterminowe local responses — SUPERSEDED 26.10 przez 12 najnowszych bieżącego czatu. Osobno pliki projektu/checkpoint.
3. Stare migracje lub wdrożenia PBOX1 — NIEAKTUALNE dla tej instancji. SATANAMD jedynym hostem.
4. Stare dokumenty WORK_STATUS 408KiB i pełny CHECKPOINT 67KiB — archiwum ZIP i krótki odnośnik; nie skanować ich co wake.
5. Mylące flagi [x] w planie 21.10 i poprzedni LIVE 1.72.95 — status historyczny, nie nowy dowód produkcyjny.

## Następna kolejność
1. Zabezpieczyć i przetestować automatyczny HANDOFF/rotację zanim obecny czat uderzy w limit (OBL-P0-08).
2. Poczekać na rzeczywistą liczbę >=11 tur i zebrać hard_dom_health, nie fałszować DIL w produkcji (OBL-P0-04).
3. Kontrolowane testy crash/partial/uncertain (OBL-P0-07/10/13), potem 8h/24h soak.
4. Test dwóch projektów, rozszerzenie do 5 i 10, jeden background worker (OBL-P0-11/18).
5. Uprościć GUI, stany właściciela i diagnostykę po potwierdzeniu P0.

## Zasada pracy z audytem
Aktualizuj tylko docs/OBL_BACKLOG.md po zmianie statusu i docs/OPENBROWSER_LITE_CURRENT_STATUS.md po wydaniu. W normalnym wake NIE czytaj tego raportu ani historycznych roadmap; wystarczy OBL_CONTEXT i kilka ostatnich linii checkpointu. Nigdy nie dopisuj fikcyjnego ACK czy dat.

## Aktualizacja po OB-WAKE-000022 (23:47 CET)
- Kanoniczny docs/OBL_BACKLOG.md nadal ma 34 zadania: 18 P0, 13 P1, 3 P2; wszystkie znane aktualne polecenia SATANAMD-only, niezawodne wybudzenia, jeden lead, FORCE deploy/rollback, DOM<=10, archiwizacja12 i cleanup, oszczedny kontekst, HANDOFF/rotacja oraz awarie/UNCERTAIN maja odwzorowanie. Nie stanowi to certyfikatu przeczytania absolutnie wszystkich dawnych rozmow.
- P0-08 pozostaje PARTIAL. Nowy proof seq21 odrebny od starego SeedUncertain seq9. W v1.73.22 doszlo do realnego click_invoked i dwoch zgodnych odczytow widocznej wiadomosci na nowym /c/local-chatgpt%3A...; nowy ledger SeedAcked Rev3. To NIE dowodzi trwalego serwerowego ID /c/<UUID>, odpowiedzi nastepcy ani bezpiecznej promocji.
- LIVE v1.73.23 dodaje blokade przejscia do NewCheckpointVerified i PromotionVerified dla tymczasowego local-chatgpt; testy negatywne PASS; jeden dotychczasowy lead, brak retry, rollback do 1.73.22.
- Przed uznaniem P0-08 za DONE trzeba niezaleznie zweryfikowac trwaly URL, odpowiedz i checkpoint nastepcy, jeden harmonogram, trzy realne E2E sukces/awaria/niepewnosc. P0-09 unhydrated DOM nadal OPEN.

## 2026-10-27 13:35 CET — Wake29 current-owner requirements crosswalk (SATANAMD)
Validated canonical backlog 34 stable IDs: P0=18, P1=13, P2=3. Current 2026-10-27 plan requirements map as follows: natural wake/Notatki -> P0-01,P0-12,P1-03; stable engine/watchdog HTTP429/no uncertain retry -> P0-03,P0-09,P0-10; one active lead plus technical successor and safe local/native archiving -> P0-08,P0-10,P1-04,P1-08; no ordinary cross-chat communication/inbox -> P1-05 and owner scope override; strict DOM/RAM -> P0-04,P0-05; folder cleanup with immutable audit allowlist and backup -> P0-06,P0-14,P0-18; full UI control-by-control reconsideration -> P1-01,P1-02; project isolation -> P0-11. User explicitly forbids PBOX1, ChatGPT Tasks, manual git writes and requires SATANAMD interactive deployment; covered in AGENTS/OPERATING_RULES and P0-02/P0-15. This crosswalk covers CURRENT PLAN and latest owner instructions; it does not certify that all historically unseen instructions were recovered.
Wake29 P0-14 actual fix: build cleaner Audit creates SHA manifest, Apply checks exact frozen candidates BEFORE deletes, unattended Cycle uses same frozen set. Actual build-17332 deletion exactly 29 files / 4,393,441 bytes, task LastTaskResult0 and zero further candidates. v1.73.36 remains healthy GUI; no need to restart for scheduled script update. P0-01/12 three recent real ACK seq27/28/29, not 24h proof. Full successor/native archive and P1 UX still pending.
