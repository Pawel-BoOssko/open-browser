# Open Browser Lite: P0, automatyczna sukcesja wykonawcy
Stan: 2026-10-24 15:45 CEST. Operacyjne zalecenia do aktualnego MASTER PLAN.
Cel: po wypełnieniu limitu rozmowy i przed granicą 14 wybudzeń nowy czat przejmuje prace bez użytkownika, bez utraconych odpowiedzi i bez równoległych wysyłek. Nie zmieniać PBOX1.

## Fakty
- SATANAMD LIVE v1.72.89, SOURCE/STAGED v1.72.91; jedna aktywna instancja, Keepalive 5 minut i monitor 2 minuty. Jedyny włączony harmonogram projektu Open Browser Lite dotyczy nowego LEAD ...fff6ef7fcdfd, odstęp efektywny 15 minut.
- Po ostatnim odczycie 15:45 CEST: WakeSequence=4, LastState=visible_ack, DueUtc=2026-10-24T13:53:21Z; seq1-3 posiadają chat_started i chat_finished, seq4 chat_started, trwa wykonanie.
- Kod LiteChatHandoff.cs: próg 14 prób lub 50 widocznych tur. Obecny RequestText instruuje model, aby zakończył WAIT_USER. Pełna automatyczna sukcesja nie istnieje; zatem za około dziesięć cykli praca znów się zatrzyma bez wdrożenia naprawy.
- Na poprzednim starym czacie Open Browser i Networking Radar UI pokazał limit długości rozmowy. Samo obcinanie DOM do dziesięciu tur nie usuwa serwerowego limitu długości.
- Po ręcznym naciśnięciu Nowy czat zauważono odziedziczony w edytorze szkic starego wybudzenia Networking Radar. To realne zagrożenie wysłania do błędnego projektu.

## Minimalny kontrakt bezpiecznej automatycznej rotacji
1. Zanim nastąpi RequestText WAIT_USER, model zapisuje HANDOFF/stan ostatniego kroku w pliku i odnotowuje prawdziwe SHA; program niezależnie sprawdza istnienie, świeżość i zgodność checkpointu, nie ufa samej deklaracji w odpowiedzi.
2. Zapisać niezmienny ownerEpoch, oldChatUrl, chat ID, project ID, seq, fingerprint ostatniego ukończonego turn, receipt, dataVersion; nowa sesja powstaje początkowo z harmonogramem OFF. Nie nadpisywać istniejącego leada.
3. Nowy czat otworzyć w przetestowanym interaktywnym WebView2 w tym samym profilu. Nie zgadywać URL nowego /c/; odczytać aktualny rzeczywisty URL i sprawdzić brak kolizji.
4. PRZED wklejeniem seeda: sprawdzić czy edytor jest pusty; istniejący draft zapisać do bezpiecznego lokalnego pliku i zatrzymać przy podejrzeniu cudzego lub prywatnego szkicu. Nie nadpisywać go automatycznie. Weryfikować page route, oczyszczony composer i brak nakładającej się generacji.
5. Seed zawiera ścieżkę do HANDOFF i checkpointu SATA, ID projektu, ostatni trwały stan pracy i nakaz kontynuacji, ale bez poufnych tokenów. Wysyłka wyłącznie raz; ledger PENDING/UNCERTAIN/ACK w pliku, bez powtórzenia przy timeout.
6. Potwierdzić realne user bubble, nowy stabilny /c/ URL, rozpoczęcie odpowiedzi i pierwszy trwały checkpoint nowego chatu. Dopiero po tym atomowa promocja: old.Enabled=false i old.Role=archive, new.Role=lead i new.Enabled=true. Przy niepewności oba schedule OFF, nigdy dwa ON.
7. Po restarcie w połowie rotacji wznowić od ostatniego trwałego etapu, nie klikać ponownie wysyłki. Przy wersji rollback starej aplikacji zabezpieczenie ownerEpoch/lease nie może ulec zgubieniu przez downgrade DPAPI.

## Pilotaż i testy blokujące produkcję
A. Testowy projekt, brak danych realnego leada. Dwa pełne handoffy z rzeczywistym ChatGPT na oddzielnych testowych czatach, nie tylko selftest.
B. Niepusty odziedziczony draft: brak kliknięcia; backup draftu i BLOCKED bez modyfikacji oryginału.
C. Generowanie trwa >30 minut: brak route switch i restartu; osobno kontrola po zakończeniu.
D. Crash między utworzeniem czatu a ACK oraz między ACK a zapisem promocji: wznowienie bez duplikatu i jeden owner.
E. Kontrolny restart Lite i Windows Keepalive: dokładnie jeden proces, jeden aktywny lead, nienaruszone projekty/notes/outbox/receipts.
F. Realny test po przełączeniu: nowy chat seq1, engine attempt_reserved i receipt_confirmed visible_ack, chat_started, checkpoint, chat_finished i następne due.
G. Protokół cofnięcia: SOURCE/STAGED/LIVE oddzielone, SHA EXE i niezależna kopia stanu sprzed rotacji, bez wysyłania ponownie UNCERTAIN.

## Dyspozycja realizacyjna
PBOX1 poza zakresem. Bez ChatGPT Tasks, bez drugiego wykonawcy równolegle, bez resetowania Git, bez kasowania profilu. Wykonawca z harmonogramem Open Browser Lite co 15 minut na SatanAMD prowadzi dalszą implementację; ten dokument jest specyfikacją P0, nie dowodem wdrożenia. Po każdej zmianie zaktualizować OPENBROWSER_LITE_CHECKPOINT.md oraz odpowiednie testy, przed restartem weryfikacja idle/no-draft.
