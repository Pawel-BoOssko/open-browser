# Open Browser Lite — plan wdrożenia uzgodnionego mechanizmu wybudzania
Data: 2026-10-27, SATANAMD. Kanoniczny backlog: docs/OBL_BACKLOG.md.

## Zasady nadrzędne
- Wybudzenia należą do PROJEKTU, jeden projekt ma jednego leada. Zmiana leada oznacza zmianę celu harmonogramu, nie kopiowanie harmonogramu.
- Wybudzenia to dostarczenie wiadomości. Brak blokad antyduplikacyjnych na podstawie poprzedniej odpowiedzi, ID, niepewnego potwierdzenia.
- Zachować lekkie porównanie docelowej ścieżki chat X z aktualną ścieżką WebView2, także bezpośrednio przed wysłaniem.
- Logi rozdzielone na bieżące (dziś i wczoraj) w projekcie oraz niekompresowane archiwum poza katalogiem roboczym. Każde zdarzenie w jednej linii .log/.txt; pełna historia zachowana.
- Jeden dotychczasowy checkpoint, bez automatycznego skracania/splitu, wpisy Notatki właściciela od razu dopisywane z czasem, bez ponownego wstrzykiwania do wake promptu.
- OBL tylko SATANAMD; żadne ChatGPT Tasks/PBOX1/ręczne Git commit/push/reset. Przy restarcie wyłącznie sprawdzone wydanie + interaktywna sesja Windows i rollback.

## Kolejność konkretnych etapów
1. [P0 IMMEDIATE] Ustalić bieżący chat URL z WebView2, wykryć istniejącego leada i aktywny harmonogram. Wykonać backup projektu i wybudzeń; wyłączyć harmonogram dawnego leada, przypisać bieżący czat do projektu jako jedyny lead, uruchomić harmonogram tego czatu 15 min w dotychczasowym silniku; ponowny odczyt i kontrola invariant one lead/one enabled wake. Nie restartować aktywnej generacji w ramach samego przełączania. To przejściowa operacja na istniejącym formacie per-chat, zanim migracja do projektu będzie gotowa.
2. [P0] Ustalić i naprawić rozbieżność faktycznego działającego EXE, aktualnej wersji i rekordu release-state, bez zniszczenia generowanej odpowiedzi. Potwierdzić plik wykonywalny, PID, SessionId=1, WebView2, rollback i watchdog.
3. [P0] Przenieść scheduler do jednego czytelnego rekordu projektu (lead URL, aktywny, stałe terminy/częstotliwość/strefa), usunąć DPAPI dla harmonogramu. Migracja z backupem i testem zachowania poprzednich terminów, pojedynczy lead.
4. [P0] Rozdzielić planowanie slotów od kontroli bieżącej godziny i samej wysyłki; brak driftu harmonogramu przy opóźnieniu, liczenie prób per slot; T1/T2/T3, czytelne ID prefiks projektu / numer slotu / ordinal próby / HHMMSS; kod projektu OBL lub NR dla CLI. CLI status/on/off/every/now dla kodu projektu, bez URL czatu i bez pełnego ID.
5. [P0] Uprościć treść wiadomości, jawnie oznaczyć PEŁNE ID wybudzenia i osobno KOD PROJEKTU DO CLI. Polecenie projektowe: checkpoint, backlog, AGENTS/zasady, następny priorytet, test/staging/LIVE/rollback, konkretne błędy. Nie kodować w promptach przestarzałych wersji.
6. [P0] WebView2: zachować istniejące route check, usunąć fingerprint/same_answer/attempted/uncertain anti-duplicate gates. Potwierdzać widoczność wiadomości do 15 sekund z natychmiastowym wyjściem po znalezieniu; osobno ewidencja wysłania i niepewności.
7. [P0] Diagnostyka interfejsu w języku polskim i angielskim: przechwycić widoczne komunikaty (w tym pełnoekranowe "ChatGPT napotkał problem / Coś poszło nie tak / Spróbuj ponownie", za długa rozmowa, błędy odpowiedzi) z rzeczywistą treścią, etapem, hostem, URL-ścieżką i kodem błędu, bez treści prywatnych wiadomości. Zdarzenia logować konkretnie, nie ogólnikowo.
8. [P0] Notatki właściciela dopisywać od razu do obecnego checkpointu z lokalną datą/godziną jako "Wiadomość od Pawła", usunąć ich duplikację w wiadomości wake.
9. [P0] Testy jednostkowe, scenariusze na prawdziwej przeglądarce: stare/nowe URL, zmiana leada, wznowienie po restarcie, polski ekran awarii, terminal za długi, błędy HTTP, dwie opuszczone pory i alarm po 3 brakach T3; wydania małe, test -> staging -> wdrożenie przez interaktywny task -> sprawdzenie LIVE/PID/WebView/ACK -> rollback.
10. [PÓŹNIEJ — DISCUSSION ONLY] Skąd i jak pierwsza wiadomość do NOWEGO projektu/czatu, jak uruchomić następcę, kto konfiguruje pierwszy prompt. Bez decyzji o automatycznym utworzeniu do czasu osobnej rozmowy.

## Fakt wejściowy i blokery
2026-10-27 ok. 19:14 CET: bieżący WebView2 wskazuje rozmowę /c/6ae07710-f0c8-83eb-a51e-dd50236a16b2, generating=true. Stary lead /c/6adf463c-4b80-83eb-9a4c-777a89f74068 ma enabled=True, interval 15m. OBL exe PID 9540 SessionId 1 FileVersion 1.73.34, natomiast stary zapis release-state mówi 1.73.38. Nie deklarować 1.73.38 jako działającego LIVE.
Kryterium sukcesu pierwszego etapu: tylko aktualny lead nowego czatu dostaje wybudzenia i status persisted, nie ma aktywnej starej ścieżki, brak utraconych plików i nowych GUI procesów.