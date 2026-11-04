# Open Browser Lite: plan radykalnego uproszczenia, 2026-10-31

## Cel i reguła
Ciągła autonomiczna praca ChatGPT bez nadzoru użytkownika. Domyślnie usuwać mechanizmy, które odraczają wybudzenie albo blokują rotację. Zostawić tylko weryfikację właściwego czatu/trasowania, technicznie wykonalną wysyłkę, trwały stan i odzyskiwanie po błędach. Duplikat wybudzenia jest dopuszczalny, brak wybudzenia nie.

## A. Priorytet bezwzględny: komunikaty i realne wybudzenia
1. Zwykły wake: dokładnie [OB-WAKE-NNNNNN] + nowa linia + Kontynuuj pracę w swoim projekcie zgodnie z zasadami.
2. Wake nr ...9 oraz ręczny handoff: dokładnie [OB-WAKE-NNNNNN] + nowa linia + Zapisz aktualny handoff projektu zgodnie z zasadami.
3. Pierwszy wake nowego leada nr ...0: identyczny jak zwykły wake; bez dodatkowego seed, czasu, CLI, receipt, Beads i instrukcji migracji w wiadomości.
4. Czas faktycznego rozpoczęcia pracy zapisuje czat samodzielnie (z bieżącego zegara), nie przepisuje czasu silnika. Silnik zachowuje niezależną telemetrię due/send/ACK; receipt nie trafia do promptu.
5. Wszystkie zasady operacyjne są w AGENTS.md/operating rules, nie w komunikacie. Notatki i metadata nie są automatycznie doklejane do dwuwierszowego wake OBL.
6. Dowody: test jednostkowy dokładnej treści 208/209/210; prawdziwy izolowany WebView2 send i DOM ACK; naturalny live wake 208; następnie naturalne 209 handoff i 210 rotacja.

## B. Usuwanie ostrożnych bramek, po udowodnieniu A
1. LiteChatWakeUi: skatalogować DeferWakeWithoutSend, WakeDeferralMinutes, fingerprint/UNCERTAIN, dwuminutowe odroczenia oraz forced-wake preflight; usunąć domyślne opóźnienia, pozostawić skończoną techniczną próbę wybudzenia i routing na właściwy URL.
2. LiteChatHandoff: usunąć stałą AutonomousSuccessorReady=false, historyczne WAIT_USER i prewencyjne wstrzymanie projektowego wykonawcy; zabezpieczyć tylko atomiczną promocję jednego leada.
3. LiteWakeRotationUi: rotacja przy numerze ...0, jeden zapisany snapshot (engine-owned), nowy /c/ i kontynuacja tej samej pracy z checkpointu, bez blokady z powodu braku dodatkowego komentarza tekstowego.
4. Uporządkować recovery: jeden mechanizm odzyskiwania edytora/SPA/procesu, skończona liczba prób i ponowienie przy następnym terminie. Brak bezterminowego WAIT_USER dla błędu technicznego.
5. Uporządkować logowanie: krótkie faktyczne zdarzenia due/attempt/ACK/chat_started/chat_finished/rotation; usuwać zdublowane stany, nie usuwać dowodów awarii.

## C. Testy odbiorcze i wdrożenie
1. SOURCE build, 4 zestawy testów headless, izolowany WebView2 z widocznym wybudzeniem dokładnie dwuwierszowym, bez jawnego receipt, bez wielokrotnego send.
2. Potwierdzić hash staging/release, pojedynczy PID Session1, preflight generowania, rollback do uprzednio sprawdzonego EXE. Nie restartować procesu w trakcie wykonywania pracy.
3. Naturalny wake 208: rzeczywisty visible_ack (potwierdzony); 209: aktualny handoff; 210: nowa /c/, właściwy lead, nowe chat_started, przejęcie zadania Beads i checkpoint.
4. 24h ciągłego wykonywania realnych zadań, 8h jednej sesji DOM <=10, zero utkwienia, następnie 7 dni. Przy harmonogramie 15 minut maksymalnie 96 regularnych terminów/dobę; 100/24h nie jest sensownym wymogiem bez dodatkowych prób.
5. Kod redukować etapami. Nie zastępować prostych operacji nowymi warstwami kontrolnymi. Każdy pozostawiony gate dokumentować jednym konkretnym powodem.

## Stan wykonania 2026-10-31
SOURCE minimal wake/hand-off/seed zaimplementowano. Cztery zestawy headless EXIT0. Izolowany realny WebView2: passed=true, checks=234, offline=true, PID7292, UTC 15:44:10. Naturalny wake208: silnik submission_attempted i visible_ack UTC15:47:12. LIVE v1.73.78 PID16656, zmiana wcześniej wdrożona przez lead207; w kolejce v1.73.79 (odrębny hash), wymaga porównania z najnowszym SOURCE. Kroki B jeszcze nie wykonane. Nieweryfikowanej rotacji 209->210 nie deklarować jako sukces.
