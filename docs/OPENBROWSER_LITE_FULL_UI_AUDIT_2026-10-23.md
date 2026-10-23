# Open Browser Lite — audyt interfejsu element po elemencie
**23.10.2026 · E2E: LIVE v1.72.75, zrzut 1936×1066 PBOX1 · Decyzje, nie debata architektoniczna**

## Model użycia
5–10 intensywnie pracujących czatów, **jeden aktywny wykonawca na projekt**, bardzo częste wybudzenia i lokalne notatki; właściciel chce w 2–3 sekundy wiedzieć: *czy pracuje, nad czym, co zrobiło, kiedy następna pobudka, czy wymaga reakcji*. Istotne logi są widoczne na żądanie; pozostała diagnostyka nie konkuruje z rozmową. Laptop ma zachować niskie zużycie RAM/CPU. Założenie „rozmowa jest najważniejsza” jest słuszne. Dotychczasowe trzy stałe kolumny + własny sidebar ChatGPT nie realizują tego celu.

## Ocena każdego obszaru
Tabela: **potrzeba | rozmiar | miejsce | częstość użycia | decyzja**. Częstość to ocena projektowa, nie zmierzone statystyki (brak telemetrii kliknięć).

| Obszar / kontrolka | Potrzeba? | Rozmiar | Miejsce | Prawdopodobne użycie | Decyzja |
|---|---|---|---|---|---|
| Pasek tytułu + wersja/data | TAK, pomaga w awariach | obecnie nadmiar pełnej daty i drugi raz w toolbarze | u góry | często przy testach | Pozostawić numer wersji w tytule, w toolbarze skrócić do „v1.72.XX” i tooltip pełnej daty |
| Status „Gotowe”/„Generowanie”/„Błąd” | KONIECZNE | 150px bywa za mało na przyczynę | w górnym wierszu | stale | Duża zwięzła etykieta stanu; nie nazywać „Gotowe” wyłącznie po udanej nawigacji. Rozróżnić silnik/ChatGPT |
| Tryb High | TAK, ale rzadko zmieniany | 120px wystarczy | mała ikona/etykieta w diagnostyce | sporadycznie | Nie dominować nagłówka czerwonym „UNKNOWN” |
| Autopraca WŁ/WYŁ, następny termin, liczba czatów | KONIECZNE | ~250px | przy statusie na górze | stale | Jeden czytelny pasek „2 aktywne · następne HH:mm”, jeden przełącznik |
| ZATRZYMAJ AUTOSTART / wznowienie | KONIECZNE | 120–155px | stale widoczne przy autopracy i w zasobniku | podczas spotkań / interwencji | **P0**: marker pauzy respektowany przed Keepalive, przycisk „Pauza i zamknij”, ręczne wznowienie |
| Wstecz/Dalej | przydatne w zwykłej nawigacji | obecnie 90+85px | dolna listwa | rzadko | Dwie krótkie ikony z tooltipem (po przetestowaniu); nie zabierać 175px stale |
| Odśwież | TAK awaryjnie | 95px | przy czacie | czasem | Zachować, chronić szkic/generowanie przed utratą |
| ChatGPT (Home) | TAK | 95px | przy nawigacji | czasem | Zachować ikonę Home z tooltipem |
| Wybudzenia (konfiguracja) | TAK | ~110px | przy statusie autopracy | często na etapie pilotażu | Zachować w widocznym miejscu |
| Autopraca: WŁ.15m | TAK | ~130px | przy Wybudzenia | często | Zachować; etykieta powinna pokazać jedną właściwą aktywną rozmowę |
| Archiwum | TAK | 90px | sekundarne menu lub projekty | rzadko | Przenieść z głównego paska do Więcej |
| Ukryj/pokaż panel | TAK | 115px | przy prawym panelu / toolbarze | często | Zachować, umieścić bliżej panelu, startować z węższym panelem |
| Więcej ustawień | TAK | 141px | skraj prawej strony listwy | sporadycznie | Zachować, lecz menu kontrolować w pełni w QA; skrypty diagnostyczne wewnątrz |
| Licznik DOM 10 tur/0 odciążonych | TAK do diagnozy, NIE jako metryka główna | obecnie 200px | pasek techniczny | rzadko | Przenieść pod Status techniczny/Zdarzenia |
| Lista projektów | KONIECZNA | ~350px przy 1936px to ZA DUŻO; ~270px idealnie | lewa kolumna | stale | Domyślna szerokość 240–280px, możliwość schowania; nigdy nie przecinać Lead i WŁ/WYŁ |
| Pola „Nazwa projektu”, „Dodaj” | tylko przy tworzeniu | dwie kontrolki ~200px | na górze listy | bardzo rzadko | Ukryć za + Projekt |
| Puste projekty (Lupus, OpenFill, Transkrypcja AI itd.) | potrzebne w katalogu, nie w aktywności | każda pusta karta 40px | lista | rzadko | Zwinąć pod „Pozostałe”; aktywne pokazać na górze |
| Rozwinięte archiwalne czaty i wszystkie przyciski Lead | tylko przy wyborze wykonawcy | ciasne | pod projektem | okazjonalnie | Domyślnie zwinięte; karta aktywnego wykonawcy ma stan i następny termin |
| Sam ChatGPT WebView2 | GŁÓWNY CEL | obecnie centrum ~750px, w tym własny sidebar ChatGPT ~340px | środek | 90% czasu | Priorytet szerokości; ograniczyć okalające panele, nie ingerować destrukcyjnie w DOM/sidebar ChatGPT |
| Prawy panel Notatki | TAK, bardzo ważne | ~470px to ZA DUŻO dla pustych notatek | po prawej | często, gdy projekt trwa | Domyślnie węższy 290–330px, jednym kliknięciem schowaj |
| Zakładka Zdarzenia | KONIECZNA | tyle samo co notatki | w tym samym panelu | przy diagnozie | Zostawić; dopisać filtr na projekt oraz status, znaczniki czasu i łatwe kopiowanie |
| Przycisk Dyktuj | TAK jeśli używany | 140px | na dole notatek | czasem | Zachować w panelu notatek |
| „Kopiuj do czatu”, „Do kolejki”, Zapisz | obecnie 4 przyciski | 4× zajmuje 2 rzędy | na dole panelu | częstotliwość nieznana | W przyszłości ocenić na podstawie użycia, scalić do 1–2 akcji, nie usuwać bez testów |
| Powiadomienia WAIT_USER/BLOCKED | KONIECZNE | jeden czytelny status, nie popup co kilka min | globalnie i w projekcie | rzadko, ale ważne | Nie powtarzać na ten sam stan, e-mail raz na zdarzenie |
| Tray/Keepalive | KONIECZNE | menu łatwo dostępne | obszar powiadomień Windows | podczas telekonferencji | Po pauzie nic nie podnosić. Manualny start pozostaje możliwy |

## Wnioski i kolejność
1. **P0 bezpieczeństwo użytkownika — pauza i zamknięcie**: użytkownik może na godzinę wyłączyć całą aplikację, bez automatycznego powrotu; bez nowych ChatGPT Tasks. Dwa miejsca: przycisk i zasobnik. Manualny start otwiera aplikację nadal w pauzie; ręczne „Wznów” włącza Keepalive.
2. **P1 uproszczenie formy**: zredukować duplikaty informacji, zawęzić panel projektów i notatek, odzyskać minimum 250px dla rozmowy; używać maks. dwóch rzędów bez przewijania.
3. **P1 obserwowalność**: czytelne projekty aktywne i znaczniki wydarzeń z realną godziną w jednej strefie, potwierdzenie ACK/model-start/model-finish, oznaczenie błędów, bez udawania „gotowe”.
4. **P2 estetyka**: konsekwentne odstępy 8/12/16, typografia 12–14px dla statusów, kolory wyłącznie semantyczne; spójne etykiety, bez losowego czerwonego UNKNOWN; ciemny tryb dopiero po stabilnym pilocie.
5. **Telemetry przed eliminacją kontrolek**: brak licznika rzeczywistych kliknięć. Nie deklarować „nieużywane” na podstawie domysłu; opcje niskiego prawdopodobieństwa przenosić do menu, nie usuwać definitywnie.

**Kryteria akceptacji:** użytkownik może jednym widocznym działaniem wstrzymać autostart i zamknąć aplikację, Keepalive w tym czasie nie uruchamia jej ponownie, po ręcznym uruchomieniu stan jest jawny; obydwa projekty i notatki pozostają zachowane; nowe wydania publikowane na PBOX1 możliwie wcześnie po testach z rollbackiem i rzeczywistym screenshot QA.
