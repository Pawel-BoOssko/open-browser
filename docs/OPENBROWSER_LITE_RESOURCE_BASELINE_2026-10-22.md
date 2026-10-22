# Open Browser Lite: punkt odniesienia zasobów (PBOX1, 22.10.2026)

## Warunki pomiaru

- **LIVE:** v1.72.20, PID 8352, jedna aktywna rozmowa projektowa, zadanie autowybudzania 20 min i jeden współdzielony WebView2 worker.
- **Czas:** 00:21–00:27 lokalnie w Warszawie, 4 próbki automatycznej diagnostyki aplikacji z `resources-2026-10-21.jsonl`.
- **DIL / rozmowy:** `dom_status` w sprawdzanym czacie około 7–8 rozpoznanych wiadomości, `optimized=0`. Modern DIL jest jeszcze `observe-only`, więc zero nie jest błędem sensora, a dowodem braku aktywnego odchudzania.
- **RAM:** 7 procesów WebView2 przypisanych do Open Browser Lite, łącznie 618,6 MiB *private bytes* w ostatniej próbce. Największy renderer 446,3 MiB, w 4 próbkach 446,3–481,2 MiB; proces główny WinForms 20,0 MiB. Pozostała dostępna pamięć systemowa 6108 MiB. Mierzone wartości nie są sumą fizycznie unikalnej pamięci.
- **Uwaga:** na całym PBOX1 znajdowało się również około 40 procesów `msedgewebview2` (2352 MiB prywatnych) używanych przez inne programy; nie przypisywać ich wszystkich Lite.

## Wnioski techniczne

1. Największy koszt to renderer aktualnej rozmowy, nie sam proces WinForms. Problem wielogodzinnego RAM należy obserwować właśnie w tej grupie.
2. Moduł `LiteDomOptimizer.js` ma dziś bezpieczne `content-visibility:auto` tylko dla starego układu; nowy DIL jest tylko mierzony. `LiteDomHardPrune.js` domyślnie OFF i ma stare selektory.
3. Nie włączać dziś `hardPrune` bez testów kompatybilności React/DIL. Rozważony pierwszy etap: CSS `content-visibility:auto` dla dawno zakończonych tur DIL z możliwością rehydratacji; pomierzyć różnicę przed włączeniem produkcyjnym.
4. Ponieważ jest tylko jedna widoczna rozmowa i maksymalnie jeden worker, prosty mechanizm WebView2 `CoreWebView2.MemoryUsageTargetLevel = Low` dla **ukrytego workera** może ograniczyć jego koszt bez wyłączania skryptów. Microsoft opisuje go jako działanie best-effort, nie gwarancję spadku RAM. W źródłach dopisano to jako eksperyment; źródło wymaga testu izolowanego i potwierdzenia rzeczywistego wybudzenia po zmianie.
5. Kryterium skutecznej optymalizacji: co najmniej 8h próbkowania rzeczywistej długiej rozmowy, wyniki max/avg renderer private MiB, liczba procesów WebView2, średnie CPU i czas reakcji; żadnych utraconych wybudzeń.

## Następne kroki

- Po uzyskaniu **dwóch niezależnych naturalnych wybudzeń** z receipt i nową odpowiedzią: pilotaż bezpiecznego DIL soft optimization, z pomiarami w trybie włączonym i wyłączonym.
- Nie dodawać nowego collectora, ponieważ istniejące pliki `resources-YYYY-MM-DD.jsonl` i dashboard HTML zawierają potrzebne dane.

## Postęp SOURCE 1.72.23 / 00:48

- Pilotaż `LiteDomOptimizer.js`: opt-in `__OPENBROWSER_LITE_DOM__.setModernSoftEnabled(true)`. Przy co najmniej 60 nowoczesnych wiadomościach i bez legacy React wrappers zakłada `content-visibility:auto` wyłącznie na starsze niż 40 wiadomości elementy DIV/ARTICLE/SECTION w układzie blokowym, mające realną wysokość >=80px, bez fokusu i bez aktywnego generowania. Domyślnie **OFF**, wyłączenie usuwa wszystkie własne flagi. Nie usuwa treści ani node'ów i nie może samo w sobie ograniczyć liczby DOM nodes; teoretycznie może zmniejszyć pracę layout/paint.
- 5 nowych testów pilotażu PASS, razem 20/20 testów JS PASS i C# Release build exit0 na SatanAMD. Rzeczywisty DOM QA nieprzeprowadzony; bez niego nie włączać dla głównej rozmowy.
- Ukryty worker WebView2: normalny poziom zużycia pamięci podczas nawigacji/wysyłki, `Low` tylko w spoczynku po zakończeniu kroku. Oczekiwana oszczędność nie jest liczbą stałą; wymaga testu porównawczego w produkcji i nie może pogorszyć liczby potwierdzonych pobudek.
- Kontrola PBOX1 LIVE v1.72.21 PID8216: próbki 00:45 i 00:47 mierzyły odpowiednio 649,5 i 637,4 MiB prywatnej pamięci wszystkich 7 procesów WebView2 Lite; największy renderer 469,8 i 458,4 MiB. Jest to ciągle **stan sprzed** wdrożenia SOURCE 1.72.23, nie dowód efektu zmiany.
- Publikacja kandydata 1.72.22 na PBOX1 odmówiona przez zabezpieczenia narzędzia; brak dalszej próby po tej odmowie, kandydat 1.72.23 nie jest wdrożony.

## Aktualizacja 2026-10-22 01:18 CEST: 30-min obserwacja produkcji i licznik DOM w SOURCE

- LIVE v1.72.21 PID8216 (7 własnych WebView2 processów), 16 próbek 00:43–01:13: sum(privateMiB) start 561.9, koniec 882.3, average 707.3, max 882.3; max(largest renderer) 687.6 MiB. CPU po odrzuceniu jednej niepełnej próbki: 15 kompletnych, average sum WebView cpuMachinePercent 5.25%, peak 8.24%. To nie jest dowód memory leak, ale wzrost związany w czasie z długim generowaniem, wymagający dalszej obserwacji.
- SOURCE v1.72.26: LiteDomOptimizer.js wykonuje lekki DOM census getElementsByTagName('*').length najwyżej co 120 sekund wyłącznie poza generowaniem. ChatGptLiteForm.cs serializuje totalDomElements oraz domSampleAt do istniejących zdarzeń dom_status, bez nowych usług i bez odczytu prywatnej treści. W przypadku braku pola lub formatu nienumerycznego stan jest null, nie wyjątek.
- Testy: 7 scenariuszy DIL soft pilot (w tym limit próbkowania i brak census podczas Stop), 20/20 plików Node test PASS, Release .NET 0 errors, izolowany WebView2 21 checks PASS. To testy źródeł, nie produkcyjny trend DOM.
- SOURCE v1.72.26 zwiększa możliwość reużycia zhydratowanego worker WebView2, zamiast wymuszać reload przy tym samym due URL. Konkretny wpływ RAM/CPU i poprawność wieloczatowa wymagają dopiero LIVE E2E.
- Cel: po dozwolonej publikacji v1.72.26 korelować dom_status.totalDomElements z privateMiB renderera i cpuMachinePercent w godzinowych oknach oraz 8h soak; rozróżniać stały wzrost liczby elementów i wzrost pamięci przy stałym DOM. Do prawdziwego DIL QA nie uruchamiać domyślnie CSS pilota ani hard prune.

## 2026-10-22 02:34 CEST: stan produkcji v1.72.21 (przed zmianą 1.72.30)

- PBOX1 56 próbek ze stałego PID8216 v1.72.21 z 00:43:01 do 02:33:01 CEST, okno 1.83h, 7 własnych procesów WebView2: suma private min561.9, max882.3, średnia784.7 MiB, pierwszy561.9, ostatni858.0 MiB; największy renderer max687.6MiB. Nie kwalifikuje to 8h soak ani dowodu wycieku. W długiej rozmowie zmian generowania i wielkości DOM nie można jeszcze jednoznacznie skorelować.
- SOURCE/TEST 1.72.30 SATANAMD: per-worker _workerVerifiedChatUrl ustawia się dopiero po własnej poprawnej nawigacji i obecnej historii (scoped main), unieważniany na SPA SourceChanged na inny URL, NavigationStarting, ProcessFailed. Pozwala uniknąć kosztownego lub błędnego reuse po przełączaniu czatów; wpływ CPU/RAM nie był testowany na produkcyjnym PBOX1.
- Full DOM census co >=120s (poza generowaniem) nadal tylko w źródłach, real-page DIL soft CSS pilot pozostaje OFF, hard prune OFF. Realny multi-chat E2E i po-deploy RAM/DOM A/B oraz 8h+ soak nadal otwarte.
