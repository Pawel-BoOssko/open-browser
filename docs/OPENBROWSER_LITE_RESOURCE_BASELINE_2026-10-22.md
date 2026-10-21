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
