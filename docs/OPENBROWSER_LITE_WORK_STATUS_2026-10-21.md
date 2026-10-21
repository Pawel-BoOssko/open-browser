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
