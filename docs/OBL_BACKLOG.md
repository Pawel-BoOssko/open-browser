# Open Browser Lite: kanoniczny backlog aktywny

## DECYZJA WŁAŚCICIELA 2026-11-03: ZAMKNIĘTA LISTA W KAŻDYM ZADANIU
1. Na POCZĄTKU nowego zadania wykonaj jeden ograniczony przegląd istniejących dowodów i sporządź jedną numerowaną, ZAMKNIĘTĄ listę konkretnych braków lub usterek, z podaniem dowodu dla każdej pozycji. Jeśli brak dowodów: lista jest pusta, niczego nie naprawiaj.
2. Po zatwierdzeniu zakresu przez fakty zadania NIE DOPISUJ nowych pozycji w kolejnych wybudzeniach, poprawkach ani checkpointach. Realizuj wyłącznie pierwotne pozycje; skreślaj po naprawie. Nie otwieraj hipotetycznych awarii, prewencyjnych testów, kosmetycznych refaktorów ani nowych kryteriów odbioru.
3. Kiedy lista zostanie wykonana albo okaże się pusta, oznacz zadanie DONE i KONIEC. Nie generuj kolejnego etapu dla tego zadania. Nowy, później potwierdzony błąd to osobne zadanie z własną zamkniętą listą, nie dopisek do starej.
4. **P0 obl-5w3 i nadajnik wybudzeń są ZAAKCEPTOWANE oraz ZAKOŃCZONE.** Nie szukaj dalszych przeszkód w nadajniku; brak nowych otwartych problemów z tym mechanizmem. NIEAKTUALNE: polecenia kolejnych wake nakazujące obligatoryjnie znaleźć nową usterkę P0 lub każdorazowo zmieniać SOURCE/LIVE.
5. Zasada obowiązuje wszystkie kolejne ledy i wybudzenia OBL. Nie wyłącza istniejącego harmonogramu ani rotacji. Nie modyfikuj innych projektów. Przy rzeczywistej zmianie kodu zachowaj minimalną kompilację, SHA, LIVE i rollback.
Aktualizacja 2026-11-02. Host: SATANAMD. Niniejszy zapis zastępuje historyczne kryteria odbioru opierające się na kolejnych testach.

## Zasada nadrzędna
STATUS DONE oznacza funkcję wdrożoną i wstępnie sprawdzoną lub wyraźnie przyjętą przez właściciela. Nie pozostawiać jej jako PARTIAL tylko ze względu na brak kolejnych testów. Przeprowadzać wyłącznie podstawowe sprawdzenie nowej funkcji, maksymalnie TRZY wybudzenia na etap. Następnie wdrażać produkcyjnie; wracać do funkcji tylko po realnym błędzie. Żadne zadanie ani warunek odbioru w poniższym aktywnym planie nie obejmuje dodatkowych serii testów.

## ZAKOŃCZONE: nie wracać bez realnej usterki
| ID | Funkcja i wynik |
|---|---|
| P0-01 | Automatyczny harmonogram i rzeczywiste wybudzenia aktualnego leada. Naturalne wybudzenia z ACK działają w produkcji. |
| P0-03 | Podstawowy watchdog i przywracanie procesu. Mechanizm istnieje i był używany w produkcji. |
| P0-04 | Redukcja DOM do maksymalnie 10 tur. Implementacja i wstępne sprawdzenie WebView2 wykonane. |
| P0-05 | Jeden główny widok i współdzielony worker oraz podstawowa telemetria zasobów. Implementacja istnieje. Dalsza optymalizacja tylko po rzeczywistym problemie. |
| P0-06 | Retencja 12 ostatnich lokalnych odpowiedzi aktualnego czatu, z istniejącym harmonogramem czyszczenia. |
| P0-07 | Utrwalanie PARTIAL/FINAL i indeksu odpowiedzi. Przyjęte działające rozwiązanie, bez dodatkowych scenariuszy awarii. |
| P0-08, P0-20 | Rotacja leadów 9 do 0, checkpoint i kontynuacja. Właściciel 2026-11-01 uznał rotację za działającą i ukończoną. |
| P0-09 | Rozpoznawanie realnego stanu przeglądarki i oddzielne potwierdzanie doręczenia. Mechanizmy działają w LIVE. |
| P0-10 | Jeden aktywny lead i trwała identyfikacja wybudzenia. Wymóg eliminowania duplikatów nie obowiązuje: priorytet ma próba wysyłki. |
| P0-12 | Dziennik wybudzeń, receipt, czas i diagnostyka. Zdarzenia są rzeczywiście zapisywane. |
| P0-13 | Checkpoint i wznowienie pracy kolejnych wykonawców. Działa w zwykłym przebiegu projektu. |
| P0-14 | Mechanizm sprzątania odtwarzalnych plików, logów oraz rotacja checkpointów. Wdrożone elementy uznane za zakończone. |
| P0-15 | Oddzielne SOURCE/STAGED/LIVE, wersje, SHA, pojedynczy proces i release manager z rollbackiem. |
| P0-17 | Autonomiczne wykonywanie kolejnych zadań przez powtarzane wybudzenia, Beads START/STOP i checkpoint. |
| P1-03 | Lokalny edytor Notatki, autosave i izolacja notatek poszczególnych czatów. Nie rozszerzać jego zakresu bez decyzji. |
| P1-06 | Kanoniczne projects.json i migracja projektowych harmonogramów z dawnego rejestru. |
| P1-07 | Lokalna historia odpowiedzi oraz podgląd zapisanych wyników. |
| P1-08 | Techniczny handoff i wznowienie po rotacji, przyjęte przez właściciela. |
| P1-09 | Rozdzielenie Open Browser Lite i OpenFill. |
| P1-10 | Wykorzystywanie istniejącego Autosync bez ręcznego ingerowania w historię Git. |
| P1-11 | Podstawowe logowanie błędów, diagnostyka i pomiary zasobów. |
| P1-12 | Identyfikacja wydań oraz wersji aplikacji. |
| P1-01 | Nowy pulpit projektów, lista leadów, notatki, projekt i rozmowy, operacje HANDOFF oraz domyślny centralny czat. Funkcjonalne etapy v1.73.142, .146, .147, .148, .149 zaimplementowane i wstępnie sprawdzone. |
| P0-11, P1-04 | Niezależne projektowe harmonogramy przez UI/CLI i odrębne leady, wprowadzone w v1.73.142. Zakończono tę funkcjonalność; nie wymaga serii dodatkowych wybudzeń. |
| P1-02 | Istniejący pasek stanu, wersji i informacji o wybudzeniu, wprowadzony w UI. Nowe usprawnienia UX należy traktować jako osobną implementację. |
| P0, obl-5w3 | **ZAKOŃCZONE I ZAAKCEPTOWANE PRZEZ WŁAŚCICIELA 2026-11-03**, LIVE 1.73.266. NIEAKTUALNE: ciągłe wyszukiwanie kolejnych przeszkód w nadajniku. Nie wracać bez zgłoszonej, rzeczywistej usterki. |

## ROZPOCZĘTE: dokończyć brakującą implementację, nie testy
| ID | Następna rzeczywista funkcja lub poprawka |
|---|---|
| P0-02 | [ZAKOŃCZONE 2026-11-02: natychmiastowa aktywacja LIVE 1.73.186 PID 19944; zadanie wdrożeniowe działa cyklicznie co 1 minutę, bez bramki idle i bez automatycznego wyłączania. Poniższy opis pozostaje historycznym wymaganiem.] Wdrożyć nową wersję od razu, także podczas generowania lub innej aktywności starego procesu. Usunąć wdrożeniową bramkę idle. Pozostawić rzeczywisty rollback. |
| P0 | [ZAKONCZONE 2026-11-02: LIVE 1.73.187 PID23232, pelne instrukcje w NumberedMessage dla registeredOblLead; pozostaly tekst historyczny.] Rozszerzyć treść kolejnych komunikatów wybudzenia o pełną instrukcję uproszczenia i natychmiastowego wdrażania, zgodnie z najnowszym poleceniem właściciela. Sam dokument instrukcji nie jest wdrożeniem. |
| P0-11, P1-04 | [NIEAKTUALNE 2026-11-02: podstawowe niezależne harmonogramy są już ZAKOŃCZONE; otwarta może być tylko nowa implementacja szczegółowych slotów] Dokończyć niezależne harmonogramy i rzeczywistą obsługę wielu projektów. Każdy projekt steruje własnymi godzinami; nie projektować centralnej kolejki. |
| P1-01 | [NIEAKTUALNE 2026-11-02: pulpit i centralny czat już wdrożone; pozostaje tylko redukcja starych kontrolek] Zaprojektować i wdrożyć nowy, uproszczony interfejs aplikacji zamiast przemalowywania dotychczasowego. |
| P1-02, P1-05 | [CZĘŚCIOWO NIEAKTUALNE 2026-11-02: P1-02 status podstawowy ZAKOŃCZONY; dalszy UX to osobna implementacja P1-05] Pokazywać rzeczywisty status pracy i ostatniego wybudzenia oraz przyczynę braku wysyłki. Uprościć zbędne kontrolki. |
| P0-18 | Uporządkować zapisy odpowiedzi workera, wyłącznie jeśli istnieje rzeczywisty brak funkcjonalny; nie otwierać zadania tylko dla osobnej weryfikacji. |
| obl-tpm | Uprościć HANDOFF, gdy realne błędy zapisu utrudniają pracę; nie tworzyć niezależnego programu dodatkowych testów. |

## DO ZROBIENIA: funkcjonalności jeszcze nieukończone
| ID | Zakres |
|---|---|
| P1-13 | Dodawanie i konfiguracja kolejnych projektów z minimalnym udziałem właściciela. |
| P0-16 | Usunąć migające okna konsoli tylko jeśli źródło w OBL jest rzeczywistym, aktualnym błędem. |
| obl-08b, obl-ipa | Edycja i nadawanie natywnych tytułów rozmów ChatGPT, zgodnie z odrębną decyzją właściciela. |
| obl-890 | Przyszłe wykorzystanie natywnych projektów ChatGPT i plików projektu. |
| P2-01 | Opcjonalne wymienne modele API, bez zależności działania rdzenia. |
| P2-02 | Dodatkowa personalizacja UI dopiero po przebudowie podstawowego interfejsu. |
| P2-03 | Eksport zebranej diagnostyki, gdy będzie potrzebny użytkownikowi. |

## ZAWIESZONE DECYZJĄ WŁAŚCICIELA
Archiwizowanie rozmów ChatGPT: bez dalszych prac, analiz i planowania, do odrębnego jawnego polecenia. Zadania wstrzymane nie są aktywnym TODO.

## Organizacja pracy
Beads zawiera wykonawcze statusy. Zgłoszenia otwarte wyłącznie z powodu dawnych kryteriów dalszego sprawdzania należy oznaczyć DONE po potwierdzeniu, że funkcjonalność działa w LIVE. Jeżeli kod działa tylko w SOURCE/STAGED lub brakuje rzeczywistej funkcji, pozostawić zadanie otwarte z opisem brakującej implementacji. Zapisywać następny krok jako zmianę kodu lub wdrożenie, nie jako kolejny test.
