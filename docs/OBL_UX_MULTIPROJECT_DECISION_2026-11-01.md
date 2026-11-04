# OBL: nowy interfejs i niezalezne harmonogramy projektow
Data decyzji: 2026-11-01. Wlasciciel zatwierdzil wykonanie bez dodatkowych pytan.

## Zasada 1: dzialajacego mechanizmu nie testujemy w kolko
Wybudzanie, rotacje i powrot do pracy traktujemy jako dzialajace.
Historia testow, pozostale dawne kryteria 30 min/24 h/7 d/100 wake, symulacje awarii
oraz stare statusy PARTIAL nie uzasadniaja nowych petli testowych.
Wracamy do tych obszarow WYŁĄCZNIE po konkretnej usterce w LIVE lub nowej decyzji wlasciciela.
Blad trzeba zdiagnozowac na podstawie rzeczywistego komunikatu i logow.
Nie oznaczamy niewykonanych testow jako zaliczone. Utrzymujemy rollback i minimalna
weryfikacje kazdego nowego wydania.

## Zasada 2: wiele niezaleznych projektow
Kazdy projekt ma identyfikator, jednego aktualnego leada i niezalezny harmonogram:
Enabled, interwal, due, sequence, historia dostarczen, notatki.
Projekt moze sam wlaczyc, wylaczyc i zmienic interwal przez UI albo CLI dla wlasnego
dokladnego identyfikatora. Praca w jednym projekcie nie zmienia innego projektu.
Nie wprowadzamy osobnej centralnej kolejki zadan miedzy projektami. Wspoldzielony worker
to opcjonalna technika oszczedzania zasobow, a nie wymaganie produktowe ani oddzielna
funkcja na ekranie. Aktualnych rozkladow nie zmieniamy automatycznie.

## Zasada 3: interfejs od nowa, nie kosmetyka
Cel: centralny czat i notatki; projekty po lewej / w osobnym pulpicie;
jedna aktualna karta projektu (lead, stan, termin, interwal) i czytelne przejscia.
Tryb dzienny i techniczny oddzielone. Informacje musza byc opisowe, nie
"Gotowe" bez dowodu. Wyrazne zmiany wymagaja swiadomego klikniecia.

### Rejestr decyzji dla obecnych kontrolek
| Element stary | Decyzja | Nowe miejsce |
|---|---|---|
| Wstecz, Dalej, Odswiez, ChatGPT | Zachowac | waski gorny pasek |
| Projekty / wybieranie projektu | Polaczyc i zaprojektowac od nowa | pulpit projektow |
| Dodaj + czat / nazwa projektu | Polaczyc | jeden formularz "Nowy projekt" |
| Przypnij biezacy czat | Zachowac | szczegoly projektu |
| Lead na kazdym wierszu | Usunac z listy | operacje zarzadzania leadem |
| Wl./Wyl. na kazdym czacie | Polaczyc | pojedynczy harmonogram aktualnego projektu |
| Popros o HANDOFF / Przejmij jako wykonawce | Ukryc | zaawansowane operacje projektu |
| Historia pracy / pliki / archiwum | Polaczyc | Historia / zakladka na zadanie |
| Przekazanie OBL | Ukryc | zaawansowane OBL |
| DOM, hard-prune, techniczna diagnostyka | Ukryc | zaawansowane ustawienia |
| Autopraca, wake status, odroczenia | Polaczyc | jedna czytelna sekcja "Wybudzenia" |
| Wersja, stan GUI, RAM, DOM | Ograniczyc | minimalny status + szczegoly techniczne |
| Notatki | Zachowac i uproscic | stale latwo dostepny panel |
| Ukryj panel / inspektor | Zmienic etykiete | "Notatki" / "Szczegoly" |
| Stare listy archiwalne | Ukryc | historia / zaawansowane |

### Etapy wdrozenia
1. SOURCE v1.73.142: niezalezna konfiguracja pierwszego harmonogramu przez
   --lite-project-wake every <15..1440>m <exact-project-key>; pierwszy nowy
   pulpit z lista projektow, leadem, interwalem, uruchamianiem/pauza, historia.
   Pulpit otwierany z istniejącego UI. Stary panel zachowany jako fallback.
2. Domyslnie centralny obszar rozmowy i zredukowana nawigacja; stary panel
   schowany do menu "Zaawansowane". Notatki stale dostepne.
3. Dodanie tworzenia/przypinania czatu, zmiany nazwy/leada oraz nawigacji
   projektowej bez odwolywania sie do starego panelu; wycofanie starego panelu
   dopiero po funkcjonalnej zgodnosci.
4. Dalsza redukcja kontrolek statusu i uproszczenie logow zdarzen w UI.
   Minimalne sprawdzenie nowego kodu; bez historycznych crash/soak.
Beads: obl-p1-01 (UX), obl-p0-11 (wiele projektow). Ten dokument jest
aktywnym rozstrzygnieciem sprzecznych starszych warunkow.

## Postep wdrozenia 2026-11-01 09:32 UTC
- v1.73.142 LIVE potwierdzony Release Manager STATE=trial: nowy LiteProjectDashboard,
  niezalezne ustawianie harmonogramu w UI i CLI, ekran dostepny z bocznego panelu.
- v1.73.143 SOURCE/STAGED/release QUEUED: domyslnie ukryty stary sidebar,
  nawigacja u gory ograniczona do Wstecz/Dalej/Odswiez/Home/Projekty/Panel/Wiecej,
  kontrole techniczne zebrane w zaawansowanych, stary panel mozna przywrocic.
  Nie deklarowac LIVE az Release Manager potwierdzi promocje.
- NA STEP 3: nowy pulpit musi jeszcze przejac tworzenie projektu, przypinanie
  aktualnego czatu, zmiane nazwy, zmiane leada, zarzadzanie notatkami i
  zwiezle stany. Dopiero wtedy usunac stara implementacje bocznego panelu.
- Dawne test-only Beads: status deferred, bez pozorowania PASS.

## 2026-11-01: etapa notatek, wydanie 1.73.146
Nowy pulpit: osobny, prosty przycisk Notatki dla aktywnego leada wybranego
projektu, bez nawigacji czatu. Ten sam klucz LiteChatNotes.NormalizeKey i
ten sam magazyn DPAPI, z ktorego korzysta istniejace wybudzanie. Edytor wymaga
jawnego Zapisz, sprawdza aktualnego leada, stan na dysku, weryfikuje odczyt
po zapisie i chroni niezapisane zmiany przy zamknieciu. Stary edytor ma
analogiczne sprawdzanie stanu na dysku, aby nie nadpisywac nowszej notatki.
Historia pracy jest w menu Zarzadzaj. Nie zmieniano implementacji wake.
Przeniesienie notatek nie oznacza jeszcze pelnego wycofania starego panelu.

## 2026-11-01: projekt i rozmowy, wydanie 1.73.147
Nowy pulpit oferuje jednoznaczne tworzenie pustego projektu albo projektu
z otwartym, nieprzypisanym czatem jako leadem. Zaznaczenie opcji jest jawne;
przy prostej nazwie projektu nie ma domyslnego przeniesienia otwartego czatu.
W trybie z leadem wykorzystana jest transakcyjna rejestracja w jednym pliku
projects.json; nowy parametr requireCurrentChat=true zabrania cichego
tworzenia pustego projektu, gdy czat zniknal lub w miedzyczasie zostal przypisany.
Nowy przycisk Rozmowy daje dostep do wszystkich czatow projektu, rowniez
archiwalnych, bez narzuconego limitu dwoch rozmow; pozwala je otwierac bez
zmiany aktywnego leada, harmonogramu i notatek. Stary panel nadal dostepny
w zaawansowanych do czasu przeniesienia operacji rotacji/handoff.

## 2026-11-01: operacje HANDOFF w nowym pulpicie, v1.73.148
Menu "Zarzadzaj" zawiera teraz podmenu "Rotacja i HANDOFF":
"Zlec HANDOFF" i "Przejmij otwarty czat po HANDOFF". Nowy
LiteProjectDashboard.Handoff.cs sprawdza projekt, leada, harmonogram i
wymaga potwierdzenia; przejecie jest mozliwe dopiero gdy istniejaca funkcja
LiteChatHandoff.CanTakeOver potwierdza oczekiwanie WAIT_USER, wstrzymanie
starego wykonawcy oraz odrebny prawidlowy URL nowego czatu.
Obie pozycje wywoluja DOKLADNIE stare procedury z LiteProjectsUi, teraz
adresowane przez jawny projectId. Nie duplikujemy logiki rotacji,
nie otwieramy nowego projektu ani nie zmieniamy innych harmonogramow.
Stan HandoffPhase jest widoczny w szczegolach projektu.
Stary panel jest nadal awaryjnym wariantem, do usuniecia po przegladzie
pozostalych kontraktow technicznych; nie wracamy do dawnych petli QA.

## 2026-11-01: uproszczenie glownego okna, wydanie v1.73.149
Zlikwidowano domyslne stale eksponowanie inspektora z prawej strony:
obszar ChatGPT wykorzystuje teraz cala szerokosc, a istniejacy inspektor
notatek bieżącego czatu i zdarzen mozna otworzyc w menu zaawansowanym.
Przycisk pokazywania/ukrywania panelu tez przeniesiony do menu zaawansowanego.
Wiersz glowny zachowuje podstawowa nawigacje, Projekty oraz menu techniczne;
_status, wersja, wskaznik wysokiego priorytetu i stan wybudzania pozostaja
w sekcji statusu. Stan wznawiania przeniesiono do narzedzi zaawansowanych.
Stary panel projektow nadal dostepny jako odwrotny wariant techniczny,
ale domyslnie ukryty. Dodatkowo jest zawsze tylko JEDEN modeless pulpit
projektow na okno OBL: kolejne klikniecie odswieza dane i aktywuje juz
istniejacy pulpit (bez tworzenia duplikatow). Bez zmian w mechanice wake
oraz danych projektow i notatek.
