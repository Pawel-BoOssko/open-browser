# Open Browser Lite — rotacja długich czatów (wersja pilotażowa 2026-10-23)

## Cel i wymagania
Kiedy wykonawczy czat projektu staje się długi lub wolny, Open Browser prosi TEN czat o pełen, trwały HANDOFF, a następnie umożliwia przełączenie projektu na nowy rzeczywisty czat ChatGPT. Zawsze najwyżej jeden aktywny lead, żadnych dodatkowych ChatGPT Tasks, niepewnych retransmisji i zgadywanych adresów.

## Implementacja SOURCE v1.72.82
1. **Wykrycie** na podstawie metadanych już pobieranych przez bezpieczny preflight: **40 prób wybudzenia** danego czatu lub **80 tur obecnie widocznych w DOM**. Próg dotyczy wyłącznie zarejestrowanego aktywnego leada projektu. Pruning DOM może zaniżać widoczne tury; licznik wybudzeń jest trwalszą przesłanką. Oba progi są heurystyką, nie rzeczywistym licznikiem tokenów.
2. **Ręczne wyzwolenie**: przycisk `Poproś o HANDOFF` w panelu wybranego projektu ustawia w zaszyfrowanym harmonogramie stan `request_pending` i termin następnego bezpiecznego wybudzenia; NIC nie wysyła natychmiast i nie omija kontroli szkicu/generowania.
3. **Jednorazowa prośba**: `LiteChatWakeRegistry.NumberedMessage` dołącza polecenie przygotowania HANDOFF do bieżącego projektu: aktualny stan, pliki, testy, backlog, ryzyka, ostatni checkpoint. Model po rzeczywistym zapisie wskazuje pełną ścieżkę i kończy `[OB-STATUS: WAIT_USER]`. Stan `requested` utrwala się po potencjalnej próbie kliknięcia; nie ponawia automatycznie niepewnej wysyłki. Przed wysyłką definitely_unsent zachowuje `request_pending`.
4. **Weryfikowany stop**: istniejący `LiteModelStatus` rozpoznaje `WAIT_USER` wyłącznie po stabilnym, zakończonym, zgodnym z adresem preflight. Dopiero wtedy wyłącza harmonogram starego leada i przekazuje zwykłą interwencję właścicielowi. Sam tekst modelu NIE dowodzi jeszcze, że plik dokumentu rzeczywiście istnieje.
5. **Przejęcie**: w już utworzonym nowym czacie używa się `Przejmij otwarty czat jako wykonawcę`; weryfikacja realnego URL `https://chatgpt.com/c/...`, niezależnego od poprzedniego. Zapis nowego czatu w projekcie, degradacja starego do archiwum i aktywacja jednego nowego harmonogramu z dotychczasowym promptem plus nakazem wczytania HANDOFF. Operacja wykonuje bezpieczne kroki w kolejności wykluczającej równoległy start dwóch leadów; przy awarii może zatrzymać oba, nigdy ich zdublować.
6. **Widoczność**: panel projektu sygnalizuje stan `HANDOFF: kolejna pobudka`, `HANDOFF: oczekiwanie na zapis` lub `HANDOFF gotowy`. Prywatne teksty dokumentacji nie trafiają do dziennika mechanicznego.

## Nierozwiązana część automatyzacji — dalszy P0/P1
Pełne `auto create new chat + seed first message + read actual /c/ ID + promote lead` nie jest wdrożone w v1.72.82. Wymaga osobnego pilotażu przeglądarkowego z twardym filtrem szkiców, route identity, jednoznacznym ACK i trwałym fencingiem. Nie wolno przypisywać nieznanego URL ani tworzyć dwóch wykonawców. Sama praca w nowym czacie wymaga jego faktycznego utworzenia i pierwszej odpowiedzi, inaczej normalny preflight zgodnie z bezpieczeństwem zgłosi `assistant_not_complete`.

## Regresje i akceptacja
- `LiteChatHandoff.SelfTest` sprawdza oba progi, wybór tylko zarejestrowanego leada, jednorazowość, brak re-send po niepewnym kliknięciu, wykluczenie przejęcia zanim stary harmonogram uzyska `status_wait_user`, nowy URL i invariant jednego aktywnego wykonawcy.
- Stary build ignoruje dodatkowe pole `HandoffPhase` i może je utracić przy rollbackowym przepisywaniu DPAPI; przed pełnym automatem trzeba utrwalić fencing także niezależnie od wersji aplikacji (oddzielny dziennik lub journal/lease), żeby nigdy nie duplikować żądań po downgrade.
- Kryterium produkcyjne: SatanAMD publish + EXE8/8 + JS23/23; PBOX1 SHA i EXE8/8 z autoryzowanym narzędziem; przejęcie realnego czatu z dokumentem HANDOFF w testowym projekcie, rollback i zrzut UI. Nie przestawiać istniejących żywych projektów tylko po to, by potwierdzić scenariusz.

## Zasady
Bez wewnętrznych ChatGPT Tasks, ręcznych git commit/push, resetu repo, niepewnych powtórek i automatycznego przełączania bez potwierdzonego nowego chat ID.
