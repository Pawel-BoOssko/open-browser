# Audyt zgodności poleceń właściciela z OBL_BACKLOG — 27.10.2026, wake 000028

Zakres: kanoniczny backlog 34 ID, ostatnie jawne polecenia właściciela, aktualny PLAN_DALSZYCH_PRAC_2026-10-27.md, AGENTS, OPERATING_RULES i dowody z LIVE. To audyt dostępnych źródeł, NIE gwarancja przeczytania każdego historycznego czatu. Backlog nadal jedyną listą zadań; plan i ten raport są materiałem dowodowym.

| Polecenie | Zadania kanoniczne | Stan |
|---|---|---|
| Pracuj wyłącznie na SATANAMD przez LC, interaktywny Session1, szybkie wydania i rollback | P0-01/02/15 | LIVE 1.73.36 potwierdzone, brak 24h testu |
| Jeden lead, naturalne wybudzenia co około 15 min, zero Tasks, zero uncertain retry | P0-01/03/10/17 | dwa prawdziwe seq27/28 visible_ack po blokadzie; 24h jeszcze OPEN |
| Notatki wyłącznie wewnątrz edytora OBL, autosave per chat, załącz do KAŻDEGO następnego wake; nie przerywaj generowania | P0-01/12, P1-03 | v1.73.36 jeden snapshot do payload i telemetrii, 3 symulowane kolejno wake + edycja + drugi czat PASS, brak kompletnego realnego 24h E2E |
| Nie twórz międzyczatowego inboxu ani przesyłania zwykłych wiadomości | P1-03/05/09 | zakaz dopisany, stare UI/outbox wymaga jeszcze inwentaryzacji i decyzji przed usunięciem |
| Automatyczna sukcesja przed limitem i po terminalnym błędzie, trwały URL, widoczny ACK, checkpoint, atomowo jeden lead | P0-08/09/10/13 | PARTIAL; seq26 trwała rozmowa i odczyt odpowiedzi, brak pełnego checkpoint/rotacji i 3 E2E |
| Po potwierdzonej sukcesji automatycznie archiwizuj starego leada w OBL i natywnie ChatGPT; niczego nie kasuj | P0-08, P1-03/07 | OPEN; konieczne potwierdzenie native Archive lub pending_archive |
| Archiwizuj lokalnie tylko 12 najnowszych tur, chroń PARTIAL/FINAL i projekty, ogranicz DOM do10 | P0-04/06/07/14/18 | realny DOM8 po cleanup; retencja Audit kept10, removed0; soak otwarty |
| Audytuj i sprzątaj foldery, chroń źródła/Git/profil/rollback; zachowuj manifest SHA | P0-14/15, P1-10/11 | odzyskano 10,341,982B build-only; usterka Audit/Apply dynamic eligibility, trzeba zamrozić allowlist |
| Oceń zasadność każdej kontrolki UI, dwa warianty układu, priorytet centralnego chatu i Notatek | P1-01/02/03/12 | OPEN; pełna analiza UX i testy 1280x850/1920x1080 nieukończone |
| 2/5/10 projektów i jeden worker, bez cross-chat, niezawodne zasoby | P0-05/11/18, P1-13 | OPEN/PARTIAL, brak testów soak/wieloprojektowych |
| Szczere checkpoint/ACK, zero fikcyjnych sukcesów, praca dalej po błędzie | P0-12/17 | seq28 actor chat_started, prawdziwy engine ACK; kolejne weryfikacje wymagane |

Wynik: wszystkie znane z aktualnego planu grupy poleceń są obecnie jawnie odwzorowane w backlogu. Siedem wierszy (P0-01, P0-08, P0-12, P0-14, P1-01, P1-03, P1-05) doprecyzowano i przetestowano walidatorem 34/34 PASS. Jest to pokrycie WYMAGAŃ, nie wykonanie wszystkich zadań. Priorytet wykonawczy nadal P0-08 (sukcesja), równolegle 24h stabilności wybudzeń i wyeliminowanie regresji cleanup.