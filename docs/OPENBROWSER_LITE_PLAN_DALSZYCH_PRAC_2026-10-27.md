# Open Browser Lite — plan dalszych prac

Data: 27.10.2026. Host: wyłącznie SATANAMD. LC MultiHost target=satamad, obowiązkowe COMPUTERNAME. Status: plan, nie raport wykonania.
Dokument nadrzędny dla kolejności prac: docs/OBL_BACKLOG.md. Starsze plany są archiwalne.

## 0. Ustalenia i granice zakresu

**Notatki** oznaczają wyłącznie edytor w oknie Open Browser Lite. Aktualny tekst notatki przypisanej do danego czatu jest doklejany do każdego następnego rzeczywiście wysłanego komunikatu wybudzenia tego czatu. Edytowanie i zapis notatki nie przerywają trwającej pracy ani generowania. Notatka nie jest samodzielną wiadomością; pozostaje w edytorze po użyciu i w kolejnych wybudzeniach.

**Wykluczone:** osobna kolejka poleceń między czatami, przenoszenie wiadomości z innych rozmów ChatGPT, inbox projektowy, nowy system doręczania. Wcześniejsza propozycja międzyczatowego transportu wiadomości zostaje anulowana. Techniczny HANDOFF następcy jest jedynie przekazaniem obowiązkowego checkpointu projektu, nie transmisją rozmów ani notatek użytkownika. Notatka zostaje w swoim czacie; po zmianie leada notatki starego czatu są dostępne w jego archiwum, bez samodzielnego kopiowania do następcy.

**Archiwizowanie starych czatów** oznacza automatyczne przeniesienie poprzedniego leada po udanym przekazaniu roli: do archiwum w OBL i, po sprawdzeniu możliwości, również do natywnego Archiwum ChatGPT. Nigdy nie kasować rozmowy. Nie archiwizować wszystkich niepowiązanych z projektem rozmów użytkownika według arbitralnego wieku.

Wszystkie zmiany: SOURCE, testy, STAGED, LIVE na SATANAMD (Windows Session1), dowód PID/SHA/WebView2 i rollback. Nie wysyłać niepewnych wiadomości ponownie, nie uruchamiać dwóch leadów, nie używać ChatGPT Tasks ani PBOX1, nie robić ręcznie git commit/push/reset.

## P0-A. Przywrócić i zabezpieczyć wybudzanie

A1. Odtworzyć oś incydentu: ostatnie potwierdzone wybudzenie 26, brak potwierdzenia 27, serwerowe HTTP 429, assistant_not_complete i same_answer_already_woken. Rozdzielić fakty od hipotez.

A2. Zdefiniować i pokazywać autentyczny stan wykonawcy: RUNNING, WAITING, RATE_LIMITED, WAKE_BLOCKED, HANDOFF_REQUIRED, NEEDS_OWNER. Widoczne: ostatni prawdziwy ACK, czas bez postępu, następny termin i przyczyna blokady. Nigdy nie wyświetlać „Gotowe” tylko dlatego, że działa proces.

A3. Usunąć wielogodzinną pułapkę niezmienionej odpowiedzi bez duplikowania wcześniejszego wybudzenia. Nowa wysyłka dopiero po potwierdzeniu zakończenia nowej odpowiedzi lub po zweryfikowanej sukcesji; niepewny rezultat zatrzymuje wysyłkę, ale generuje diagnostykę i uruchamia bezpieczną ścieżkę naprawy.

A4. Utrzymać trwały cooldown HTTP 429 oraz watchdog, który nie restartuje sprawnego WebView2 z powodu zablokowanej odpowiedzi. Restartować tylko faktyczne awarie lokalnego procesu i nieodpowiadający renderer.

A5. Prawdziwe E2E: 100 prób, 24 godziny bez zbędnych restartów, żaden podwójny receipt, wykonane kolejne zadania, nie tylko zdarzenia due. Kryterium odbioru: autonomiczna praca mierzalna ACK i postępem.

## P0-B. Notatki, dokładnie jeden prosty mechanizm

B1. Formalnie przetestować działającą ścieżkę: edycja Notatki, autosave (obecnie 850 ms), zapis DPAPI, pobranie bieżącej treści podczas konstruowania wybudzenia, doklejenie do komunikatu i potwierdzenie receipt.

B2. Każde kolejne skuteczne wybudzenie dostaje pełną, aktualną notatkę, nawet niezmienioną. Nie usuwać jej, nie odznaczać jako dostarczonej. Wpis podczas generowania trafia dopiero do następnego komunikatu wybudzenia; nigdy nie zatrzymuje odpowiedzi.

B3. Zachować oddzielne notatki dla każdego czatu, szyfrowanie i trwałość po restarcie. Nie mieszać notatek między projektami ani podczas zmiany leada. Pokazywać błąd zapisu; nie udawać dołączenia niezapisanych danych.

B4. Limit bieżącej wysyłki 24 000 znaków musi być wyraźnie oznaczony w UI, bez usuwania nadmiaru z zapisanego notatnika. Testy: pusty i pełny tekst, zmiana podczas generowania, dwa różne czaty, restart, błąd zapisu, przekroczony limit, kilka kolejnych wybudzeń.

B5. Usunąć z zakresu nowy inbox międzyczatowy. Przyciski „Do kolejki” i „Kopiuj do czatu” ocenić w przeglądzie UX pod kątem usunięcia. Przed ewentualnym usunięciem mechanizmu starej kolejki sprawdzić, czy nie ma niezachowanych wpisów.

Kryterium odbioru: wystarcza wyłącznie okienko Notatki. Nie ma osobnej wiadomości, kolejki, przełączania czatu ani zakłócania pracy.

## P0-C. Automatyczna sukcesja i archiwizacja starych czatów

C1. Wcześnie rozpoznawać ryzyko limitu długości rozmowy, a po wystąpieniu limitu terminalnego rozpoczynać sprawdzoną procedurę zmiany wykonawcy.

C2. Utrwalić zwięzły checkpoint: cel, kontekst, decyzje, wyniki, status backlogu, aktywna wersja, blokady i następny krok. Sprawdzić SHA256.

C3. Utworzyć nowy czat z trwałym serwerowym identyfikatorem /c/UUID, wysłać techniczny handoff tylko raz; potwierdzić dokładną odpowiedź następcy i rzeczywistą zgodną wiadomość użytkownika dwoma niezależnymi odczytami DOM. Uwzględniać strukturę data-chatgpt-selection-message-id i data-message-author-role; nie uznawać tekstu użytkownika za odpowiedź asystenta.

C4. Po potwierdzeniu odczytu checkpointu atomowo wyłączyć harmonogram starego leada, przekazać rolę jednemu następcy, uruchomić jego wybudzenia i sprawdzić ciągłość. Przy UNCERTAIN brak retry i brak równoległego leada.

C5. **Archiwum OBL:** w tym samym potwierdzonym cyklu ustawić stary czat jako archive w rejestrze projektu, zapisać URL, datę i powód, przenieść do domyślnie zwiniętej sekcji „Archiwum”; bez utraty historii i notatek.

C6. **Archiwum ChatGPT:** osobno sprawdzić, czy przez istniejącą sesję można wykonać natywne „Archiwizuj” dla dokładnego identyfikatora rozmowy. Robić to dopiero po wyłączeniu wybudzeń starego leada; potwierdzać faktyczne przeniesienie. Brak wiarygodnego potwierdzenia => pending_archive, nie fikcyjny sukces i nie kasowanie.

C7. Trzy rzeczywiste scenariusze E2E: udana sukcesja, crash w połowie procesu, niepewna odpowiedź i ponowne uruchomienie. Plus test rollbacku EXE i brak drugiego lidera.

Kryterium odbioru: nowy czat sam kontynuuje zadania, stary ląduje w archiwum, bez utraty checkpointu i bez podwójnych wiadomości. Nie przekazywać pomiędzy nimi zwykłych wiadomości użytkownika.

## P0-D. Sprzątanie katalogów i trwała polityka porządków

Stan orientacyjny z audytu 27.10: diagnostics ~92 MB, releases w repo ~224 MB, src ~20 MB, tests ~21 MB, tools ~30 MB. Aktualny cleaner wskazał 1 stary odtwarzalny build, build-17329 (~5,96 MB), ale w trybie Audit niczego nie skasował. W drzewie Git liczne niezatwierdzone i nieśledzone pliki; absolutnie nie traktować ich jako śmieci.

D1. Raport przed sprzątaniem: rozmiar i liczba plików dla każdego katalogu, status Git, zależności, wiek, kopie i SHA. Każdy typ klasyfikować: zachowaj / zarchiwizuj / odtwarzalny / usuń po teście.

D2. diagnostics: usuwać tylko odtwarzalne build-* po 2 h od zakończenia, sprawdzeniu istniejącego wydania, testach odtworzenia i potwierdzeniu, że żadna aktywna wersja z niego nie korzysta. Dla screenshotów, prób, raportów i logów określić osobną politykę rotacji według wieku i limitu rozmiaru.

D3. releases: chronić LIVE, rollback i ostatnią sprawdzoną wersję stabilną. Starsze wersje pakować do kontrolowanego archiwum z manifestem i SHA; kasować duplikaty dopiero po audycie skryptów startowych. Nie zmieniać bez osobnej kontroli D:\Apps\OpenBrowserLite\releases.

D4. docs: utrzymywać mały zestaw aktywnych źródeł (OBL_BACKLOG, OBL_CONTEXT, CURRENT_STATUS, CHECKPOINT, ten plan). Historyczne audyty, stare roadmapy i pełne checkpointy do docs/history jako wersjonowane archiwum ZIP z weryfikacją SHA. Upewnić się, że żadna aktywna referencja nie zależy od przenoszonego pliku.

D5. src, scripts, tests, tools: identyfikować pliki .before-*, przestarzałe próbki, osierocone skrypty i wielokrotne kopie. Przed usunięciem: wyszukanie referencji, testy, backup. Nie ingerować w pracę Autosync i nie resetować dirty tree Git.

D6. Zdiagnozować i poprawić zadanie OBL Archive Retention SATANAMD: ostatni wynik 1, próba Audit zwróciła CURRENT_CHAT_ARCHIVE_NOT_FOUND. Brak katalogu odpowiedzi aktywnego czatu powinien kończyć się bezpiecznym NO_ARCHIVE_DATA, a nie niepotrzebną awarią. Nie mylić retencji cache lokalnych 12 tur z archiwizacją rozmów ChatGPT.

D7. Standard porządków: dry-run Audit => zatwierdzona allowlista i kopia/manifest SHA => Apply => raport bajtów przed/po => kontrola pojedynczego PID, WebView2, wybudzania i rollbacku. Buildy codziennie; kontrola logów i rozmiarów cyklicznie; historyczne dokumenty po każdym zakończonym etapie.

NIE usuwać profilu przeglądarki, cookies, DPAPI, notatek, ledgerów sukcesji, dziennika wybudzeń/ACK, checkpointów, aktywnego EXE, rollbacku, niezatwierdzonych zmian Git ani niezweryfikowanych plików.

## P1-E. Przemyślenie interfejsu od zera, również sensu jego elementów

E1. **Nie zaczynać od przemalowania obecnego UI.** Najpierw zdefiniować podstawowe scenariusze: nieobecny właściciel i autopraca, szybki podgląd stanu, dopisanie Notatki podczas generowania, zmiana projektu, ręczna pauza, wyjaśnienie awarii, odnalezienie archiwalnego czatu.

E2. Zinwentaryzować wszystkie widoczne i schowane elementy: panele, zakładki, menu, przyciski, statusy, komunikaty, popupy i ustawienia. Każdemu przypisać odpowiedzi: jaki cel realizuje, kiedy jest potrzebny, jak często, co się stanie po usunięciu i czym można go zastąpić.

E3. Dla KAŻDEGO elementu podjąć decyzję: pozostawić, połączyć z innym, zautomatyzować, przenieść do diagnostyki, usunąć. Nie zakładać, że funkcja ma istnieć tylko dlatego, że została kiedyś zaprogramowana. Zmierzyć realne użycie, jeżeli to potrzebne.

E4. Przygotować dwa naprawdę różne warianty organizacji aplikacji, nie dwie kolorystyki. Sprawdzić wariant z centralną rozmową, niewielką zwijaną listą projektów, łatwą do wywołania Notatką oraz małym paskiem prawdziwego statusu. Archiwum i zaawansowana diagnostyka na żądanie. Przycisk Do kolejki jest naturalnym kandydatem do usunięcia.

E5. Status ma pokazywać rzeczywiste wybudzenia i blokady, a nie tylko gotowość UI. W ciągu 2–3 sekund użytkownik ma wiedzieć: czy działa, co robi, kiedy następne wybudzenie, jaki ostatni rezultat i czy potrzebna interwencja.

E6. Prototyp i test na prawdziwym SATANAMD, minimum 1280x850 i 1920x1080. Zero uciętych kontrolek i przewijania głównego paska. Najwięcej miejsca dla rozmowy, szybka dostępność Notatek, możliwość ukrywania zbędnych paneli. Porównać liczbę działań w scenariuszach.

E7. Wdrożenie etapami z rollbackiem do poprzedniego UI, testami WebView2, CPU, RAM, DOM i sprawdzeniem, czy przełączanie paneli podczas generowania nie niszczy szkicu/odpowiedzi.

Kryterium odbioru: udokumentowany sens każdej pozostałej funkcji, usunięcie zbędnych elementów, zauważalne uproszczenie obsługi i więcej miejsca dla centralnej rozmowy. Sam efekt wizualny nie wystarcza.

## P1-F. Jakość, wydajność i dokumentacja

F1. Baseline CPU, RAM, liczby rendererów i rozmiarów cache; testy obciążeniowe 8h/24h przy kilku projektach.
F2. Ograniczenie DOM do ostatnich 10 tur; lokalnie 12 tur odpowiedzi w aktywnym czacie, bez utraty ważnych checkpointów.
F3. Każde wydanie: wersja, SHA, jeden PID w Session1, prawdziwy preflight, rezultat prób wybudzenia, sprawny rollback i log zmian.
F4. Aktywne checkpointy do 32 KiB, pełna historia w zweryfikowanych archiwach. Nie czytać starych planów automatycznie.
F5. Co etap: wykonane zadania, wyniki testów, zmiana stanu backlogu, zaoszczędzone miejsce, otwarte błędy i dokładny następny krok.

## Kolejność

1. Najpierw P0-A: wybudzenia muszą rzeczywiście działać.
2. Równolegle nieinwazyjny audyt D1 oraz naprawa awarii retencji D6.
3. P0-B: formalnie potwierdzić prosty mechanizm Notatek i usunąć zbędny zakres międzyczatowy.
4. P0-C: pełna sukcesja, potem automatyczne archiwizowanie starych czatów w OBL i ChatGPT.
5. Bezpieczne iteracyjne Apply dla P0-D: odtwarzalne artefakty i stare dokumenty, po testach.
6. P1-E: pełne przeprojektowanie UX od zera i wdrożenie etapami.
7. P1-F jest kryterium odbioru wszystkich etapów, nie zadaniem na końcu.

**Pierwsze konkretne zadania:** A1/A3, D6, B1, C3/C4/C6, E1/E2. W czasie aktywnego błędu wake nie zajmować się dekoracją UI.
