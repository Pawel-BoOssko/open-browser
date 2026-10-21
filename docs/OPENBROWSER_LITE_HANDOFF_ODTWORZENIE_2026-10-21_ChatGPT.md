# Open Browser Lite: odzyskany stan projektu, 21.10.2026

**Zakres:** odtworzenie z historii rozmów (priorytet), plików modyfikowanych 14–21.10.2026 na SATANAMD i PBOX1, danych Git i repozytorium GitHub. Bez odwoływania się do starszych plików jako aktualnych źródeł.  
**Produkt:** Open Browser Lite / Open Browser - ChatGPT Lite. To NIE jest odrębny projekt Open Bridge ani Bridge Browser. Historyczna nazwa EXE i katalogu źródłowego `BridgeBrowserAlpha0` nie zmienia tożsamości produktu.  
**Metoda:** audyt read-only repo/produkcji, cztery samodzielne testy kandydata, bez zmiany aktywnego procesu, danych, ustawień harmonogramu, źródeł lub plików git.
**Uwaga:** ten nowy dokument jest punktem wznowienia, a szczegółowym backlogiem pozostaje 86-pozycyjna roadmapa wskazana niżej. Fakty SOURCE/TEST/STAGED/LIVE należy zawsze rozdzielać.

## 1. Ustalenia właściciela z wcześniejszych rozmów

- Cel nadrzędny: ukończyć Open Browser Lite i zapewnić autonomiczną kontynuację pracy wybranych czatów ChatGPT, bez udziału właściciela, z checkpointami i świadomym wyborem następnego zadania przez sam czat.
- Wewnętrzny nadzorca aplikacji zwykle co 25 minut; nigdy nie wskrzeszać natywnych ChatGPT Tasks/reminders. Z historycznej korespondencji: dwie automatyzacje rozwoju Open Browser zostały wyłączone 19.10.2026. Nie włączać ponownie.
- Autopraca: model High ma zostać zweryfikowany; nie wysyłać, jeśli wciąż generuje, jest niewysłany szkic, nie ma zakończonej stabilnej odpowiedzi, istnieje błąd lub niewłaściwy czat. Budzenie krótkim komunikatem, nie przydzielaniem zadań.
- Dla każdej rozmowy oddzielny wyraźnie włączony harmonogram, DPAPI, notatki i idempotentne potwierdzanie wysyłki. Wysyłki niepewne oznaczać jako niepewne; nigdy nie przedstawiać click/form_submitted jako potwierdzenia backendowego.
- Zasada organizacyjna: bez ręcznych git commit/push; istniejący Windows Autosync synchronizuje snapshoty. Nie resetować/clean/rebase rozbudowanego dirty repo.
- Nie naruszać działającego okna, szkiców i profilu, nie zabijać masowo wszystkich procesów. Side-by-side publikacja, manifest, selftesty, bezpieczne graceful upgrade i fallback.
- Każdy rzeczywisty wynik i blokada natychmiast trafia do lokalnego checkpointu; kod i historia nie zastępują obserwacji LIVE.
- Historyczna decyzja 19.10 o ponowieniach po NIEPEWNEJ wysyłce jest niespójna: w `docs/agent/architecture-decisions_new.md` zaakceptowano ograniczony retry po 25 min, ale późniejszy kod blokuje fingerprint tej samej odpowiedzi. Nie rozstrzygać po cichu bez decyzji właściciela.

## 2. Lokalizacje, tożsamość, synchronizacja

- SATANAMD, SOURCE: `D:\projects\open-browser`, gałąź `master`, HEAD `374ed29`, lokalny `master` jest 27 commitów przed `origin/master`; masowy zbiór plików staged/modified/untracked, w tym trwająca historyczna relokacja `releases/BridgeBrowserAlpha0/src/` -> `src/BridgeBrowserAlpha0/`.
- SATANAMD, inne katalogi historycznie wymienione w kontekście: `D:\projects\open-browser-stable`, `D:\downloads-open-browser`, `D:\bridge-poc`. Nie korzystano z plików starszych niż 7 dni jako źródeł aktualnego stanu.
- GitHub: `https://github.com/Pawel-BoOssko/open-browser`; `master` przestarzały (publiczny HEAD `b4da9f8`, maj 2026). Źródło współczesne: `autosync/SATANAMD`, snapshot `f3240c7`, 20.10.2026 06:28:47 Warszawa. Potwierdzono przez lokalne refs i fetch GitHub, że w tej gałęzi `BridgeBrowserAlpha0.csproj` ma `<Version>1.72.16</Version>`.
- PBOX1, LIVE 21.10: `C:\Users\pboru\AppData\Local\Programs\OpenBrowserChatGPTLite_v1.72.15_20261020_0621\BridgeBrowserAlpha0.exe`; PID 11188, Responding=True, tytuł okna Open Browser Lite 1.72.15. Log zdarzenia `window_constructed`: `1.72.15+build.20261020T042137Z.c5a522d...`.
- PBOX1, kandydat 1.72.16: `%LOCALAPPDATA%\Programs\OpenBrowserChatGPTLite_v1.72.16_20261020_0630`; EXE SHA256 `9FF6C0E056899A7A50AEF351A7A5495540A9E2DC91B43BA6C604C42B61D42C10`. Osobny build-source `%LOCALAPPDATA%\OpenBrowser\build-source-v1.72.16` z HEAD `f3240c7`.
- Skróty Start i przypięty skrót paska zadań 21.10 kierują do 1.72.15 z `--lite-open-safely`. 1.72.16 nie jest uruchomiona i nie została w tym audycie promowana. W katalogu 1.72.16 brak oczywistego manifestu wydania; wymagany osobny release gate i test rzeczywisty.
- Diagnostyka na PBOX1: `%LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\diagnostics\lite-events-YYYY-MM-DD.jsonl`, `resources-YYYY-MM-DD.jsonl`, `resources-dashboard.html`.
- Chat profile, notatki, rejestr wybudzeń: DPAPI w lokalnym profilu PBOX1. Nigdy nie kopiować sekretów, pełnych URL rozmów, treści wiadomości lub cookies do repo/logów.

## 3. Architektura w odzyskanych plikach

.NET 8 Windows WinForms, Microsoft WebView2, skrypty JavaScript wstrzykiwane do lokalnego WebView2; aplikacja ma osobny tryb `--chatgpt-lite` bez automatycznego wykonywania starego protokołu OpenBridge. 
Moduły SOURCE `src/BridgeBrowserAlpha0`:
- `ChatGptLiteForm.cs` oraz `LiteWindowShell.cs`: powłoka użytkownika i osadzone WebView2.
- `LiteChatWakeRegistry.cs`, `LiteChatWakeUi.cs`, `LiteChatWakePreflight.cs`: wiele allowlisted rozmów, harmonogramy, izolowany ukryty WebView2 worker, preflight i statusy.
- `LiteDevWakeState.cs`, `LiteDevWakeUi.cs`, `LiteDevWakeDirect.cs`: starszy jednochatowy DevWake; wyłączony jako równoległy sender po inicjalizacji multi-chat.
- `LiteResumeProbe.js`, `LiteResumeSender.js`, `LiteHighGuard.js`, `LiteResponseSignal.js`: wykrywanie ukończonej tury/DIL, poprawnego przycisku wyślij, idempotencja receipts, kontrola High.
- `LiteChatNotes.cs`, `LiteNotesOutbox.cs`, `LiteNotesUi.cs`, `LiteWorkJournal.cs`, `LiteSecureArchive.cs`: szyfrowane notatki, outbox, dziennik, archiwum i checkpoint.
- `LiteProjectRegistry.cs`, `LiteProjectsUi.cs`, `LiteLeadLease.cs`: lokalny panel projektów i koordynacja czatów; NIE utożsamiać lokalnego panelu z rzeczywistymi projektami ChatGPT.
- `LiteDomOptimizer.js`, `LiteDomHardPrune.js`, `LiteDomSelectorAudit.js`, `LiteRendererBackpressure.cs`, `LiteTabResidencyPlanner.cs`: DOM/performance/pool kart; agresywnego DIL pruning nie włączać bez realnego długiego testu.
- `LiteEventLog.cs`, `LiteTechnicalInfoUi.cs`, `LiteTransportDiagnostics.cs` i skrypty `scripts/analyze-lite-resource-trends.ps1`, `scripts/export-lite-resource-dashboard.ps1`: obserwowalność i raporty.
- `LiteUpgradeHelper.cs`, `LiteBuildInfo.cs`, release manifest scripts w `scripts/`: build, staging, aktualizacje i rollback.
- `LiteLunaAdvisor.cs`: doradca oparty na zewnętrznym API; nie utożsamiać nazwy Luna z gotowym identyfikatorem modelu API.

## 4. Rekonstrukcja chronologiczna 18–21.10

- **18.10:** rozwój oddzielnego lekkiego klienta, główne komponenty notatek, archiwum, recovery/DOM, diagnostyka, roadmap v2 i v3. Odnotowane etapy 1.60–1.64 i testy. Zakaz nowych natywnych automatyzacji ChatGPT oraz ręcznych pushy.
- **19.10 rano:** wersje 1.68–1.72.4; poprawki preflight DIL i legacy, High, wykrywanie busy i submit, trwałe receipts. Wczesne buildy miały `answer_missing` i brak ACK; raporty STAGED NIE były równoznaczne z LIVE.
- **19.10 14:45 i 15:10:** dwa prawdziwe budzenia głównego czatu w v1.72.6, odstęp 25m32s, kontynuacja pracy. Udokumentowano rzeczywisty sukces tych dwóch cykli, nie ogólnej niezawodności.
- **19.10 15:35:** pominięta pobudka z powodu `other_chat`. Projekt przeszedł na niezależne multi-chat schedules z jednym niewidocznym WebView2 worker i szyfrowanymi notatkami.
- **19.10 17:47–17:49:** wersja 1.72.12 LIVE potwierdziła jeden rzeczywisty background wake czatu książki z unikalnym receipt, `visibleUserBubble=true`, `receiptConfirmed=true`, `includesNotes=true`. To dowód klientowego ACK, nie generowania odpowiedzi serwera. Dwa aktywne chaty: projekt i testowa książka, 25 minut każdy według ówczesnego rejestru.
- **20.10 przed 06:00:** przygotowano 1.72.13/14. Nowy preflight two-sample, fingerprint przed wysyłką, host-driven click zamiast throttled JS timer, 6500ms limit `ExecuteScriptAsync`. Bezpieczne wdrożenie side-by-side. Stary taskbar link uruchamiał 1.64; później poprawiono.
- **20.10 06:22–06:27:** 1.72.15 LIVE wykrywało `composer=true`, `targetMatches=true`, ale `assistantNodes=0`, `userNodes=0`, `turns=0`. Worker ładował pustą powłokę czatu i nie wysyłał. Źródło 1.72.16 wymaga dodatkowo rzeczywistych wiadomości i po ~25s loguje tylko metadata `multi_wake_history_not_ready`.
- **20.10 06:30:** na PBOX1 utworzono, lecz nie wdrożono do aktywnego okna binarkę 1.72.16. Snapshot Autosync `f3240c7`.
- **21.10 około 19:52–21:53 Warszawa:** aktywna 1.72.15 PID 11188, dwie włączone rozmowy (z `multi_wake_loaded enabledCount=2`). Dzienny log ma 877 zdarzeń, w tym 118 `multi_wake_preflight_skipped`, 111 `multi_wake_preflight_observed`, 237 `ui_heartbeat`, 237 `dom_status`; ZERO `multi_wake_send` w tym logu. Ostatnie preflight ponawia `assistant_not_complete` przy 0 elementach rozmowy; generowanie, tool i draft są false, composer i target są true. To **aktywny blocker P0**.
- **21.10 testy bez zmiany LIVE:** na kandydacie PBOX1 1.72.16 uruchomiono `--lite-version-selftest`, `--lite-chat-wake-selftest`, `--lite-dev-wake-selftest`, `--lite-resume-selftest`. Wszystkie cztery zwróciły exit 0. Nie był to realny WebView2 E2E ani test naturalnej wysyłki.

## 5. Plan i stan kompletności

Kanoniczna roadmapa: `docs/design/openbrowser-master-roadmap-v3-2026-10-19.md`, **OB3-001 do OB3-086**, 9 faz:
0 (001–020) bezpieczne autowznowienie; 1 (021–030) diagnostyka; 2 (031–037) DOM i wydajność; 3 (038–045) projekty ChatGPT; 4 (046–054) UX operatora; 5 (055–063) autonomiczny nadzór multi-chat; 6 (064–070) MCP/integracje; 7 (071–077) recovery; 8 (078–086) testy, instalator, updater i finalny soak.
Wiele funkcji ma status SOURCE lub TEST, część STAGED lub pojedyncze LIVE_VERIFIED. **86 zadań nie jest ukończonych.** Nie przenosić statusu z wersji 1.72.12 na 1.72.16 bez nowego dowodu.
- Obecnie krytyczne: OB3-007/009/025/055/059/063, trwałość/bezpieczeństwo wiadomości i realny ACK w dwóch niezależnych czatach.
- Wymagania równoległe: procesy WebView2/RAM/renderer, długie czaty, optymalizacja DIL, realne projekty vs aliasy, notatki i dyktowanie, własny serwer MCP, odzyskiwanie utraconych odpowiedzi, updater/rollback, OB-10, wielodniowy soak.
- Testowy czat książki ma warunek stop `[BOOK-COMPLETE]` i historyczną sztuczną notatkę o kompasie. Nie wypuszczać testowego tekstu do prawdziwego projektu.
- Nadzorca głównego projektu ma marker `[OPENBROWSER-PLAN-COMPLETE]`. Wcześniejsze dowody dwóch poprawnych cykli dotyczą 19.10, nie 21.10.

## 6. Zalecana kolejność przejęcia (bez naruszania działającej produkcji)

1. Traktować ten dokument, roadmapę v3 oraz najnowszy `docs/openbrowser-checkpoint-2026-10-20-0619-live-v17214-preflight.md` jako wspólne źródła. Czytać nowsze sekcje nad historycznymi nagłówkami.
2. Zweryfikować aktualny proces PBOX1, ostatnie metadata-only logi i dwa oddzielne harmonogramy. Nie drukować zapisanych promptów ani URL.
3. Sprawdzić, czy katalog 1.72.16 rzeczywiście ma wymagany manifest i pełny gate oraz izolowany realny WebView2 QA. Na 21.10 cztery headless testy PASS; **full gate i naturalny wake E2E NIEPOTWIERDZONE**.
4. Rozpoznać dlaczego background worker na 1.72.15 stale otrzymuje pusty DOM: uwzględnić niegotową historię ChatGPT, ukryte/occluded WebView2, autoryzację i zmiany DIL; nie obchodząc zabezpieczeń preflight ani nie wysyłając w ciemno. Kandydat 1.72.16 ma licznik i timeout do diagnostyki, ale wymaga testu w rzeczywistym środowisku.
5. Dopiero po pozytywnym gate i ocenie idle: gracjalna promocja 1.72.16, backup DPAPI, rollback, bez `Stop-Process` grupowego; nie przerywać aktywnej odpowiedzi.
6. Naturalny test dwóch oddzielnych rozmów i cykli. Wyraźnie rozdzielić `due`, `preflight`, `click`, `visible unique receipt`, faktyczną odpowiedź. Weryfikować `includesNotes`, fingerprint i brak duplikatów.
7. Jeśli blocker nadal występuje, zbadać go osobno zamiast oznaczać P0 jako DONE; niezależnie kontynuować następne roadmap items, zachowując krótkie checkpointy.
8. Po każdym rzeczywistym etapie dopisać lokalny checkpoint z czasem, PID, wersją, ścieżką, build/test evidence, blockerem i kolejnym krokiem. Bez ręcznego commit/push; nie tworzyć ChatGPT Tasks.

## 7. Kluczowe niedawne dokumenty do przeczytania

- `docs/design/openbrowser-master-roadmap-v3-2026-10-19.md` (modyfikowany 20.10 06:28, pełne 86 zadań).
- `docs/openbrowser-checkpoint-2026-10-20-0619-live-v17214-preflight.md` (modyfikowany 20.10 06:28, ostatni root-cause, SOURCE 1.72.16).
- `docs/openbrowser-checkpoint-2026-10-20-0535-wake-preflight.md` (20.10 05:55, szczegółowo v1.72.13/14 i deployment).
- `docs/openbrowser-checkpoint-2026-10-19-1717-multichat-wake.md` (19.10 17:51, multi-chat i realny ACK 1.72.12).
- `docs/openbrowser-checkpoint-2026-10-19-1512-second-wake.md` (19.10 15:33, dwa wake i resource dashboard).
- `docs/OPENBROWSER_DEEP_HANDOFF_2026-10-19_1245_ChatGPT.md` (19.10 12:42, wcześniejszy audyt: statusy historyczne).
- `docs/OPENBROWSER_AUDIT_RUNLOG_2026-10-19_1220_ChatGPT.md` (19.10 12:42, rejestr audytu).
- `docs/agent/architecture-decisions_new.md` (modyfikowany 19.10 12:39, spór retry vs dedup).
- `docs/agent/development-rules_new.md` (modyfikowany 18.10, zasady repo; nie czytać starszych plików jako źródeł zmian ostatniego tygodnia).
- `docs/Rejestr nieudanych komend.md` (modyfikowany 19.10 14:55, rzeczywiste blokady narzędzi).

**Źródła dowodów 21.10:** PBOX1 proces 11188, log `lite-events-2026-10-21.jsonl`, SHA256 kandydata, 4 selftesty exit 0; SATANAMD `git status`, `git log origin/autosync/SATANAMD`, najnowszy kod 1.72.16; GitHub `autosync/SATANAMD` weryfikowany przez bezpośredni odczyt kodu i checkpointu. 

## 8. Dodatkowa walidacja bieżącego przejęcia, 21.10

- Na SATANAMD uruchomiono 16 plików testów JavaScript `tests/lite-*.test.cjs` z ostatnich siedmiu dni przez `node --test`; kod wyjścia 0.
- Następnie `dotnet build D:\projects\open-browser\src\BridgeBrowserAlpha0\BridgeBrowserAlpha0.csproj --configuration Release --no-restore -v:q` zakończył się kodem 0: błędów 0, jedno znane ostrzeżenie MSB3277 (konflikt WindowsBase v4/v5 w zależności WebView2).
- Żaden z tych testów nie stanowi realnego testu loginu ChatGPT, hydracji ukrytego WebView2 ani naturalnej wysyłki. Nie zmieniono aktywnego procesu PBOX1, nie uruchomiono przypominajki ChatGPT i nie wykonano ręcznego commit/push.
