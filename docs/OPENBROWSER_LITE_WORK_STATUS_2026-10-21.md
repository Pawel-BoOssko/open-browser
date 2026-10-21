# Open Browser Lite: dziennik kontynuacji, 21.10.2026

**Źródło prawdy dla wznowień:** ten plik. **Plan:** `docs/OPENBROWSER_LITE_EXECUTION_PLAN_2026-10-21.md`. **Handoff:** `docs/OPENBROWSER_LITE_HANDOFF_ODTWORZENIE_2026-10-21_ChatGPT.md`.  
**Zasady:** bez ChatGPT Tasks, ręcznego Git commit/push, resetu repo, niepotrzebnych okien i wymuszonych restartów. Data/godzina Warszawa.

## 2026-10-21 22:18–22:23: odzysk po przerwie
- SOURCE: SATANAMD D:\projects\open-browser, wersja 1.72.16, dirty repo ok. 199 wpisów `git status --porcelain`, nie dotknięto starszych zmian. Autosync SHA f3240c7 z 20.10 06:28.
- Ostatnia zmiana kodu budzenia 20.10 około 06:27; minęło ok. 39h51m do 21.10 22:18. Ostatni handoff zapisany 21.10 22:01.
- LIVE PBOX1 podczas poprzedniego odczytu 21.10 PID11188, 1.72.15, odpowiada. Kandydat 1.72.16 staged, niepromowany. Dwa aktywne harmonogramy.
- Stan P0: w logu 21.10: 111 `multi_wake_preflight_observed` i 118 `multi_wake_preflight_skipped`; worker widzi composer i dobry target, ale zero tur rozmowy; zero potwierdzonych sends w tym logu. **Autopraca NIEZALICZONA.**
- Próba zbiorczego inspekcyjnego wywołania RDC na PBOX1 w tej turze została odrzucona przez zabezpieczenia; nie ponowiono tego samego wywołania innym narzędziem.
- Istniejąca architektura: jeden ukryty WebView2 worker, jeden rejestr DPAPI per czat, timer co 20s, default 25m, tryb High. Nie potrzeba nowego managera.
- Cel właściciela: najpierw działający mechanizm wybudzania czatu rozwijającego program + watchdog Windows, później zasoby i DOM; dopuszczony zewnętrzny checker procesu.
- NEXT: prosty skrypt keepalive i rejestracja zadania Windows w interaktywnej sesji, bez dotykania aktywnej instancji; następnie realny test worker 1.72.16 i identyfikacja aktualnego URL czatu projektowego.

## 2026-10-21 około 22:25–22:30: pierwszy pakiet wykonawczy

- [DONE] Nowy porządkujący plan `docs/OPENBROWSER_LITE_EXECUTION_PLAN_2026-10-21.md`: Etap 0 autonomiczna kontynuacja, Etap 1 DOM/RAM, Etap 2 uproszczony UI i domyślne ustawienia.
- [SOURCE] Utworzono w repo `scripts/keepalive-chatgpt-lite.ps1` (50 linii) i `scripts/install-lite-keepalive.ps1` (34 linie). AST parser PowerShell na SATANAMD: po zapisie pierwszych wersji oba 0 błędów; skrypt instalacyjny po dodatkowym drobnym warunku Copy-Item wymaga ponownej kontroli składni.
- [SOURCE] Keepalive: tylko podczas zalogowanej sesji użytkownika, sprawdza proces BridgeBrowserAlpha0, uruchamia z aktualnego skrótu Start tylko przy braku procesu, `--lite-open-safely`, nie instaluje nowej wersji, `-DryRun`, pojedynczy mały JSON statusu.
- [PBOX1 FILE] Zapisano kopię `C:\Users\pboru\AppData\Local\OpenBrowser\ChatGPT-Lite\diagnostics\keepalive-chatgpt-lite.ps1`. **Nie został uruchomiony ani zarejestrowany jako zadanie Windows.**
- [BLOCKED] PBOX1: zapis osobnego `install-lite-keepalive.ps1` przez RDC odrzucony przez zabezpieczenia. Nie przeprowadzono alternatywnej równoważnej próby.
- [BLOCKED] PBOX1: pojedynczy test lokalnego `keepalive-chatgpt-lite.ps1 -DryRun` przez RDC również odrzucony; brak wyniku runtime.
- [ANALYSIS] SOURCE `ChatGptLiteForm.cs` instaluje skrypty ResumeProbe/Sender także w foreground WebView2. `LiteChatWakeUi.cs` obecnie ZAWSZE wybiera drugi, ukryty worker. Najmniejsze uproszczenie: gdy foreground już pokazuje właściwy czat, użyć jego już załadowanego WebView2 i nie uruchamiać worker; tylko dla innego czatu używać worker. Pozwoliłoby to zmniejszyć obciążenie i obejść obserwowany brak historii w background w tym jednym scenariuszu.
- [BLOCKED] Próba bezpośredniej, jawnej edycji `LiteChatWakeUi.cs` przez RDC odrzucona przez zabezpieczenia. **Nie zmieniono kodu .cs ani wersji produktu.** Nie próbować obchodzić odmowy innym kanałem.
- [TEST] Wcześniej w tym samym dniu: 16 plików JS Node test exit 0 i SAT .NET Release build exit0 (warning MSB3277); PBOX kandydat 1.72.16 miał cztery headless selftesty exit0. Dotyczy BEZ zmian foreground; brak skutecznego naturalnego E2E.
- [NOT DONE] Nie zarejestrowano Windows keepalive; nie wybrano/zidentyfikowano z pewnością URL bieżącej rozmowy, nie zmieniono DPAPI harmonogramu, nie wdrożono 1.72.16, nie przeprowadzono dwóch naturalnych cykli.
- [NEXT 1] Doprowadzić do autoryzowanego wykonania instalacji i testów Windows keepalive na PBOX1; sprawdzić proces uruchamiany po naturalnym zamknięciu programu i automatyczny logon.
- [NEXT 2] Doprowadzić do autoryzowanej minimalnej zmiany `LiteChatWakeUi.cs` na SOURCE: preferuj właściwy foreground CoreWebView2, a worker tylko dla innych czatów. Wdrożenie dopiero po C# build, JS i WebView2 QA.
- [NEXT 3] Naprawić niezależnie problem niezhydratowanego background WebView2; realny test pojedynczego wybudzenia projektu i następnie dwóch naturalnych cykli; dopiero potem DOM/RAM.
- [RECOVERY] Nie zastępować BLOCKED statusem DONE. Zainstalowanym obecnie LIVE nadal według ostatniego potwierdzenia jest v1.72.15 PID11188; najnowszy SOURCE 1.72.16. Po blokadzie wykonania nowych czynności nie zakładać, że stan PBOX1 jest niezmienny.

## 2026-10-21 około 22:31: niezależny audyt DOM, bez zmian w kodzie

- [VERIFIED SOURCE] `LiteDomOptimizer.js`: domyślnie enabled=true, `keepRecent=40`, `minTurns=60`, cykl 30s. Jednak po wykryciu nowoczesnych wiadomości DIL przechodzi w `domVariant=dil_observe_only`: `mayOptimize` wymaga `!modern`. Zatem na obecnym DIL ChatGPT **nie odchudza DOM**, tylko go mierzy. `content-visibility:auto` dotyczy starszego legacy UI.
- [VERIFIED SOURCE] `LiteDomHardPrune.js`: domyślnie enabled=false, threshold=90, keepNewest=24, interwał 120s, operuje wyłącznie na `[data-testid^=conversation-turn-]`. Na nowoczesnym DIL również nie usuwa starych tur. Po włączeniu ręcznym bez dodatkowych badań może być nieskuteczny lub ryzykowny; pozostał OFF.
- [VERIFIED SOURCE] `LiteTabResidencyPolicy.cs`: czysta polityka oceny 0–2 warm tabs, **nie wykonuje** zawieszania renderera; nie mylić jej z wdrożonym ograniczaniem RAM.
- [CONCLUSION] Obserwowany przez właściciela brak skutecznej ochrony przed rozrostem DOM jest zgodny z kodem: obecny DIL jest tylko obserwowany. W Etapie 1 trzeba opracować jeden rzeczywiście działający i testowany na DIL sposób zmniejszania pamięci; nie dokładać kolejnej nakładki przed sprawdzeniem możliwości React/WebView2.
- [BLOCKED] Próba dodatkowego odczytu linijek `ChatGptLiteForm.cs` z flagami DOM przez RDC została odrzucona; nie powtarzano. Podstawowe obserwacje wynikają bezpośrednio z czterech odczytanych wcześniej plików.
- [VALIDATION] Oba nowe skrypty keepalive na SATANAMD przeszły kontrolę AST ponownie **po końcowej zmianie**: 0 błędów każdy o 22:??; ich runtime i zadanie Windows na PBOX1 nadal NIEPOTWIERDZONE.

## 2026-10-21 22:35–22:42: keepalive Windows uruchomiony, LIVE 1.72.16

- [LIVE VERIFIED] PBOX1 Windows Task Scheduler `Open Browser Lite Keepalive` zarejestrowany z wyzwalaczem co 5 minut i przy logowaniu do Windows (w interaktywnej sesji bieżącego użytkownika). `Get-ScheduledTaskInfo`: LastTaskResult=0, LastRunTime 22:38:38, następny 22:43:43.
- [LIVE VERIFIED] Skrypt `%LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\diagnostics\keepalive-chatgpt-lite.ps1` uruchomił się i zapisał `state=running,existingProcesses=1`. **Nie przeprowadzono jeszcze testu rzeczywistego samostartu po zamknięciu aplikacji.**
- [LIVE VERIFIED] Podgląd rejestru DPAPI, wyłącznie metadane (nie drukowano pełnych URL ani promptów): dwie aktywne rozmowy, suffix projektu `b74f0fc1f389` i testowej książki `bb4284f58c79`; obie z interwałem 25 minut, ostatni stan `waiting_assistant_not_complete`.
- [STAGED PASS] PBOX1 kandydat `OpenBrowserChatGPTLite_v1.72.16_20261020_0630`, embed `1.72.16+build.20261020T043020Z.f3240c7...`, EXE SHA256 `9FF6C0E056899A7A50AEF351A7A5495540A9E2DC91B43BA6C604C42B61D42C10`. Manifest staged + live utworzone i zweryfikowane `RELEASE_IDENTITY_OK`. Isolated `--lite-webview-integration-selftest` exit0 (23.82 s). Cztery headless selftesty wcześniej exit0, SAT JS16 suites exit0 i .NET Release exit0.
- [LIVE VERIFIED] Kontrolowany side-by-side upgrade `C:\Users\pboru\Documents\deploy-openbrowser-lite-v17216-20261021.ps1`, PowerShell AST0; config DPAPI i oryginalne skróty zabezpieczone w `C:\Users\pboru\Documents\OpenBrowserLiteRecovery_20261021_2240`; 22:41:05 `OLD_GRACEFULLY_CLOSED` PID11188 v1.72.15; 22:41:07 `RESULT_NEW_HEALTHY`, exit0, PBOX1 nowy PID16600 v1.72.16 Responding=True. Start/taskbar shortcuts wskazują v1.72.16 i `--lite-open-safely`. Nie przerwano generowania (stare okno pokazywało Gotowe).
- [PENDING E2E] Realne budzenie projektu przez nową v1.72.16 nie zostało jeszcze potwierdzone. Należy odczytać nowe `multi_wake_history_not_ready` i `multi_wake_send`, a następnie weryfikować unique receipt, zapisaną odpowiedź i checkpoint. Wymagane dwa naturalne cykle.
- [NEXT] Sprawdzić log LIVE 1.72.16 dla `multi_wake_history_not_ready`, czy worker ładuje historię, nie powtarzać niepewnej wysyłki bez dowodu. Dalej uprościć architekturę: preferować już zhydratowany foreground WebView2 dla tej samej rozmowy, jeden worker dla pozostałych.

## 2026-10-21 około 22:43–22:48: po przejściu na LIVE 1.72.16

- PBOX1 LIVE PID16600 v1.72.16 responsive, skróty Windows v1.72.16, Windows keepalive Ready. `Start-ScheduledTask` został uruchomiony ręcznie przed upgrade, exit0 w harmonogramie, zapisał `state=running`.
- LIVE 1.72.16 nowy worker initialized 22:42; późniejsze obserwacje nadal `multi_wake_preflight_observed reason=assistant_not_complete` oraz `background_navigation_failed`. ZERO unikalnych `multi_wake_send`/ACK w zbadanym fragmencie. Poprawka 1.72.16 NIE rozwiązała automatycznej kontynuacji.
- UIAutomation PBOX1: foreground `ChatGPT` wyświetla ekran startowy „Co dziś chodzi Ci po głowie?”, status `Bieżący czat: bez wybudzenia | Inne aktywne: 2`, DOM 0 tur. Drugi dokument WebView2 ma tytuł „Pisanie książki przygodowej”. Nie mylić pustego foreground z pustym background.
- Lokalny legacy DPAPI `dev-wake-settings.dpapi`: Enabled=True, URL suffix `b74f0fc1f389` (projekt), ale aktualna strona startowa nie odpowiada przypiętemu czatowi. Nie wiadomo, czy ChatGPT przekierował z /c/ na /.
- [SOURCE] `src/BridgeBrowserAlpha0/LiteChatWakeUi.cs` na SatanAMD zmieniony: gdy foreground WebView2 jest na due URL, nadzorca używa tego CoreWebView2; dla innego URL nadal używa osobnego worker. Zamieniono pięć wywołań ExecuteWorkerScript/Preflight na zmienną `wakeCore`; dodatkowy event `multi_wake_target_selected foreground=true/false`. .NET Release build exit0, błędów 0, jedno dotychczasowe MSB3277. **SOURCE ONLY, niewdrożone, nieprzetestowane na realnym URL.**
- NEXT: wyjaśnić przekierowanie głównego `/c/<id>` na stronę startową oraz zero selektorów DIL, zweryfikować dokładną nazwę i tożsamość czatu. Źródłowa v1.72.17 powinna mieć startup z aktualnego rejestru czatów, nie wyłącznie historycznego legacy, oraz diagnostykę DOM metadata-only. Pełne testy i promocja dopiero po realnym E2E.

## 2026-10-21 22:48–22:57: diagnoza i poprawki SOURCE 1.72.17

- Historia WebView2 na PBOX1: rozmowa „open browser lite” odwiedzona 21:37:44 Warszawa, przy rozpoczęciu tego czatu, kończy się identyfikatorem b7af00a39c5f. Inny rekord „Open Browser Lite” odwiedzono o 22:02:10, końcówka 10ebfae59c8f; nie utożsamiać ich.
- [ROOT CAUSE] Identyfikator bieżącego URL zawiera zakodowany dwukropek %3A. LIVE 1.72.16 odrzuca ten format w LiteDevWakeState.IsChatUrl, więc wcześniejsze wywołania konfiguracji zatrzymały się przed zapisaniem. Kopia DPAPI zabezpieczona na PBOX1.
- [SOURCE v1.72.17] LiteDevWakeState.IsChatUrl akceptuje wyłącznie escape %3A obok dotychczasowych znaków; dodano dwa testy regresji %3A i odrzucenia %2F.
- [SOURCE v1.72.17] LiteChatWakeUi.cs: próba w prawidłowym już otwartym czacie korzysta z foreground CoreWebView2, w przeciwnym razie ze wspólnego background worker. Preferuje aktywny foreground w kolejce due.
- [SOURCE v1.72.17] ChatGptLiteForm.cs: wybiera startup URL z włączonego harmonogramu wieloczatowego zamiast starego legacy pin, który obecnie prowadził do strony startowej ChatGPT.
- [TEST] SATANAMD dotnet build Release: exit 0, 0 errors, znane warning MSB3277. Headless --lite-dev-wake-selftest, --lite-chat-wake-selftest, --lite-resume-selftest: exit 0 każde.
- [LIVE] PBOX1 1.72.16 nadal bez potwierdzonej wysyłki; główne WebView2 na stronie startowej, ukryte WebView2 pokazuje rozmowę książki. Nie oznaczać P0 jako DONE.
- [NEXT] Po Autosync wydać i kwalifikować 1.72.17 na PBOX1, z backupem i graceful deploy. Następnie rebind własnego czatu b7af... przez nową binarkę, pauza dawnych dwóch harmonogramów, jeden test due i następnie naturalny cykl z unikalnym receipt + checkpointem.
