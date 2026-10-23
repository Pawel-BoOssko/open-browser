# Open Browser Lite — plan uporządkowania interfejsu (23.10.2026)

## Rzeczywisty stan UI
Obejrzano okno PBOX1 1280×850 przez PrintWindow bez zmiany aktywnego okna. Toolbar 42px z ~18 kontrolkami i poziomym przewijaniem; nawigacja i status mieszają się z technicznymi opcjami. Projektowe przyciski 238px są obcinane w panelu około 220px. Prawy panel notatek stale zabiera ~300px z miejsca na rozmowę i nie zawiera historii pracy. Pod notatkami jest mylące „Oczekuje na ręczne przekazanie”, mimo że notatki zapisane w DPAPI per chat są dołączane przy zewnętrznym wybudzeniu. Nie trafiają automatycznie do rozmowy już w toku.

## Zasady
- Jedna czytelna rozmowa jest priorytetem; wykorzystujemy obecne WinForms/WebView2 i rejestry, bez nowych agentów lub menedżerów.
- Status i termin wybudzenia przed opcjami eksperymentalnymi. Dziennik z istniejących zapisów silnika i aplikacji, bez treści rozmów.
- Nie zmieniać wysyłania, retry, profilu, mechanizmów rollbacku, DPAPI, Autosync i jednego wykonawcy na projekt.

## Etap A — SOURCE 1.72.75
1. Dwa wiersze toolbaru: stan aplikacji i harmonogramu; dalej nawigacja i główne akcje. Opcje techniczne w „Więcej ustawień”.
2. Dynamiczne szerokości przycisków projektów, aby Lead i WŁ/WYŁ nie były obcięte.
3. Zakładki „Notatki” i „Zdarzenia” w istniejącym prawym panelu, do 32 ostatnich zdarzeń JSONL, odczyt tylko przy otwartej zakładce, przycisk kopiowania.
4. „Ukryj/Pokaż panel”, aby zwiększyć szerokość centralnej rozmowy.
5. Prawdziwy komunikat: notatki tego czatu automatycznie dołączają się przy kolejnym wybudzeniu. Test izolacji dwóch osobnych notatek przy zachowaniu szyfrowania.

## Etap B — po kontroli realnego UI
1. Czytelne polskie statusy zamiast wewnętrznych kodów, statusy WAIT_USER i BLOCKED wyraźne i bez powtarzanych alertów.
2. Każdy projekt: wykonawca, WŁ/WYŁ, najbliższa godzina, ostatni ACK, start i koniec modelu, ostatni checkpoint.
3. Zachowanie preferencji szerokości paneli pomiędzy sesjami; tylko po teście recovery, bez ryzyka resetowania profilu.
4. Nie wprowadzać dodatkowych płatnych modeli, schedulerów ani nieudokumentowanego odpytywania endpointów.

## Kryteria akceptacji
Przy 1280×850 podstawowe akcje dostępne bez poziomego przewijania. Żadne notatki, harmonogram ani widoczny chat nie giną przy zmianie zakładek i ukrywaniu panelu. Logi tylko z metadanych, ograniczone rozmiarem, bez prywatnych wiadomości. Zielone testy JS/EXE i manifest SHA nie zastępują QA na rzeczywistym oknie produkcyjnym i możliwości rollbacku.

Bez ChatGPT Tasks, ręcznego Git commit/push/reset ani retransmisji niepewnych wiadomości.