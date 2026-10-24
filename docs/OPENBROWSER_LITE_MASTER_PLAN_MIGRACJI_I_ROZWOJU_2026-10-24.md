# OPEN BROWSER LITE. PLAN MIGRACJI I ROZWOJU
Data: 2026-10-24. Wersja planu: 2.1. Właściciel: użytkownik. Host docelowy: SATANAMD.
Status: kierunek i rozpoczęcie wdrażania zatwierdzone. Nie mylić planu z wykonaniem. Daty techniczne hostów w Europe/Warsaw (24.10 CEST), użytkownik w EAT (+1 godzina).
Istotne: wymagania właściciela z 24.10 mają pierwszeństwo przed starszymi roadmapami i ostrożnymi domyślnymi ustawieniami.

## 1. WYMAGANIA NADRZĘDNE
1. SATANAMD ma uruchamiać Open Browser Lite jako narzędzie do autonomicznej pracy asystenta. PBOX1 pozostaje maszyną operatorską/zapasową, bez jednoczesnego wybudzania tych samych czatów.
2. TWARDY LIMIT 10 tur/wiadomości w aktywnym DOM. Historyczne wiadomości nie muszą być dostępne przez przewijanie, nie przewidujemy UI do przeglądania starych tur. Nie wolno ucinać aktualnie generowanej odpowiedzi, kompozytora i kontrolek stanu.
3. Każda istotna odpowiedź modelu ma być ZAPISYWANA do trwałego lokalnego pliku UTF-8 Markdown/TXT, po oczyszczeniu z elementów interfejsu i formatowych śmieci. Kontekst odtwarza się z plików, a nie przez przewijanie coraz dłuższego czatu.
4. Zadania i odpowiedzi mogą trwać kilkadziesiąt minut. Co etap należy utrwalać checkpoint, także przed ryzykiem reload, restartu renderer lub rotacji chatu. Nie powtarzać bez sprawdzenia wcześniej zakończonych czynności.
5. Ocenić istniejące DPAPI, którego właściciel nie zlecał. Usunąć je z danych, dla których nie ma uzasadnienia, jeżeli utrudnia odczyt i migrację. Chronić mimo to cookies, hasła, tokeny i prywatne dane, których przypadkowe ujawnienie byłoby szkodliwe. Starych plików zaszyfrowanych nie kasować do czasu bezstratnego przejścia.
6. Docelowa skala: 5 do 10 aktywnych projektów, jeden lead per projekt, jeden foreground WebView2 oraz najwyżej jeden wspólny worker, bez osobnego browser renderer dla każdego zadania.
7. Priorytet: niezawodna praca, czyszczenie pamięci, odzysk po awarii, czytelny prosty UI, dopiero potem funkcje dodatkowe. Bez ChatGPT Tasks, niepotrzebnej chmury, nowych frameworków i płatnych usług.
8. Wszystkie funkcje mierzyć testami E2E. Testy kodu i status HTTP 200 nie dowodzą poprawnego działania widocznego ChatGPT.

## 2. STAN FAKTYCZNY 2026-10-24
| Parametr | PBOX1 | SATANAMD |
|---|---|---|
| Komputer | Lenovo laptop | ASRock stacjonarny |
| CPU | Intel i7-7600U 2C/4T | Ryzen 5 8500G 6C/12T |
| RAM | 16 GiB | 64 GiB |
| Dostępny RAM po zakończeniu Firefoksa | około 9,5 GiB | około 39,5 GiB |
| Windows | 10 Pro | 11 Pro |
| GPU | Intel HD 620 | AMD Radeon 740M |
| Lite na początku migracji | LIVE v1.72.76 PID 2404 | nieuruchomiony |
| Repo | stary checkout autosync/SATANAMD z 19.10 | SOURCE v1.72.83 |

PBOX1 LIVE 1.72.76 to ROLLBACK z kandydata 1.72.82 (24.10 10:52:17 CEST, reason=candidate_process_missing, poprzedni PID4232). Przyczyna zniknięcia procesu nadal nieudowodniona.
W ostatnim odczycie PBOX1 obecny był niezapisany szkic w ChatGPT, system raportował draft_present, brak generowania. Właściciel wyraźnie zgodził się na zatrzymanie Lite PBOX1.
SatanAMD: po zakończeniu 33 procesów Firefoksa commit zmalał o 34,02 GiB. Duży zapas pamięci jest wystarczający do pilotażu, nie usprawiedliwia braku limitu DOM.
SOURCE 1.72.83 ma wcześniejsze testy Node23/23, EXE8/8, publish 0 errors i manifest SHA256 38c7f29ca89ecf1c2f9a1b84e6b64d9f4d6e24040fe7e99d5e5096cc22e99320; brak E2E na SATANAMD.
Repo SATANAMD master ma stary HEAD i dużo staged/unstaged/untracked; Autosync działa na osobnej gałęzi autosync/SATANAMD. PBOX1 checkout jest przestarzały (7bb4525 z 19.10). Nie robić git clean/reset/rebase ani ręcznego pełnego commita.
Stary Open Browser zawiera PageTap i ConversationTrimmer ograniczający GET conversation oraz monitor odświeżania przy 100 turach. Lite NIE podłącza go, ma modernSoftEnabled=false dla DIL i LiteDomHardPrune enabled=false dla legacy. Ten brak produkcyjnego odchudzania trzeba usunąć.
Osiem godzin starego PBOX1 v1.72.21: 241 próbek, 7 WebView2 PIDs, suma private 561,9 do 1023,3 MiB. Wzrost udokumentowany, przyczyna nieudowodniona.
Problemy: renderer-unresponsive, response_load_failed mimo HTTP 200, timeout JS ExecuteScriptAsync, mylące Gotowe, znikająca odpowiedź po rehydration, niepełny handoff, luki audit journal i sprzeczny opis OB-WAKE-000016.

## 3. KANONICZNE ŚCIEŻKI
SATANAMD SOURCE: D:\projects\open-browser
SATANAMD KOD: D:\projects\open-browser\src\BridgeBrowserAlpha0
SATANAMD DZIENNIK: D:\projects\open-browser\docs\OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md
SATANAMD AUDYT: D:\projects\open-browser\docs\OPENBROWSER_LITE_AUDYT_STANU_2026-10-24.md
SATANAMD STARY PLAN: D:\projects\open-browser\docs\OPENBROWSER_LITE_EXECUTION_PLAN_2026-10-21.md
SATANAMD HANDOFF: D:\projects\open-browser\docs\OPENBROWSER_LITE_CHAT_HANDOFF_ROTATION_2026-10-23.md
SATANAMD FULL UI: D:\projects\open-browser\docs\OPENBROWSER_LITE_FULL_UI_AUDIT_2026-10-23.md
SATANAMD BAZA ZASOBÓW: D:\projects\open-browser\docs\OPENBROWSER_LITE_RESOURCE_BASELINE_2026-10-22.md
SATANAMD HISTORIA: D:\projects\open-browser\docs\design\openbrowser-master-roadmap-v3-2026-10-19.md
SATANAMD PLAN: D:\projects\open-browser\docs\OPENBROWSER_LITE_MASTER_PLAN_MIGRACJI_I_ROZWOJU_2026-10-24.md
PBOX1 KOPIA PLANU: C:\Users\pboru\Documents\Projects\open-browser-lite-source\docs\OPENBROWSER_LITE_MASTER_PLAN_MIGRACJI_I_ROZWOJU_2026-10-24.md

PBOX1 ROOT DANYCH: C:\Users\pboru\AppData\Local\OpenBrowser\ChatGPT-Lite
PBOX1 PROFILE: C:\Users\pboru\AppData\Local\OpenBrowser\ChatGPT-Lite\Profile
PBOX1 PROJECTS: C:\Users\pboru\AppData\Local\OpenBrowser\ChatGPT-Lite\projects.json
PBOX1 WAKE REGISTRY: C:\Users\pboru\AppData\Local\OpenBrowser\ChatGPT-Lite\chat-wakes.dpapi
PBOX1 LEGACY WAKE: C:\Users\pboru\AppData\Local\OpenBrowser\ChatGPT-Lite\dev-wake-settings.dpapi
PBOX1 NOTES: C:\Users\pboru\AppData\Local\OpenBrowser\ChatGPT-Lite\notes
PBOX1 ARCHIVE: C:\Users\pboru\AppData\Local\OpenBrowser\ChatGPT-Lite\archive
PBOX1 LEDGER: C:\Users\pboru\AppData\Local\OpenBrowser\ChatGPT-Lite\resume-ledger
PBOX1 OUTBOX: C:\Users\pboru\AppData\Local\OpenBrowser\ChatGPT-Lite\outbox
PBOX1 DIAGNOSTYKA: C:\Users\pboru\AppData\Local\OpenBrowser\ChatGPT-Lite\diagnostics
PBOX1 WAKE JOURNAL: C:\Users\pboru\AppData\Local\OpenBrowser\ChatGPT-Lite\diagnostics\wake-lifecycle.jsonl
PBOX1 RELEASING: C:\Users\pboru\AppData\Local\OpenBrowser\ChatGPT-Lite\diagnostics\lite-release-state.json
PBOX1 LIVE EXE PRZED MIGRACJĄ: C:\Users\pboru\AppData\Local\Programs\OpenBrowserChatGPTLite_v1.72.76_20261023_candidate\BridgeBrowserAlpha0.exe
PBOX1 HISTOR. SOURCE: C:\Users\pboru\Documents\Projects\open-browser-lite-source
SATANAMD DOCELOWE DANE (PLAN): %LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite
SATANAMD DOCELOWE RELEASES (PLAN): D:\Apps\OpenBrowserLite\releases\vX.Y.Z
SATANAMD ACTIVE SHORTCUT (PLAN): skrót do zatwierdzonego wersjonowanego EXE; nie uruchamiać z bin kompilacji.

## 4. SZYFROWANIE: OCENA I DECYZJA ARCHITEKTONICZNA
Obecnie harmonogramy chat-wakes.dpapi i notatki w notes oraz archiwum tekstowych odpowiedzi używają DPAPI CurrentUser. To standardowa ochrona danych konta Windows, ale wiąże deszyfrację z danym użytkownikiem oraz instalacją Windows i komplikuje przeniesienie na drugi komputer. Projekty i część logów już są jawnym JSON/JSONL.
Nie ma powodu, by rutynowy status techniczny, identyfikatory projektów, harmonogramy lub rezultaty pracy nad kodem były przymusowo zamykane w trudnym do odtworzenia formacie. Wygodna czytelność i łatwy HANDOFF są celem produktu.
Zachować zabezpieczenie sekretów i danych szczególnie poufnych. Nie eksportować haseł, cookies, tokenów, kodów 2FA i kluczy do jawnych plików.
Kierunek: schematy data/ zachować w JSON/JSONL/Markdown dla technicznego stanu i zwykłych odpowiedzi, z ACL do konta Windows. Prywatne notatki można oznaczyć jako SENSITIVE, opcjonalnie szyfrować osobno; domyślnie czytelny format lokalny zgodnie z preferencją właściciela. Dla przechwyconych odpowiedzi domyślnie tekst/Markdown, bez surowych danych z interfejsu i bez sekretnych credentiali.
Implementacja migratora: uruchomić w kontekście właściciela PBOX1, odczytać stare DPAPI, wygenerować kontrolowany zaszyfrowany transport (lub bezpieczne połączenie PBOX1-SATANAMD), zaimportować i walidować na SATANAMD. Nie umieszczać odszyfrowanego eksportu w repo/Git/logach/publicznym temp. Po bezstratnym przeniesieniu zdecydować, które stare DPAPI przechowywać jako backup. Nie kasować ich natychmiast.
Każdy plik wyjściowy ma być w chronionym lokalnym katalogu, nie w publicznym share lub cloud; przy archiwizacji/przesyłaniu na zewnątrz osobna ochrona.

## 5. AGRESYWNE DOM: KONTRAKT TECHNICZNY
Twardy, egzekwowany budżet: maksymalnie 10 najnowszych, zakończonych lub aktywnych tur widocznego chatu w głównej sekcji UI. W praktyce najnowsza generacja i edytor muszą zawsze pozostać nietknięte. Count ma dotyczyć właściwych kontenerów wiadomości, nie każdego zagnieżdżonego DIL component, a sygnał musi odpowiadać prawdziwemu ChatGPT.
Gdy pojedyncza odpowiedź składa się z wielu węzłów, usuwać lub ograniczać cały blok atomowo, a nie wewnętrzne dzieci. Warunek: nigdy nie modyfikować aktywnej generacji ani elementów composer/stop/busy.
Testować dwie drogi redukcji: (A) ograniczanie renderowanej historii na etapie GET conversation (rewizja oryginalnego ConversationTrimmer ze sprawdzeniem obecnego API/mapping/current_node), (B) twarde usuwanie DOM we współpracy z React/DIL, jeśli jest bezpieczne. Możliwe jest połączenie obu metod, ale żadnej nie deklarować działa bez realnego testu.
CSS content-visibility nie spełnia warunku 10 tur ani redukcji liczby węzłów DOM: może pomóc paint/layout, ale nie zastępuje głównej funkcji.
Pruning nie może uruchamiać auto reload w trakcie generowania lub szkicu, przestawiać rozmów ani nadpisywać odpowiedzi. Sprawdzić hydratację po SPA route, odświeżeniu, otwarciu innego czatu i powrocie. Jeśli React przywraca wycięte węzły bez końca, użyć mechanizmu danych wejściowych lub innego bezpiecznego sposobu.
Każda próba musi emitować metadane: liczba tur przed/po, liczba węzłów przed/po, timing, błędy, wersja DOM, renderer privateMiB, bez logowania prywatnej treści.
Gate produkcyjny: <=10 tur przez wielogodzinny test realnej strony, brak utraty edytora i aktywnej odpowiedzi, prawidłowy ACK, brak wzrostu pracy React przez cykliczną rekonstrukcję, 8h i 24h soak.

## 6. ARCHIWUM ODPOWIEDZI I ODTWARZANIE KONTEKSTU
Docelowy układ na SATANAMD: %LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\responses\<project-id>\<chat-id>\YYYY-MM-DD\.
Dla każdej odpowiedzi plik: YYYYMMDD_HHMMSS_UTC__wake-seq__receipt-prefix.md (dla odpowiedzi nienumerowanej odrębny stabilny message-ID/hash). Osobny manifest JSONL zawiera: projectId, chatId, realny URL, messageId, wakeSequence, receipt, capturedAtUtc, finishedAtUtc, hash SHA256, source/renderer state, status PARTIAL/FINAL, fileName, textChars i sanitizationVersion.
Treść: czysty Markdown/UTF-8. Usuwać: nawigację ChatGPT, systemowe etykiety, debug logi, zduplikowane formatowanie, przypadkowe tooltipy, przyciski, znaczniki widżetów renderera i nieistotne artefakty. Zachować merytoryczne akapity, polecenia, kod, tabele, ścieżki, linki, ważne cytaty oraz jawne wyniki narzędzi, jeśli stanowią treść odpowiedzi.
Zasada: sanitizacja nie może usunąć sensu, skrócić poprawnej odpowiedzi lub przeformatować komend. Test różnicowy oryginał/oczyszczony na kilkudziesięciu prawdziwych typach odpowiedzi, w tym DIL.
Zapis w trakcie generowania: okresowe snapshoty PARTIAL do pliku tymczasowego przez atomic rename, co najmniej przy zmianie treści i granicy checkpoint; FINAL po stabilizacji/końcu. Nigdy nie nadpisywać już potwierdzonej starszej wersji bez zapisu manifestu. Po restarcie wykrywać ostatni PARTIAL i kwalifikować jego stan, nie udawać FINAL.
Odtwarzanie kontekstu: chronologiczny indeks ostatnich odpowiedzi, streszczenie stanu projektu, dokumenty na dysku i last checkpoint. Na nowym czacie agent odczytuje odpowiedzi z plików zamiast przewijać ChatGPT.
Retencja odpowiedzi: długoterminowa, limity miejsca i polityka archiwizacji do zatwierdzenia; żadne automatyczne usuwanie starszych plików po 30 dniach bez zgody. Szyfrowany legacy archive pozostaje do migracji.
Nie oznaczać klientowego snapshotu jako dowodu serwerowego utrwalenia. Wyświetlenie odpowiedzi i model/chat_finished mają odrębne znaczenia.

## 7. PLAN MIGRACJI: JEDNOZNACZNE KROKI
M00: Odczyt stanu LIVE, logów i procesów obu hostów. Ustalić właściciela harmonogramu, realne URL/lead, aktywne generacje, szkice, ostatni ACK i UNCERTAIN. STOP, jeżeli nie można rozstrzygnąć ryzyka dubla.
M01: Wstrzymać PBOX1 Keepalive i stare automatyczne uruchamianie; następnie zamknąć tylko Lite PBOX1 za zgodą właściciela. NIE usuwać Profile, notes, ledger. Zrobić backup projects/chat-wakes i logów, z nazwą i SHA; sprawdzić task Disabled i brak procesu. Nie dotykać innych programów.
M02: Zdokumentować rollback 1.72.82->1.72.76 i przyczynę candidate_process_missing; skorelować lite-events, window_closing, WER i Windows startup. Nie blokować czystego stagingu SATANAMD samą nieznaną przyczyną, ale blokować powtórzenie wadliwej strategii restartu.
M03: Sprawdzić SATANAMD login Windows, WebView2 Evergreen, .NET 8, dostęp do ChatGPT, miejsce na dysku D:, %LOCALAPPDATA%, autostart/task i konflikt z oryginalnym Open Browser.
M04: Wykonać kompletny backup PBOX1 state+DPAPI+Profile+notes+archive+resume-ledger+outbox+diagnostics, source snapshot SATANAMD; sprawdzić SHA i odtwarzalność. Nie kopiować pracującego profilu bez snapshot.
M05: Ustalić konkretną wersję release i manifest, wykonać build/publish do wersjonowanego folderu SATANAMD poza aktywnym repo i oryginalnym Open Browser. Node suite, .NET Release, EXE8/8, hash gate.
M06: Uruchomić izolowany SATANAMD Lite z domyślnie WYŁĄCZONYMI harmonogramami. Sprawdzić rzeczywisty GUI, URL, zhydratowany DIL, composer i konto ChatGPT. Dopiero logowanie w prawdziwym profilu potwierdza możliwość pracy.
M07: Zbudować i wykonać jednorazowy migrator PBOX1->SATANAMD; przesłać dane i walidować 100% liczby projektów, chatId, lead, URL, note hashes, harmonogramów, wakeSequence, fingerprints, HandoffPhase, pending/uncertain i archiwum. Wszystkie porażki FAIL/STOP.
M08: Kontrolny test na osobnym czacie testowym SATA: jedna wiadomość z receipt, jeden ACK, jedna odpowiedź, trwały plik oczyszczonej odpowiedzi i checkpoint, prawidłowe statusy UI.
M09: Formalny cutover: PBOX1 OFF potwierdzone po 2 cyklach Keepalive / jednoznaczny task Disabled, dane importowane i zgodne, ownerEpoch SATA nowy, dopiero potem SATANAMD wybudzenia ON. Przed uruchomieniem ponownie potwierdzić, że brak drugiego aktywnego wykonawcy.
M10: Produkcyjne dwa realne wybudzenia po migracji: Open Browser Lite i Networking Radar, osobne receipt, właściwy URL, UI ACK, odpowiedź, zapis Markdown/manifest, checkpoint. Przy niepewnej wysyłce nie retry.
M11: Keepalive SATA: jeden proces w interaktywnej sesji Windows, kontrolowany relaunch po idle, test przycisku Pauza i zamknij (2 pełne cykle bez auto restart), brak drugiego lead.
M12: Test oszczędności DOM 10 tur i archiwizacji PARTIAL/FINAL na prawdziwej długiej rozmowie. Nie włączać hard prune do produkcji bez real QA React.
M13: 8h soak dla 2 projektów, wzrost do 3, 5 i 10 projektów, następnie 24h i 7 dni pracy bez dublowania, niekontrolowanego wzrostu RAM/CPU lub utraconego checkpointu.
M14: Zamknąć migrację, PBOX1 cold standby, zdatny do odtworzenia i wybudzenia WYŁ, oznaczenie SOURCE/STAGED/LIVE, raport i link do bieżącego statusu.
Bramki: G1 po M01, G2 po M04, G3 po M06, G4 po M08, G5 po M10, G6 po M13. Daty to nie dowód.

## 8. PLAN ROZWOJU: BACKLOG WYKONAWCZY
P0-A1: dokładna telemetria renderer vs ChatGPT delivery. Rozróżniać HTTP200/JS/React/pełny zapis, stale status, response_load_failed i WER. Bez błędnego Gotowe.
P0-A2: zapisywanie merytorycznych odpowiedzi do plików, sanitizer, atomic partial/final, indeks i odzyskiwanie po restarcie.
P0-A3: egzekwować 10 ostatnich tur, najlepiej przed renderowaniem po GET, a jeśli trzeba w DOM. Obsługa dynamicznego DIL, ochrony live generation/composer i anti-reconciliation loop.
P0-A4: brak restartowania przy szkicu/generowaniu/niepewnej wysyłce. Wysyłka po potwierdzeniu właściwego URL, niepewny ACK -> trwałe UNCERTAIN bez automatycznego ponowienia.
P0-A5: niezawodny single-host/single-lead ownership oraz dedup przy migracji, restartach i rollbackach; osierocone PowerShell na SATA diagnozować osobno.
P0-A6: 5-10 projektów na jednym wspólnym workerze, uczciwa kolejka i kontrola zasobów, naturalne E2E.
P1-B1: pełny automatyczny HANDOFF: wcześniej trigger, zapis pliku, niezależny test istnienia/hash, zamknięcie starego leada, nowy chat /c/ ID z prawdziwego UI, seed, nowy lead, rollback fencing. Minimum 3 testowe rotacje E2E.
P1-B2: przeprojektować interfejs: centralna rozmowa szeroka, panele projektów i notatek wąskie/schowane; statusy realnego modelu, ostatni ACK, kolejna godzina, WAIT_USER/BLOCKED. 1280x850 bez obciętych kontrolek.
P1-B3: prosty panel wyników projektu: ostatnie odpowiedzi Markdown, ostatnie checkpointy, ścieżki, błędy; wyszukiwanie po treści lokalnych plików bez rozrastania DOM.
P1-B4: ujednolicić status CURRENT_STATUS.md: timestamp, aktywny host, SOURCE/STAGED/LIVE, PID/EXE/SHA, ostatnie wake, migracja, checkpoint, next task. WORK_STATUS zostawić append-only.
P1-B5: release manager z izolowanym staging, manifest, selftests, watchdog, rollback zgodnym ze schematem, a nie restartem hosta podczas pracy.
P1-B6: odczyt/powiadomienia owner intervention po jednym powiadomieniu na nowy stan, bez twierdzenia że email został wysłany gdy nie był.
P2-C1: dopracowanie layoutu, motywów, personalizacji tylko po podstawowych testach.
P2-C2: rejestr wydań, eksport diagnostyki, tygodniowe statystyki zasobów, porządkowanie Git dirty tree jako osobny projekt.

## 9. REGRESJE I KRYTERIA AKCEPTACJI
- SOURCE: Node JS wszystkie testy, .NET publish zero błędów, EXE selftest 8/8, manifest SHA256; bezpieczna praca na właściwym host.
- Aplikacja: widoczny zalogowany ChatGPT, aktualny model, prawidłowy DIL, brak stale Gotowe.
- Dane: 100% projektów/notes/wake sequence/ledger zachowane; zero nieudokumentowanych zmian ID/lead.
- 10 tur: DOM nie przekracza maksymalnego budżetu w zakończonej historii po realnym generowaniu, przełączeniu i restarcie, bez naruszenia aktualnej odpowiedzi.
- Archiwum: każdy odpowiedni model-output ma oczyszczony UTF-8 plik, unikalny indeks, częściowy i końcowy status, SHA i możliwość ponownego odczytu oraz odbudowania kontekstu.
- Długi chat: 30-60 min generowania z checkpoint w pliku. Po przerwaniu praca kontynuuje od rzeczywistego last done, bez halucynowanych rezultatów.
- Wybudzenia: 100 kontrolowanych prób z poprawnym projektem/URL bez dubli, potem rzeczywiste 2/5/10 projektów i pełna obserwacja 8h/24h/7 dni.
- Zasoby: trend private WebView2 per PID, CPU, RAM commit, DOM nodes, latency; brak nieograniczonego wzrostu, progi liczbowe dobrać po baseline SATA.
- Pauza: brak wznowienia przez dwa cykle Keepalive; po wznowieniu dokładnie jedna instancja.
- UI: podstawowe informacje w 2-3 s, kontrolki dostępne przy 1280x850.
- Ostateczny sukces: 0 utraconych potwierdzonych checkpointów, 0 podwójnych lead, 0 automatycznego retry UNCERTAIN, PBOX1 jako sprawdzony cold standby.

## 10. ROLLBACK I ZASADY BEZPIECZEŃSTWA
Rollback wersji SATA: zatrzymać nowe wybudzenia, nie przerywać aktywnego modelu, przełączyć na poprzedni sprawdzony EXE/manifest w zgodnym schemacie, zachować dane i ledger.
Rollback hosta: SATANAMD wysyłki OFF oraz proces i scheduler zweryfikowane -> synchronizacja nowego ledger i stanów UNCERTAIN -> dopiero PBOX1 wysyłki ON; nie opierać się na starszym backupie bez ostatnich receipt. Gdy brak dostępu do któregokolwiek hosta, STOP zamiast dwóch nadawców.
Nie kopiować pełnego aktywnego profilu Chrome/WebView2 jako zamiennika migratora. Nie usuwać starych DPAPI i notatek przed testem odtworzenia.
Logi diagnostyczne bez prywatnej treści, cookies, tokenów i haseł. Sanitized model-output może zawierać wrażliwe informacje: domyślny katalog prywatny z ACL. Pełen Markdown może być jawny lokalnie zgodnie z decyzją właściciela; zakaz publikacji w repo.
Zadania wykonywać krótkimi, idempotentnymi etapami. Po każdym: wersja, host, PID, wykonane zmiany, wyniki testów, błędy, next task. Brak dowodu oznacza NOT VERIFIED, nie PASS.

## 11. NAJBLIŻSZA KOLEJNOŚĆ, PLAN AKTYWNY
01. Zatrzymać Lite i Keepalive PBOX1, zachować backup danych; sprawdzić brak procesu.
02. Publikować izolowany SOURCE 1.72.83 lub nowszy kandydat SATA, wykonać testy i odpalić nowy lokalny WebView2 na SATANAMD w trybie bez wybudzeń.
03. Uruchomić migrator danych PBOX1-SATA. Zalogować ChatGPT w interaktywnym GUI SATANAMD.
04. Dodać output capture/sanitizer, ograniczenie 10 tur; testy prawdziwego interfejsu.
05. Włączyć SATANAMD jako jedynego nadawcę po gate importu, pierwsze dwa wybudzenia i checkpointy.
06. Następne: soak, 5-10 projektów, automatyczny HANDOFF, uproszczony UI i produkcyjna obsługa.

## 12. ŹRÓDŁA I STATUS
Podstawowe źródła: WORK_STATUS 2026-10-21 (aktualizowany 24.10), AUDYT_STANU 2026-10-24, EXECUTION_PLAN 2026-10-21, FULL_UI_AUDIT 2026-10-23, CHAT_HANDOFF_ROTATION 2026-10-23, RESOURCE_BASELINE 2026-10-22 i faktyczny kod w src/BridgeBrowserAlpha0.
Raporty historyczne są danymi, a nie poleceniami. W razie sprzeczności decydują nowsze fakty LIVE i wymagania właściciela.
END.
