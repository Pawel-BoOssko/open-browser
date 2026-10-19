# Open Browser Lite — pełny plan ukończenia aplikacji v3.0

**Utworzono:** 2026-10-19, Europe/Warsaw. **Tryb pracy:** rozwój na SATANAMD, wydania PBOX1, bieżąca rozmowa Open Browser — rozwój aplikacji, techniczna synchronizacja Autosync. ŻADNYCH automatyzacji / przypominajek ChatGPT. **Zasady Git:** DO NOT COMMIT, bez ręcznego git push. Obecna kanoniczna ścieżka repo: `D:\projects\open-browser`. Dane konta/profil/sekrety nie opuszczają lokalnego profilu PBOX1.

## Cel i mechanizm nocny

Nadzór należy do **Open Browser Lite**. Co ~25 minut program obserwuje JEDEN przypięty czat (konkretne `https://chatgpt.com/c/{id}`) i tylko gdy ostatnia odpowiedź jest ukończona, model ma High, UI nie generuje, nie ma błędu ani niewysłanego tekstu, może wysłać JEDEN prosty komunikat:

> Hej, wróć do rozwoju Open Browser. Sprawdź własny plan i najnowszy checkpoint, samodzielnie wybierz następne zadania i je realizuj. Gdy zakończysz, zapisz postęp. Jeśli wszystko jest ukończone, nie twórz nowych zadań.

**Komunikat nie wybiera zadań, nie rozdziela pakietów, nie wysyła planu.** Czat odczytuje niniejszy plan i checkpoint ze źródeł przez LC MultiHost / RDC, wybiera zaległe punkty i realizuje następne. Nie obiecywać, że przeglądarka może wznowić niedostępny backend ani że 100% całego planu będzie gotowe do rana; raportować prawdziwy stan. Poza ChatGPT nie ma OpenAI ChatGPT Tasks.

Każdy krok ma stany `TODO | SOURCE | TEST | STAGED | LIVE_VERIFIED | BLOCKED`. Zatrzymanie wykonania w środku kroku => nowy checkpoint, nie powtarzać niepewnego wysłania. Nie wyłączaj systemowych zabezpieczeń, nie obchodź blokad przez inne narzędzia.

## Nadrzędny audyt i handoff — 2026-10-19 około 12:45 CEST

Przed wyborem nowych punktów **odczytaj** `docs/OPENBROWSER_DEEP_HANDOFF_2026-10-19_1245_ChatGPT.md` i `docs/OPENBROWSER_AUDIT_RUNLOG_2026-10-19_1220_ChatGPT.md`. Native ChatGPT hourly automations Open Browser OFF; nie tworzyć ani nie włączać. PBOX1 LIVE v1.72.0 PID5852, a SOURCE 1.72.4 wyłącznie SATANAMD; STAGED 1.72.4 ma EXE bez manifestu/gate. Nie ma potwierdzonego live `dev_wake_ack` — OB3-018 pozostaje otwarte. W 1.72.4 selected JS 16+10+3 PASS tylko według starszych checkpointów, testy manifestu/headless zablokowane, nie promować bez kwalifikacji. Sprzeczność polityki retry OWNER/implementacja patrz handoff; nie poprawiać jednostronnie. Przestarzałych dawnych oznaczeń LIVE/STAGED poniżej nie interpretować jako stanu bieżącego. Dalej realizować OB3 według dowodów SOURCE/TEST/STAGED/LIVE_VERIFIED, bez ręcznego commit/push i bez restartu rozmowy.

## Najnowszy checkpoint P0 — 2026-10-19 08:57 CEST
- **OB3-001..008/010..017: STAGED 1.72** — PBOX1 nowe wydanie `OpenBrowserChatGPTLite_v1.72.0_20261019_0847`, SHA256 EXE `DA6C2DD7B13A1B312EF2F459E7C38948F52ADE845A1A9103FC1A62C33B05159D`, manifest+gate PASS, 17/17 selftests PASS, izolowany PBOX1 WebView2 z realnym formularzem bez data-testid PASS; 15/15 zestawów JS PASS.
- **OB3-009/018: nadal PENDING REAL ACK** — 19.10 PBOX1 LIVE 1.71.0 PID21560 ma włączoną Autopracę 25m, 58 prób: 56 `busy`/niewysłane, 2 niepewne, 0 potwierdzonych `dev_wake_ack`. Nie zaliczać faktycznego wznowienia. 1.72 poprawia wybór faktycznego przycisku submit i zapisuje `dev_wake_delivery_phase`, nadal wymaga sprawdzenia po uruchomieniu LIVE.
- **OB3-017: promocja oczekująca bez restartu** — skróty Start i pulpit wskazują 1.72, proces pomocniczy PID19936 w interaktywnej sesji Windows 1 czeka wyłącznie na NATURALNE zamknięcie starego PID21560, potem uruchomi 1.72 na istniejącym profilu. Nie zabijać aktywnej rozmowy.
- Analiza starej wielokrotnej wysyłki: `docs/openbrowser-old-sender-audit-2026-10-19-0845.md`; checkpoint źródła/test/staged/live: `docs/openbrowser-checkpoint-2026-10-19-0857-v172.md`. Stara metoda w MainForm wysyłała bez receipts/ACK, jej generyczne klikanie SVG nie nadaje się do Lite. Nie uruchamiać surowego SSE `PageTap` z powodu obciążenia prywatności/RAM.

## Wykonanie P0 — checkpoint 2026-10-19 01:50 CEST
- 001–003: źródło i stan PBOX1 zinwentaryzowane. Dokładny URL tego czatu potwierdzony w prywatnej kopii historii WebView2 (NIE zapisany w repo).
- 004–014: kod v1.68.0 w Open Browser `LiteDevWakeState.cs` + `LiteDevWakeUi.cs`, wewnętrzny timer 60s/wybudzenie 25m, DPAPI, wybór wyłącznie jednego czatu, High, no draft/error/busy, preflight odpowiedzi i no duplicate. Testy stanu 10/10 PASS; runtime E2E pending.
- 015–016: PBOX1 STAGED 1.68.0 w `%LOCALAPPDATA%\\Programs\\OpenBrowserChatGPTLite_v1.68.0_20261019_0142`, manifest i hash PASS, 17/17 selftestów, izolowany prawdziwy WebView2 UI QA PASS. Nie jest to jeszcze potwierdzenie rzeczywistej wysyłki budzącej do serwera ChatGPT.
- 017: jednorazowy, techniczny helper PBOX1 `%LOCALAPPDATA%\\OpenBrowser\\ChatGPT-Lite\\diagnostics\\one-time-upgrade-168-when-idle.ps1` PID21276, Session1; czeka >=15 minut i >=10 minut braku aktywności, próbuje WYŁĄCZNIE graceful close starego PID13032 (v1.67.0), bez force i z uwzględnieniem blokady zapisu notatek. Dopiero po naturalnym wyjściu uruchomi nowy 1.68.0; log `dev-wake-rollover.log`. Nie jest to ChatGPT przypominajka ani usługa cykliczna. Przy niepowodzeniu helper wyjdzie i opisze blokadę.
- Konfiguracja na PBOX1 uzbrojona przez headless `--lite-dev-wake-arm`: DPAPI file istnieje, log `dev_wake_armed` confirmed, firstDue około 02:08 CEST. Aktywne okno o 01:46 było jeszcze v1.67.0; nie twierdzić, że autopraca już wysyła wiadomości. Sprawdź LIVE EXE, log `dev_wake_loaded`, `dev_wake_send_attempt`, `dev_wake_ack` po 25m przed oznaczeniem LIVE_VERIFIED.
- 018–020: do wykonania po uruchomieniu LIVE 1.68.0 i rzeczywistej pierwszej odpowiedzi. Pracuj nad pozostałymi punktami niezależnie w aktualnym czacie.
- Równolegle SOURCE 1.69.0: tylko odczytowy fallback do metadanych DIL w `LiteDomOptimizer.js`, test `lite-dom-dil-count.test.cjs` 3 PASS. Optymalizacja DIL pozostaje OFF do realnego QA, aby nie rozbić React.
- SOURCE 1.71.0, 03:23 CEST: OB3-009/011/031 w toku. Zakaz automatycznej ponownej wysyłki dla identycznej odpowiedzi po niepewnym dostarczeniu; logowanie przyczyn pominięcia `dev_wake_skipped` z limitem 10 minut; poprawka mieszanego DOM legacy+DIL w `LiteResumeProbe.js`. Testy JS 14 plików/17 runner PASS, DevWake 24 warunki PASS na Debug przed zmianą numeru wersji. PBOX1 LIVE nadal 1.70.0, UI: High, Autopraca WŁ. 25m, `answer_missing`, brak `dev_wake_ack`. SOURCE nie oznacza STAGED ani LIVE. Szczegóły i blokady: `docs/openbrowser-checkpoint-2026-10-19-0323-v171.md`.
- SOURCE 1.71.0, 04:21 CEST: OB3-007/018/031. PBOX1 LIVE 1.70.0 wykonał 6 prób `dev_wake_send_attempt`, każda kończy się `dev_wake_deferred_busy` (potwierdzone niewysłanie); `dev_wake_ack` nadal brak. Ujednolicono priorytet wyboru edytora w ResumeProbe/Sender, dodano 3 regresje (hidden textarea/draft, contenteditable fallback, missing editor), 14 plików JS/17 runner PASS, 0 FAIL. SATANAMD Autosync 04:21:03 potwierdził push 3260fa292102. Brak build/publish 1.71 po wcześniejszej odmowie; LIVE nietknięte. Checkpoint `docs/openbrowser-checkpoint-2026-10-19-0421-composer.md`.

## Faza 0 — P0: autowznowienie tego czatu

- [ ] OB3-001. Zabezpieczyć aktualne repo (dirty/staged) i identyfikatory aktywnej produkcji, bez resetu.
- [ ] OB3-002. Ustalić dokładny URL bieżącej rozmowy z lokalnej historii WebView2 i zweryfikować tytuł, bez kopiowania cookie.
- [ ] OB3-003. Ustalić prosty model trwałej konfiguracji `DevWake`: aktywacja, jeden URL, 25 min, timestampy, identyfikator ostatniej ocenionej odpowiedzi, próba z unikalnym receipt.
- [ ] OB3-004. Implementacja trwałego planowania bez Windows Task Scheduler i bez przypominajek OpenAI — timer tylko podczas działania aplikacji i aktywnej sesji.
- [ ] OB3-005. Implementacja jednego krótkiego, stabilnego komunikatu budzącego; nie przydziela zadań.
- [ ] OB3-006. Bramka URL dokładnie przypiętego czatu i modelu High; brak pracy w innych rozmowach.
- [ ] OB3-007. Bramka UI: brak generowania, niewysłanego szkicu, błędu transportu, brak odpowiedzi w toku; dwie kolejne próbki tej samej odpowiedzi.
- [ ] OB3-008. Trwałe zapisanie próby PRZED wysłaniem, unikalny receipt i brak powtórki dla niezmienionej odpowiedzi po restarcie.
- [ ] OB3-009. Potwierdzenie wysłania w DIL user bubble; jeśli wynik niepewny, brak automatycznego ponawiania.
- [ ] OB3-010. Limit na wiadomości: co najmniej 25 minut i limit dzienny, limit na jeden odpowiedź; kontrola obciążenia CPU i RAM.
- [ ] OB3-011. Logowanie zdarzeń: `dev_wake_armed`, `tick`, `skipped`, `submission_uncertain`, `ack`, `stopped`, bez treści rozmów i sekretów.
- [ ] OB3-012. UI: widoczny status i przełącznik `Autopraca: WŁ./WYŁ.`, dokładny czat, odstęp i data następnego sprawdzenia.
- [ ] OB3-013. Automatyczne przywracanie przypiętego URL po restarcie i stanu autopracy bez zagubienia tożsamości projektu.
- [ ] OB3-014. Test negatywny: obcy URL, brak High, user draft, streaming, renderer timeout, error, ponowny start, taka sama odpowiedź.
- [ ] OB3-015. Test integracyjny w izolowanym WebView2 na lokalnej stronie, bez prawdziwego konta.
- [ ] OB3-016. Side-by-side Release, manifest/hash/gate, 16+ selftestów na PBOX1.
- [ ] OB3-017. Kontrolowane przejście z działającego okna na nową wersję bez ucinania generowania i bez utraty danych.
- [x] OB3-018. LIVE_VERIFIED 2026-10-19: dwa rzeczywiste budzenia bieżącej rozmowy 14:45:23 i 15:10:55 (odstęp 25m32s), dwa wznowienia pracy oraz checkpointy. Bez zauważonych duplikatów w tych cyklach. Wewnętrzny ACK aplikacji nadal odrębny zakres OB3-009/025.
- [ ] OB3-019. Przetrwanie naturalnego restartu komputera; jeśli program nie uruchomiony, nie udawać wykonania.
- [ ] OB3-020. Autopraca nocna: po każdej odpowiedzi model sam czyta plan i checkpoint oraz realizuje zadania do wyczerpania backlogu; każde zatrzymanie jest dokumentowane.

## Faza 1 — diagnostyka i obserwowalność

- [ ] OB3-021. UI panel techniczny: build timestamp Warszawa, semver, Git SHA, kanał (SOURCE/TEST/STAGED/LIVE), WebView2 runtime i profil.
- [ ] OB3-022. CPU czasu rzeczywistego wszystkich procesów potomnych WebView2 i hosta, normalizacja do rdzenia/całości CPU.
- [ ] OB3-023. RAM private bytes, working set, commit, peak i dostępna RAM z trendem 5m/1h/24h.
- [ ] OB3-024. Rozróżnianie Chromium GPU/network/renderer/utility, sprawdzanie procesów sierocych, skalowanie interwału przy przeciążeniu.
- [ ] OB3-025. Multi-signal status fazy odpowiedzi: input→submitted→accepted→generating→tool→stable→final/blocked/error/unknown.
- [ ] OB3-026. Korelacja renderer freeze, dom probe, response transitions i HTTP bez fałszywego twierdzenia o backend delivery.
- [ ] OB3-027. Rejestrowanie źródła i czasu `delivery_timeout`, `response_load_failed`, przerwania, bez automatycznego Retry.
- [ ] OB3-028. Próbki fragmentów odpowiedzi do zaszyfrowanego archiwum (bez danych w Git, z retencją).
- [ ] OB3-029. Dashboard zdarzeń/profil czasowy, eksport raportu diagnostycznego dla właściciela.
- [ ] OB3-030. Soak dla 10k tur, CPU/RAM i WebView2 leak vs DOM/cache, nietolerowanie martwych rendererów.

## Faza 2 — wydajność DOM

- [ ] OB3-031. Zweryfikować aktualny DIL DOM na rzeczywistym, długim czacie; liczniki nie mogą być stale 0/0.
- [ ] OB3-032. Zidentyfikować granice wiadomości DIL i role, nie usuwać bieżącej odpowiedzi, edytora ani przycisków.
- [ ] OB3-033. Bezpieczny pomiar pamięci/renderu bez przycinania.
- [ ] OB3-034. Włączyć lekkie content-visibility oraz sprawdzić rzeczywiste oszczędności.
- [ ] OB3-035. Agresywne usuwanie starszych węzłów za feature flag z limitami 24/90, tylko po stabilnym zakończeniu.
- [ ] OB3-036. Fallback przy zmianach DOM i natychmiastowy rollback bez utraty możliwości wysłania.
- [ ] OB3-037. Testy realnej długiej rozmowy oraz porównanie CPU/RAM/kliknięć przed/po.

## Faza 3 — projekty ChatGPT i organizacja

- [ ] OB3-038. Jasno oddzielić **prawdziwe Projekty ChatGPT** od lokalnych aliasów/panelu Open Browser.
- [ ] OB3-039. Inwentaryzować prawdziwe projekty/foldery ChatGPT i rozmowy bez zmian dla właściciela.
- [ ] OB3-040. Każdemu repo/produktowi (Open Browser, Lupus Commander, OpenFill, Networking Radar, Transkrypcja AI, Lupus Commander MultiHost, CourtControl i nowe) przypisać docelowy projekt ChatGPT.
- [ ] OB3-041. Bezpieczny proces przenoszenia czatów do prawdziwych projektów ChatGPT po kontroli uprawnień; brak nieistniejącego API.
- [ ] OB3-042. Panel pokazuje obie warstwy i rozbieżności, zamiast udawać, że lokalna karta = projekt ChatGPT.
- [ ] OB3-043. Projekty bez sztucznego limitu liczby, nazwy, kolory, aliasy i migracje schematu.
- [ ] OB3-044. Lista czatów per projekt (lead/worker/test), szybki link i zmiana prowadzącego.
- [ ] OB3-045. Stany pracy, zadania, blokady, postępy, ostatni checkpoint, logi i metryki per chat.

## Faza 4 — interfejs operatora

- [ ] OB3-046. UX panelu projektów i czytelne karty z małą liczbą kliknięć.
- [ ] OB3-047. Przypięta karta dla każdej pracującej rozmowy, adaptacyjny warm pool do 2.
- [ ] OB3-048. Pamięciooszczędne usypianie wyłącznie nieaktywnych kart bez utraty szkiców.
- [ ] OB3-049. Panel notatek per chat — zapis szyfrowany i podgląd.
- [ ] OB3-050. Dyktowanie Win+H i mikrofon zarówno w notatkach, jak i ChatGPT WebView2.
- [ ] OB3-051. Kopiowanie notatek, queued/outbox/confirmed, przekazywanie raz „Informacje od Pawła:”.
- [ ] OB3-052. UI status pracy: ikona 16px, tooltip, progress i ikona na pasku zadań.
- [ ] OB3-053. Log techniczny/diagnostyka ze szczegółami nieudanych komend.
- [ ] OB3-054. Obsługa klawiatury, dostępności, dark/light i rozdzielczości 100–200%.

## Faza 5 — autonomiczny nadzór wielu czatów

- [ ] OB3-055. Nadzorca dowolnego allowlisted czatu, nigdy auto na wszystkich rozmowach.
- [ ] OB3-056. Luna analizuje odpowiedź i blokery; nie wymyśla nowych zadań.
- [ ] OB3-057. Konfigurowalna strategia modeli High/Medium tylko świadomie zatwierdzona; brak degradacji jakości.
- [ ] OB3-058. Krótkie komunikaty kontynuacji bez sztucznego poganiania.
- [ ] OB3-059. Trwałe lease/fencing, atomic receipt, odporność na crash/restart.
- [ ] OB3-060. Zatrzymanie na pytań wymagających właściciela i na nieznanym stanie wysyłki.
- [ ] OB3-061. Zmiana czatu głównego/handoff; nowy czat czyta plik, starsze mogą odejść bez utraty postępu.
- [ ] OB3-062. Deduplicated notifications, schematy wydatków API i limitów modeli.
- [ ] OB3-063. Weryfikacja na czacie testowym książki: naturalne stop, błąd, High, completed, no duplicate.

## Faza 6 — MCP i integracje

- [ ] OB3-064. Własny serwer MCP Open Browser: register_chat, heartbeat, status, checkpoint, handoff, blocker, notes ack.
- [ ] OB3-065. Kontrola uwierzytelnienia i sesji; separacja dwóch komputerów i czatów.
- [ ] OB3-066. Panel dodawania/weryfikacji MCP bez kopiowania sekretów do promptów.
- [ ] OB3-067. LC MultiHost podstawowym kanałem zdalnego wykonania, RDC do GUI/plików, awarie szczegółowo logowane.
- [ ] OB3-068. Luna/Eve i inne modele jako strategie API zgodne z faktycznym katalogiem modeli.
- [ ] OB3-069. Wykorzystanie Autosync bez ręcznego push, release gates i zaznaczenie kanału.
- [ ] OB3-070. Integracje Drive/GitHub/Outlook w ramach nadanych uprawnień bez działania ukrytych automatyzacji ChatGPT.

## Faza 7 — wytrzymałość i odtwarzanie

- [ ] OB3-071. Szyfrowany checkpoint każdej długiej odpowiedzi, także fragmenty wyświetlone przed błędem.
- [ ] OB3-072. Rozpoznawanie „odpowiedź zniknęła” przez lokalny zapis i stan UI.
- [ ] OB3-073. Pełny rejestr akcji i idempotentny retry tylko po świadomej decyzji użytkownika.
- [ ] OB3-074. Multi-device SATANAMD / PBOX1, internet offline, zarządzanie profilami i bezpieczny backoff.
- [ ] OB3-075. Testy restartu programu/Windows, samoczynnego logowania, recovery DPAPI.
- [ ] OB3-076. Backup/retencja/eksport/import bez cookie, tokenów ani historii bez zgody.
- [ ] OB3-077. Crash-safe raporty i nieudane polecenia, odporność na uszkodzony plik.

## Faza 8 — wydania i zakończenie

- [ ] OB3-078. Jednolite wersje i manifest z timestamp/hash/sourceCommit/kanałem STAGED lub LIVE.
- [ ] OB3-079. Automatycznie wykonywane selftesty JS/.NET/realny WebView2 w izolacji.
- [ ] OB3-080. Kontrolowany updater side-by-side bez przerywania w trakcie generowania; rollback.
- [ ] OB3-081. Poprawa starego zadania Harmonogramu Windows OB-10, osobno od ChatGPT Tasks.
- [ ] OB3-082. QA 16px, sesja interaktywna, stany błędu, przyciski „High”, długi chat.
- [ ] OB3-083. Dokumentacja operatora, instalacja, konfiguracja monitoringu i wznawiania.
- [ ] OB3-084. Końcowy soak / nocny test autonomicznej pracy z checkpointami.
- [ ] OB3-085. Sprawdzić WSZYSTKIE zgłoszone historyczne wymagania i blokady z rejestrem, przenieść brakujące do planu.
- [ ] OB3-086. Każdy punkt ma dowód LIVE_VERIFIED, żadnych pominiętych testów, plan zamknięty dopiero po finalnym audycie.

## Reguła kolejnej odpowiedzi po przebudzeniu
1. Otwórz ten plan i ostatni checkpoint przez LC MultiHost (preferowany) lub RDC. Nie zakładaj, że pamiętasz ostatnie wykonanie.
2. Zweryfikuj aktywny proces/wersję, repo i wolne zasoby, sprawdź konflikty innych agentów.
3. Sam wybierz najwyższe nierozwiązane punkty i zrealizuj kolejny sensowny pakiet. Nie czekaj na nowe zadanie od nadzorcy.
4. Po każdej zmianie zapisz zwięzły checkpoint i zweryfikuj testy; rozszerzaj plan tylko o uzasadnione zależności.
5. Nie uruchamiaj automatyzacji ChatGPT, nie rób commita/ręcznego push; użyj istniejącego Autosync.
6. Gdy pełny plan jest wykonany i LIVE zweryfikowany, poinformuj użytkownika, nie wysyłaj kolejnych poleceń pracy.

## Historia
- Źródłowy backlog 35 punktów: `docs/design/openbrowser-full-delivery-plan-v2-2026-10-18-1626.md`.
- Checkpoint v1.67: `docs/openbrowser-p0-checkpoint-2026-10-18-2305.md`.
- Aktualna decyzja operatora 19.10: włącz wyłącznie własny nadzorca Open Browser dla tej rozmowy, ~25m, bez sztucznego doboru zadań i bez przypominajek ChatGPT.

## Aktualizacja 2026-10-19 05:20 CEST: OB3-031

SOURCE 1.71.0: poprawiono licznik mieszanego DOM legacy+DIL, preferując rzeczywiste wiadomości DIL. Nie włączono optymalizacji ani przycinania DIL. Nowy test mixed: 90 DIL i 3 legacy; wszystkie 17 plików testów JS PASS. LIVE PBOX1 pozostaje 1.70.0, bez potwierdzonego dev_wake_ack. Szczegóły w `docs/openbrowser-checkpoint-2026-10-19-0519-diagnostics.md` i rejestrze nieudanych komend.

## Aktualizacja 2026-10-19 06:17 CEST: OB3-031/032

SOURCE v1.71.0: liczniki ról DIL w produkcyjnie ładowanym `LiteDomOptimizer.js` (`dilAssistantCount`, `dilUserCount`), wyłącznie odczyt DOM. Osobny testowy `LiteDomSelectorAudit.js` obsługuje DIL/mixed i deduplikację. Testy Node: 18/18 PASS, 0 FAIL. LIVE PBOX1 nadal v1.70.0, PID 8704; nadzorca do 06:11:51 CEST odmawia `busy`, `dev_wake_ack` niepotwierdzony. Wydanie i realny QA nadal TODO. Szczegóły: `docs/openbrowser-checkpoint-2026-10-19-0617-dom-roles.md`.

## Aktualizacja 2026-10-19 08:08 CEST: OB3-009/073, pasywny late ACK

- [DONE-SOURCE] v1.71.1: CanReconcileReceipt/ConfirmVisibleReceipt w LiteDevWakeState oraz pasywny odczyt DIL user bubble w LiteDevWakeUi. Wykrywa spóźniony ACK po niepewnej wysyłce bez ponowienia; nie znosi blokady tego samego hash odpowiedzi.
- [DONE-TEST] SATANAMD: Node 18/18 plików PASS; Debug build 0 errors; dev-wake selftest 30 checks EXIT 0; Release publish, 9 bramek JS, manifest, 5 headless selftestów PASS.
- [STAGED] SATANAMD: %LOCALAPPDATA%\OpenBrowser\staged\OpenBrowserLite_v1.71.1_20261019_0805. SHA256 5DD94EA237F049DF86BD46397B6FB7706F9B2A481031D3C042ADFB3DBAF79738.
- [LIVE-VERIFIED] PBOX1 v1.71.0 PID 21560, Responding=True od 07:44:50. O 07:57:56 dev_wake_submission_uncertain no_visible_ack, brak dowodu skutecznego budzenia. Nie restartowano, nie ponawiano.
- [BLOCKED] Izolowany test WebView2 oraz późniejszy odczyt kontrolny PBOX1 odrzucone przez safety checks LC MultiHost. Szczegóły w rejestrze i checkpointcie.
- [NEXT] Po Autosync PBOX1 STAGED 1.71.1, izolowany WebView2 QA, kontrolowany upgrade, potwierdzenie realnego ACK i no-duplicate. Nie zamykać OB3-009/018/073 jako LIVE_VERIFIED przed dowodami.
- Checkpoint: docs/openbrowser-checkpoint-2026-10-19-0808-late-ack.md.

## Aktualizacja 2026-10-19 09:18 CEST: OB3-009/011/025
- SOURCE 1.72.1: obserwator DOM zwraca liczbę węzłów asystenta/użytkownika i długość ostatniej odpowiedzi, bez nowych treści; nadzorca zapisuje throttled `dev_wake_probe_diagnostic` dla `answer_missing`/`high_not_verified`/`not_idle`. Wysyłka i idempotencja bez zmian.
- TEST: Node 15/15 suites PASS, 4 PASS dla mixed probe, .NET 17/17 selftest PASS, Debug build 0 errors, 1 historyczne MSB3277.
- STAGED SATANAMD 1.72.1: `OpenBrowserLite_v1.72.1_20261019_0917`, manifest/hash i release gate PASS. Izolowany WebView2 blocked (rejestr); PBOX1 LIVE nadal 1.72.0 PID5852, `answer_missing` o 09:15, brak potwierdzonego ACK. Checkpoint: `docs/openbrowser-checkpoint-2026-10-19-0918-probe-metadata.md`.
- NEXT: po Autosync staged PBOX1 1.72.1, realne odczyty diagnostyki, następnie rzeczywiste DIL ACK. Nie oznaczać OB3-009/018 jako ukończonych.

## Aktualizacja 2026-10-19 14:56 CEST: OB3-012/018/061/081

- **OB3-018, PARTIAL LIVE:** nowy czat projektu (ID suffix `b74f0fc1f389`) otrzymał rzeczywistą pobudkę z PBOX1 o 14:45:23, co wznowiło pracę modelu. Drugi cykl 25m dopiero przed nami, nie zamykać punktu.
- **OB3-012/061, SOURCE+TEST+LIVE UI:** 1.72.6.0 dodała operatorowi `Przypnij ten czat`, potwierdzenie zmiany rozmowy, aktualizację szyfrowanej konfiguracji, reset ledgeru poprzedniej rozmowy, widoczny suffix przypiętej rozmowy i czas następnego wybudzenia. Testy DevWake 33 checks PASS, Node promotion 9/9 PASS, wdrożenie LIVE PID21332 14:52:56 z bezpiecznym fallbackiem, kontrola UIAutomation przycisku+statusu PASS. Rzeczywista interakcja zmieniająca czat w GUI wciąż do sprawdzenia.
- **OB3-081, BLOCKED:** próba aktualizacji istniejącego zadania Windows `OpenBrowser ChatGPT Lite` wskazującego starą wersję telemetryczną: `Set-ScheduledTask` odmówiło dostępu HRESULT 0x80070005, nie powtarzano. Skróty pulpit/menu Start już wskazują 1.72.6; zadanie systemowe nadal stare. Log w `docs/Rejestr nieudanych komend.md`.
- **Checkpoint:** `docs/openbrowser-checkpoint-2026-10-19-1451-handoff-ui.md`. Nie używano ChatGPT Tasks, ręcznego commit/push, reset/clean.

## Aktualizacja 2026-10-19 15:04 CEST: OB3-022/023

- **SOURCE + LIVE SCRIPT HOTPATCH:** naprawiono wykrywanie uruchomienia `--lite-open-safely` w `scripts/monitor-lite-resources.ps1` i lokalnym monitorze PBOX1. Poprzednio realny PID21332 był raportowany jako 0 hostów; od 15:00 hostCount=1 i 7 procesów WebView2.
- **LIVE VERIFIED MONITOR:** zaplanowana, istniejąca próbka Windows Task 15:01:02 również wykryła hostCount=1. Dodatkowe obliczanie `cpuCorePercent` i `cpuMachinePercent` z różnicy skumulowanego czasu CPU (ten sam PID, znane okno pomiarowe) działa w próbce 15:03:55: 4 procesory logiczne, 51,9 s okna, host 0,1% jednego rdzenia, 0,02% całej maszyny. Historia sprzed poprawki nie została przepisana.
- **PARTIAL:** dopiero początek OB3-022/023, bez długich trendów i GUI dashboardu. Szczegóły `docs/openbrowser-checkpoint-2026-10-19-1504-resources.md`. Wydanie przeglądarki nadal LIVE 1.72.6, bez restartu.

## Aktualizacja 2026-10-19 15:07 CEST: OB3-024

- **SOURCE + LIVE monitor:** klasyfikacja 7 procesów WebView2 działa: browser 1, crashpad-handler 1, gpu-process 1, utility 2, renderer 2. Dodano wyłącznie odczytowe wykrywanie kandydatów na procesy osierocone według niezwiązanego parentPid (bez automatycznego zamykania).
- **PBOX1 15:07:17:** host 1, WebView2 7, orphan candidates 0, pomiar CPU 11,5 s. AST PowerShell 0 błędów.
- **Częściowo wykonane:** OB3-024, wciąż brak testów awarii renderera. Checkpoint `docs/openbrowser-checkpoint-2026-10-19-1504-resources.md`.

## Aktualizacja 2026-10-19 15:20 CEST: OB3-018, OB3-021 i OB3-023

- **OB3-018: LIVE_VERIFIED.** Dwa kolejne wybudzenia 14:45:23 i 15:10:55 w prawidłowej rozmowie, odstęp 25 min 32 s, faktyczne wznowienia pracy i zapis checkpointów. Użytkownik dostarczył drugi komunikat. Wewnętrzne potwierdzanie ACK w nowej ścieżce wysyłania nadal osobnym zadaniem OB3-009/025.
- **OB3-021: SOURCE+STAGED+LIVE UI, częściowo.** Wersja 1.72.7 w PBOX1 PID20956 od 15:15:28. Nowy przycisk `Techniczne` widoczny w rzeczywistym pasku, manifest LIVE ma source SHA `671ed94dac64183307bd73cc99bed4ed5c0aec8e`, kanał live i poprawny hash EXE. Tekst okna po kliknięciu nie został potwierdzony przez automatyczny test GUI.
- **OB3-023: SOURCE+TEST (rozszerzenie analizy).** Analizator trendów rozróżnia surową zmianę RAM między restartami od zmiany w nieprzerwanym okresie z identycznymi PID-ami procesów. Wprowadzono 9 testów regresyjnych PASS; prawdziwe próbki PBOX1 do weryfikacji. Nie oznaczamy pełnego 5m/1h/24h dashboardu jako ukończonego.
- Checkpoint: `docs/openbrowser-checkpoint-2026-10-19-1512-second-wake.md`. Bez przypominajek ChatGPT, ręcznych commitów i pushy.

## Aktualizacja 2026-10-19 15:26 CEST: OB3-023/029

- **OB3-023 (PARTIAL):** analizator wspiera teraz osobne okna `5m`, `1h`, `24h`, każde z faktycznym czasem pokrycia, próbkami, minimum dostępnego RAM, maksimum pamięci prywatnej hosta/WebView2 i szczytem CPU. Zachowuje osobny wskaźnik nieprzerwanej serii PID. 16 testów PASS.
- **OB3-029 (PARTIAL):** lokalna, statyczna strona HTML z trzema oknami i stanem stabilności procesów wygenerowana oraz zweryfikowana na PBOX1: `%LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\diagnostics\resources-dashboard.html`. Brak zewnętrznego JavaScript lub eksportu treści rozmów. Nadal brak UI w samej aplikacji i osi zdarzeń diagnostycznych.
- **Rzeczywiste dane PBOX1:** okno 5m: 3 próbki/4m, 1h: 33/58m, 24h: 404/800m, wyraźnie oznaczone niepełne 24h; 24h peak private WebView2 1103,7 MiB. Weryfikacja `docs/openbrowser-checkpoint-2026-10-19-1512-second-wake.md`.

## Aktualizacja 2026-10-19 15:31 CEST: OB3-029

- **SOURCE + LIVE SCRIPT:** dashboard zasobów wzbogacono o ostatnie 25 istotnych zdarzeń z `lite-events-YYYY-MM-DD.jsonl`, wyłącznie godzina, nazwa zdarzenia i PID. Bez `details`, rozmów, URL, cookies ani zdalnego JavaScript.
- Istniejący monitor Windows odświeża dashboard po zapisaniu próbki, ale błąd generatora nie zatrzymuje pomiarów.
- 18 regresji PowerShell PASS, AST 0 błędów. PBOX1 po Autosync uruchomił poprawiony monitor 15:31:08 i wygenerował 4060-bajtowy HTML z wydarzeniami DevWake.
- **OB3-029 nadal PARTIAL:** brak zintegrowanego widoku wewnątrz aplikacji i rozbudowanej korelacji wielu dni. Checkpoint `docs/openbrowser-checkpoint-2026-10-19-1512-second-wake.md`.

- **OB3-029 automatic refresh LIVE_VERIFIED, 15:33 CEST:** istniejące zadanie Resource Monitor zapisało próbkę 15:33:01, a HTML z wydarzeniami i zasobami automatycznie odnowiono o 15:33:08 (4044 bajty). Bez tworzenia nowego zadania. Integracja dashboardu z GUI Lite nadal TODO.

## Aktualizacja 2026-10-19 17:51 CEST: wybudzenia wielu rozmów i notatki

- **Przyczyna 15:35 potwierdzona:** PBOX1, 15:36:27 oraz 15:46:27 `dev_wake_skipped reason=other_chat`. Wersja 1.72.7 nie wysyłała, gdy widoczny czat był inny niż przypięty.
- **OB3-055 i OB3-061 SOURCE+LIVE v1.72.12:** szyfrowany rejestr DPAPI do 48 jawnie włączonych czatów, osobne interwały 20–1440 minut, teksty poleceń, terminy, stan i zatrzymanie. Osobny WebView2 worker pracuje na czacie docelowym bez potrzeby przełączenia foreground. UI `Wybudzenia` i `Ustaw ten czat`, komendy `--lite-chat-wake-set/off/now`; klasyczny pojedynczy timer ma zablokowaną wysyłkę w trybie multi.
- **OB3-049/051 SOURCE+LIVE:** notatki konkretnej rozmowy pobierane z dotychczasowego szyfrowanego store i dołączane do wiadomości przy pobudce. Test książki `includesNotes=true`.
- **OB3-063 realny test LIVE v1.72.12:** w rozmowie „Napisz książkę przygodową” 17:47:53 log `phase=click_invoked`, `visibleUserBubble=true`, `receiptConfirmed=true`; każda wiadomość ma nowy `[OB-<receipt>]`, dlatego nie może zostać uznana za stary powtarzany komunikat. Obsługa klikania została przeniesiona do hosta C# (`submitDirect`), aby opóźnianie `setTimeout` w tle nie blokowało wysyłki. Testy nadawcy 20 PASS, HighGuard 18, HighSlider 5, Release build i selftest PBOX1 PASS.
- **Aktywne na PBOX1:** czat Open Browser projekt 25m, następne 18:02:33; czat książki 25m, następne 18:15:01, już ze stałym poleceniem kontynuowania jednej spójnej sceny (bez starych znaczników testowych).
- **Ograniczenia:** nie potwierdzono jeszcze dwóch kolejnych NATURALNYCH cykli dwóch różnych czatów, ani odpowiedzi modelu na wysłaną w tle wiadomość książki. Client-side DIL receipt to dowód widocznej nowej wiadomości użytkownika, nie odpowiedzi asystenta. Obserwować zdarzenia `RenderProcessUnresponsive`.
- Checkpoint: `docs/openbrowser-checkpoint-2026-10-19-1717-multichat-wake.md`. Instrukcja dla czatów: `docs/openbrowser-multichat-wake-commands-2026-10-19.md`. Nie oznaczaj całego 86-punktowego planu jako kompletnego. Bez ręcznego commit/push i bez ChatGPT Tasks.
