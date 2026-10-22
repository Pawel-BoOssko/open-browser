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

## 2026-10-22 03:38 CEST: oddzielenie próbek aktualnego PBOX1 LIVE PID8216 i analiza sender-retention

- PBOX1 88 próbek pełnych z zakresu 00:43:01–03:37:01 CEST (2.9h) obecnej instalacji LIVE v1.72.21, 7 procesów WebView2: suma privateMiB początkowo561.9, ostatnio901.5, min561.9, max901.5, średnia807.9, największy renderer max700.0MiB. Zbiorcza historia 7.7h przed filtrem zawierała wcześniejsze instancje/wersje i chwilowe 2828.7MiB, nie jest porównywalna i nie należy jej uznawać za 8h soak aktualnej wersji. Nie ma jeszcze DOM full census ani CPU/DOM korelacji dla LIVE.
- SOURCE/TEST 1.72.33 SatanAMD: usunięto nieograniczoną retencję pełnych wiadomości i referencji do DOM z `LiteResumeSender.js` po click_invoked lub blocked. Niewysłane pending<=8, metadata attempted/phases<=128; lazy expiry15min. Nie można szacować efektu w MiB bez produkcyjnego A/B. Czeka prawdziwe dozwolone wdrożenie PBOX1, nowy resource baseline i 8h obserwacji, razem z real-page DIL QA. Hard prune i modern soft CSS default pozostają OFF.

## 2026-10-22 08:00 CEST — 7h10m ciągła seria tej samej produkcyjnej wersji (PBOX1)

- Źródło: tylko read-only PowerShell na PBOX1: `resources-2026-10-21.jsonl` + `resources-2026-10-22.jsonl`, po UTC i filtrze host PID8216, identycznym zbiorze siedmiu WebView2 PID, przerwach próbkowania <=300s. Bez mieszania historycznych procesów/wersji. Data początku 2026-10-21T22:43:01Z (00:43 CEST), ostatnia 2026-10-22T05:53:01Z (07:53 CEST); **216 próbek, 7,17h**; LIVE 1.72.21 (stara produkcja).
- Suma `privateMiB` siedmiu własnych WebView2: pierwszy **561,9**, ostatni **1026,0**, zmiana **+464,1 MiB**, minimum561,9, max1063,8, średnia898,1 MiB. Max private największego renderera839,1MiB (renderer PID20756); aplikacja WinForms host private pierwszy19,3 i ostatni20,9MiB. Liczba własnych WebView2 pozostaje 7. Min wolnej RAM systemu5783MiB; CPU wszystkich WebView2 średnio5,50% maszyny, max28,94% (nie jest to dokładne CPU każdej pojedynczej rozmowy).
- Okna względem 07:53 CEST: 5min 3 próbki i 4min efektywnego zakresu, suma WebView2 pierwszy1046,9, ostatni1026,0, max1060,0MiB; 1h 31 próbek, pierwszy984,9, ostatni1026,0, max1063,8MiB; okno 24h faktycznie obejmuje **jedynie 7h10**, a nie pełne24h. 8h próg jeszcze nieosiągnięty. Cała seria ma ten sam zestaw PID i obsługuje 1 foreground chat; nie jest 8h soak nowej wersji ani wieloczatu.
- Wniosek: **rzeczywisty wzrost private memory +464,1MiB** przez 7h10, ale nie wolno stwierdzać wycieku bez korelacji CPU/DOM, długości rozmowy i pracy renderera. PBOX1 LIVE1.72.21 nie generuje pełnego `dom_status.totalDomElements`; nowsza SOURCE zawiera census, ale nie jest wdrożona. Nie zabijać rendererów ani nie uruchamiać hard DOM prune w odpowiedzi na samą liczbę MiB.
- Naprawiono **błąd pokrycia dobowych raportów** w `scripts/analyze-lite-resource-trends.ps1`: opcjonalne `-AdditionalPaths` pozwala łączyć sąsiadujące pliki `resources-YYYY-MM-DD.jsonl` po UTC; sortowanie chronologiczne i `-MaxSamples` liczone po scaleniu, a nie osobno per plik. `scripts/export-lite-resource-dashboard.ps1` ma analogiczne `-AdditionalInputPaths`. Domyślna analiza jednego pliku zachowuje dotychczasową zgodność. Nie zmieniono collectora ani nie dodano cyklicznych narzędzi/timerów.
- Testy 2026-10-22 07:59 CEST SatanAMD: `tests/test-lite-resource-trends.ps1` **24/24 PASS** (18 dawnych +6 cross-midnight fixtures: stabilny PID, wzrost50MiB, kolejność argumentów nowy→stary, pełne okno24h, poprawny HTML); `node --test tests/lite-*.test.cjs` 20/20 PASS; `dotnet build Release` exit0, znany MSB3277 warning; 8 CLI selftestów EXE (łącznie izolowany WebView2) EXIT0; `git diff --check` PASS. SOURCE aplikacji i jej VERSION pozostają 1.72.45, nie tworzono nowej binarki/manifestu/instalacji; zmieniły się wyłącznie dwa istniejące skrypty analityczne, test i dokumenty.
- NEXT: po przekroczeniu 8h powtórzyć pomiar dla **TEGO SAMEGO PID/setu** i oddzielnie po dozwolonym wdrożeniu nowszej SOURCE zebrać >=8h nowej wersji razem z DOM census oraz real 5–10 zalogowanych rozmów E2E. Bez prób obejścia odmowy PBOX1 publish; autowake test nadal tylko foreground.

## 2026-10-22 08:44 CEST — pełny 8h baseline starego PBOX1 LIVE v1.72.21, PID8216

- PBOX1 read-only, oba dzienne JSONL `resources-2026-10-21.jsonl` i `resources-2026-10-22.jsonl`, filtr host PID8216, posortowane pełne UTC, dokładnie ten sam zestaw 7 PID WebView2 w każdym rekordzie; przerwy maksymalnie **161 sekund** (limit300), brak zmian PID lub liczby procesów.
- Pierwszy UTC `2026-10-21T22:43:01.3057510+00:00` (22.10 00:43 CEST); ostatni `2026-10-22T06:43:01.1553029+00:00` (22.10 08:43 CEST). Obserwacja **8,000h**, **241 kolejnych próbek**, **0 naruszeń zestawu PID / max przerw**. Nie ma podstaw nazywać jej automatycznie 8h testem pełnej funkcjonalności wielu czatów, gdyż nie potwierdzono 5–10 background E2E.
- Suma PRIVATE WebView2: **561,9 → 1023,3 MiB, +461,4 MiB**; maksimum **1073,2 MiB**, wszystkie dla PBOX1 starego LIVE 1.72.21. To potwierdzony wzrost pamięci, ale NIE dowód wycieku i NIE test nowego source/staged v1.72.47, ani korelacja DOM z RAM. Pierwsze pełne8h starej wersji mierzone bez restartu PBOX1; zachowano działającą aplikację, jakkolwiek od 08:33 widoczne `response_load_failed`.
- OBL-1.6 nadal **OPEN**: wymaga 8h nowej wersji po uprawnionym deployment, real 5–10 background schedule, CPU/RAM/DOM i poprawność wybudzeń. Nie włączać hard DOM prune na bazie samego RAM. Logi nie dowodzą przyczyny wzrostu.
