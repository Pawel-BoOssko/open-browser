# Open Browser Lite — audyt stanu i przekazanie projektu

**Sporządzono:** 22 października 2026, około 14:10 CEST (Warszawa) / 15:10 EAT (Kenia).  
**Rodzaj:** handoff dla nowego czatu; audyt tylko do odczytu na PBOX1 i weryfikacja testowa na SatanAMD.  
**Priorytety właściciela:** (1) realnie niezawodne samowybudzenia 5–10 rozmów, (2) małe RAM/CPU/DOM, (3) prosty UI.  
**Polecenie właściciela z 22.10:** aplikacja obecnie praktycznie nie odpowiada i nie udało się jej dobrze uruchomić; **nie naprawiać ani nie uruchamiać jej w ramach tego audytu**. Przygotować zapisany handoff, by inny czat mógł przejąć projekt.  
**Ważne:** Ten raport NIE jest dowodem, że nowa aplikacja działa produkcyjnie, ani poleceniem restartowania, zabijania procesów, wdrażania lub wysyłania testowych wiadomości.

## 0. Najważniejszy stan na moment przekazania

| Obszar | Potwierdzony stan | Czego nie wolno wnioskować |
|---|---|---|
| Kod źródłowy na SatanAMD | `D:\projects\open-browser\src\BridgeBrowserAlpha0\BridgeBrowserAlpha0.csproj` wskazuje **v1.72.58** | To nie jest wersja zainstalowana na PBOX1 |
| Kandydat wydania | `%TEMP%\OpenBrowserLiteCandidate_v1.72.58\BridgeBrowserAlpha0.exe` na SatanAMD; manifest testowy `openbrowser-release-manifest-test-v17258-1212.json` zweryfikowany | Katalog testowy nie jest dystrybucją ani instalacją PBOX1 |
| SHA-256 EXE testowego | **0eb1bafbf912bf89cb6c5aeee8f589f9287e22e8fed9621f1e0baca373f9952e**, `RELEASE_IDENTITY_OK v1.72.58` | Nie jest to hash uruchomionego PBOX1 |
| Wersja PBOX1 | W tytule uruchomionego okna nadal **v1.72.21** | Poprawki v1.72.22–58 NIE są produkcyjnie sprawdzone |
| Stan PBOX1 / zgłoszenie właściciela | Użytkownik zgłasza brak praktycznie działającego interfejsu. **14:02:59 CEST: brak procesu `BridgeBrowserAlpha0`. 14:03:03: Keepalive sam uruchomił PID 8516**. 14:06:21: `Responding=False`. 14:10:24: `Responding=True`, tytuł „Generowanie”. | Sam PID lub `Responding=True` nie potwierdza sprawnego wyświetlania, logowania, nawigacji ani generowania |
| Keepalive na PBOX1 | Windows Scheduled Task co ~5 min, ostatni odczyt JSON `lite-keepalive-status.json`: 14:03:03 `launched processId=8516`, 14:08:20 `running existingProcesses=1` | `LastTaskResult=0` / `running` nie oznacza zdrowego GUI; **audyt nie uruchamiał ani nie restartował** aplikacji |
| Samowybudzenia | Ostatnio udokumentowane **37 naturalnych wysłanych wiadomości do jednej rozmowy foreground**; #37 o12:07:41 CEST, `receiptConfirmed=true` i model `generating=true→false` w12:07:57–12:08:17 | Nie udowadnia niezależnego serverDeliveryProven, stałej pracy po12:14 ani **5–10 rzeczywistych wybudzeń czatów w tle** |
| Główny brak funkcjonalny | OBL-0.3: ukryty, współdzielony WebView2 worker na realnej zalogowanej stronie miewa composer i właściwy URL, ale **zero tur/user/assistant w MAIN** | Testy offline pod mapowaną domeną nie potwierdzają realnej hydracji na koncie |
| Pamięć | Pełny **8h baseline STAREJ v1.72.21**, 241 próbek z tą samą siódemką PID WebView2: **561,9→1023,3 MiB, +461,4 MiB**, maksimum1073,2MiB | To nie dowodzi konkretnego memory leak ani efektywności nowej v1.72.58 |
| Gotowość wdrożenia | Brak ukończonego, dozwolonego PBOX1 promotion/rollback. Wcześniejsza próba PBOX1 publish v1.72.22 spotkała odmowę narzędzia; dotąd jej nie obchodzono | Nie wykonywać alternatywnego obejścia blokady lub wymuszonej instalacji |
| Repozytorium Git | SatanAMD `master...origin/master [ahead 27]`, liczne modyfikacje i **bardzo dużo plików untracked**, m.in. kod Lite, checkpointy, testy, skrypty; liczne staged/modified renames w starszym OpenBridge | `git reset/clean/checkout`, masowy commit, manual push mogą utracić znaczną część pracy |
| Autosync | Natywne zadanie Windows SATANAMD co ~10 min; 14:00:57 `LastTaskResult=0`; ostatni zaobserwowany lokalnie tracking `origin/autosync/SATANAMD` **f14003a**, opis z12:21:01 | Nie twierdzić, że cała robocza v1.72.58 i nowy handoff znajdują się już na zdalnej gałęzi |
| Zmiany wykonane w ramach TEGO handoffu | **Brak modyfikacji źródeł, brak zmiany harmonogramów, brak restartów i wdrożeń.** Odczyt PBOX1, regresja na SatanAMD, nowy plik dokumentacji. | Samo naturalne wznowienie przez zastany Keepalive nie jest działaniem autora audytu |

## 1. Cel produktu i architektura

Open Browser Lite to mała, okienkowa aplikacja Windows, oparta o **WinForms + Microsoft WebView2 + C#/.NET 8 i wstrzykiwany JavaScript**, osadzająca stronę ChatGPT. Ma umożliwić właścicielowi trzymanie kilku rozmów i ich automatyczne, kontrolowane wybudzanie bez zakładania ChatGPT Tasks. Jedno okno foreground oraz **maksymalnie jeden** współdzielony WebView2 worker okresowo obsługujący rozmowy w tle (nie ciężki renderer na każdy czat).

Podstawowe ogniwa cyklu: Windows Keepalive (PBOX1) → uruchomiona aplikacja → lokalny rejestr harmonogramów DPAPI (`LiteChatWakeRegistry`) → wybór due chat (`SelectDueChat`, ochrona przed zagłodzeniem innych) → kontrola tożsamości URL/SPA/epoch → gotowa i stabilna odpowiedź asystenta w aktywnym `main` → bezpieczny send/recovery z własnym losowym markerem `[OB-32hex]` → obserwacja rzeczywistej wiadomości USER / ACK → trwały checkpoint i następny harmonogram.

**Ważne rozdzielenie dowodów**: wybór due ≠ hydration historii; hydration ≠ gotowa odpowiedź; gotowa odpowiedź ≠ click; click ≠ widoczna wiadomość USER; receipt w USER bubble ≠ skończona odpowiedź modelu; generating=false ≠ udowodniony zapis po stronie serwera. W logach pojawia się `serverDeliveryProven=false`. Nie sumować tych kategorii jako jeden „sukces”.

**Odporność na pomyłki**: nie wysyłać do innego czatu przy SPA A→B→A; sprawdzać exact target i epoch przed/po asynchronous JS; nie powtarzać kliknięcia przy niepewnym wcześniejszym dostarczeniu; nie przejmować obcej odpowiedzi, Stop, alertu lub tool-running z sidebar; nie zwalniać trwałego anti-duplicate jedynie przez to, że stara odpowiedź wygląda na gotową.

**Model zasobów**: jeden shared worker WebView2; po zakończeniu próby pamięć worker może przejść w Low, a podczas aktywnego ładowania Normal; rosnące koszty DOM/RAM ograniczać najpierw poprzez unikanie nadmiarowych skanów i timerów. Hard-prune DOM i eksperymentalny modern DIL CSS soft pilot pozostają **OFF do prawdziwego QA**.

## 2. Urządzenia, katalogi i źródła prawdy

**SatanAMD (źródła i testy)**:
- Repo: `D:\projects\open-browser`.
- Kod: `src\BridgeBrowserAlpha0\`.
- Project file / version: `src\BridgeBrowserAlpha0\BridgeBrowserAlpha0.csproj`.
- Testy JS: `tests\lite-*.test.cjs`.
- Testy skryptowe: `tests\test-lite-resource-trends.ps1`; także skrypty release-audit/manifest w `tests\`.
- Testowy pakiet 1.72.58: `%TEMP%\OpenBrowserLiteCandidate_v1.72.58\`; **nie** źródło produkcyjne.
- Ten handoff: `docs\OPENBROWSER_LITE_HANDOFF_2026-10-22_AUDYT_I_PRZEKAZANIE.md`.
- Nadrzędna szczegółowa historia cykli: `docs\OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md` (572 linie przed niniejszym audytem; ostatni checkpoint12:14 CEST).
- Bieżący plan etapowy: `docs\OPENBROWSER_LITE_EXECUTION_PLAN_2026-10-21.md`.
- Zasoby i 8h baseline: `docs\OPENBROWSER_LITE_RESOURCE_BASELINE_2026-10-22.md`.
- Starszy punkt odzyskania (historyczny, UWAGA: mówi o wersji 1.72.16): `docs\OPENBROWSER_LITE_HANDOFF_ODTWORZENIE_2026-10-21_ChatGPT.md`.
- Reguły: `docs\agent\READ_BEFORE_WORK_new.md`, `current-state_new.md`, `development-rules_new.md`, `coding-invariants_new.md`, `documentation-update-checklist_new.md`, `commit-rules_new.md`, `folder-policy_new.md`; obserwacje `docs\feedback\field-notes_new.md`.
- Historyczny, zdecydowanie zbyt szeroki backlog: `docs\design\openbrowser-master-roadmap-v3-2026-10-19.md` (86 pozycji). NIE traktować jako planu 86 równoległych prac.

**PBOX1 (faktycznie uruchamiana produkcja i telemetria)**:
- Windows proces `BridgeBrowserAlpha0`, aktualnie zaobserwowany PID8516 po autostarcie Keepalive 14:03:03.
- Katalog diagnostyki: `%LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\diagnostics\`.
- `lite-events-2026-10-22.jsonl` — metadata wybudzeń, UI/renderera, błędów, etapów wysyłki; nie drukować pełnych treści rozmów.
- `resources-2026-10-21.jsonl`, `resources-2026-10-22.jsonl` — procesy WebView2, privateMiB, CPU i host, znaczniki UTC.
- `lite-keepalive-status.json` — informacja o ostatnim działaniu skryptu Keepalive, NIE healthcheck UI.
- `lite-pending-update-status.json` — istniejący stan update (nie interpretować jako udanego deploy bez SHA i weryfikacji produktu).
- Zadanie Windows `Open Browser Lite Keepalive` (historycznie co 5 minut). Nie tworzyć ani nie przerabiać zadań podczas bieżącego handoffu.
- DPAPI profil i harmonogramy zawierają prywatne dane. Nie wypisywać promptów, loginów, tokenów, pełnych URL czatów ani sekretów do dokumentu lub logu.

## 3. Weryfikacja incydentu zgłoszonego przez właściciela — BEZ NAPRAWY

- **14:02:59 CEST**: read-only `Get-Process BridgeBrowserAlpha0` nie zwrócił procesu; równocześnie zadanie Keepalive miało last result0. Faktyczny stan aplikacji był więc inny niż pozorny „sukces” harmonogramu.
- **14:03:03 CEST**: zastany Windows Keepalive bez udziału autora audytu wystartował proces **PID8516**, co potwierdza `lite-keepalive-status.json` z `state=launched`.
- **14:06:21 CEST**: PID8516 `Responding=False`; tytuł okna nadal „Open Browser Lite | Generowanie | v1.72.21”. Logi nowego PID zawierały serię `web_backend_response endpoint=conversation HTTP200`, `ui_heartbeat` z przerwami 39 i 46 sekund; to **nie dowodzi prawidłowego wyrenderowania UI**.
- **14:08:20 CEST**: Keepalive odnotował `state=running, existingProcesses=1`; nie naprawia on sam zawiśniętego UI.
- **14:10:24 CEST**: PID8516 `Responding=True`, ten sam tytuł „Generowanie” i wersja1.72.21. Nie ma potwierdzenia widocznej odpowiedzi ani sprawnego uruchomienia w ocenie właściciela. To zmienny stan, możliwe przeciążenie/blokowanie wątku UI, ale przyczyna NIE została ustalona.
- Wcześniejsza produkcja PID8216, v1.72.21 działała wiele godzin. Ostatni zapisany szczegółowy checkpoint o12:14 CEST mówił o foreground wake #37, następnie brak aktywnego procesu o14:02. Brak weryfikacji dokładnego powodu zakończenia poprzedniego PID (nie utożsamiać go automatycznie z crash).
- Ostatnie dostępne eventy po restarcie obejmowały `archive_probe_timeout`, `high_guard_probe_timeout`, `response_probe_delayed`, `dom_probe_timeout` i `ui_heartbeat`; to poszlaki przeciążenia/opóźnień, nie potwierdzona przyczyna. W audycie NIE wykonano restartu, reinstalacji, terminacji PID, `Stop-Process`, edycji harmonogramów, stron, profilu ani sekretów.
- Praktyczna ocena: **incydent dostępności UI OPEN**; kolejny czat ma zacząć od read-only diagnostyki aktualnego GUI/PID, ale nie działać wbrew decyzji właściciela o odłożeniu naprawy. W razie rozpoczęcia naprawy później: najpierw ustalić dostępność okna, proces/CPU/UI heartbeat, autostart, eventy Windows (Application Error/.NET Runtime) i czas pomiędzy uruchomieniem a hangiem. Nie wyciągać wniosków z samego `Responding` lub HTTP200.

## 4. Co zrobiono — najważniejsze etapy techniczne (wersje źródłowe)

Wszystkie wersje poniżej po **v1.72.21** to zmiany **SOURCE/TEST na SatanAMD**, dopóki nie istnieje osobny, zweryfikowany protokół wdrożenia na PBOX1.

### Fundament / autopraca (21 października i wcześniejsze)
- Jedno okno z foreground ChatGPT i jedno współdzielone WebView2 do czatów background. Lokalny rejestr harmonogramów zaszyfrowany DPAPI. Jedna instancja nadzorcy chroniona mutexem; inne okna nie powinny tworzyć równoległych schedulerów.
- Keepalive PBOX1 uruchamia aplikację w zalogowanej sesji, jeżeli nie widzi procesu, bez domyślnego zabijania istniejącego. Autosync SATANAMD obsługuje synchronizację Git bez ręcznych commit/push.
- Rejestrowanie metadanych `multi_wake_send`, `response_generation_transition`, `response_error_transition`, `dom_status`, próbek RAM/CPU, faz kliknięcia i unikalnych receipt. `receiptConfirmed=true` to widoczna wiadomość USER, nie dowód odpowiedzi.
- Historyczny bug background: poprawny URL/edytor, ale `main` bez żadnych tur ChatGPT; nie wolno zastąpić go wysyłką „w ciemno”.
- Próby naturalnego foreground samowybudzenia były powtarzalne: 37 opisanych wysłań do tej jednej rozmowy do12:07:41 CEST 22.10. Nie ma certyfikatu 5–10 równoległych logowanych czatów background.

### Bezpieczeństwo wysyłki, URL, ACK i przedwczesnych ponowień (v1.72.22–1.72.48)
- v1.72.22–32: m.in. ograniczenia liczby skryptów/rozmiaru historii, single worker, identyfikacja kontekstu po URL i main, uniknięcie ślepego reuse, ograniczone 25s budżety odczytów, 4,2s ACK read-only, backpressure renderer i testy dwóch rozmów na izolowanej lokalnej fixture.
- v1.72.33–36: ograniczona retencja metadata/references w JS sender; ACK unikalnego 32-hex receipt tylko w USER bubble bieżącego `main`; hard DOM prune OFF nie ma timerów JS; niepełny ACK nie powoduje ponowienia kliku.
- v1.72.37: opóźnione USER ACK po kliknięciu → wyłącznie kolejne **odczyty**, bez kolejnych kliknięć. v1.72.38: usunięcie zbędnego JS interwału DOM wobec 30s monitora WinForms. v1.72.39: sprawiedliwy wybór najstarszego due czatu, foreground preferowany tylko w tolerancji1min.
- v1.72.40: JS `sendDirect` z `expectedPath` blokuje wysyłkę po SPA na inny czat już przed edycją. v1.72.41: `_workerRouteEpoch` i stabilność historii po asynchronicznym JS, w tym A→B→A. v1.72.42: po25s niezhydratowanej powłoki worker aktywuje narastający cooldown 30–300s, bez cyklicznego męczenia renderera.
- v1.72.43: dwa snapshoty assistant preflight oddzielone1,5s wymagają stabilnego URL+epoch zarówno dla foreground, jak i worker. v1.72.44–45: odczyt odpowiedzi, Stop, tool-running i alertów w aktywnym `main`; sidebar preview nie może udawać odpowiedzi lub czynnego narzędzia. v1.72.46: recovery własnego szkicu `[OB-...]` otrzymuje exact target; `already_attempted` nie oznacza pewnie niewysłane.
- v1.72.47: `submitOwnDraft` zapisuje `attempted` PRZED potencjalnym kliknięciem, re-click tego samego receipt odrzucany; DPAPI `LastState=own_draft_clicked_unconfirmed` przeżywa restarty i odroczenia. v1.72.48: po niepewnym kliknięciu wznowienie możliwe tylko po nowej, dwukrotnie stabilnej odpowiedzi o innym SHA; brak zapamiętanego poprzedniego hash → fail-closed. Nie zamieniać tej blokady w automatyczny dubel.

### Dalsze poprawki worker, completion i wydajności (v1.72.49–1.72.58)
- v1.72.49: WinForms 60s raport hard DOM prune przestaje działać, gdy tryb OFF; start tylko przez ręczny ON, stop po OFF.
- v1.72.50: `LiteDomOptimizer` liczy/oznacza elementy tylko w aktywnym `main`, nie sidebar; brak main → `unhydrated`, zero optymalizacji. DIL modern soft CSS pilot nadal OFF.
- v1.72.51: dwie obserwacje assistant muszą mieć tę samą strukturę `TurnCount`, `UserNodeCount`, `AssistantNodeCount`, `LastAssistantChars`; same identyczne Text nie wystarcza. SHA porównywany case-insensitive.
- v1.72.52: terminalny, dwukrotnie potwierdzony marker kompletnego planu może zamknąć harmonogram mimo starego unconfirmed click, bez kolejnego wysyłania; zwykła odpowiedź nadal ma trwałą bramkę antyduplikacyjną.
- v1.72.53: `NavigationCompleted.NavigationId` akceptowane tylko po odpowiadającym `NavigationStarting.NavigationId`, nie ze spóźnionej nawigacji A. v1.72.54: `NavigationStarting` dodatkowo weryfikuje exact URI żądanego czatu już po wydaniu Navigate; obcy/poprzedni start nie przypisuje ID.
- v1.72.55: fałszywe przyciski Stop sidebar/top-level i aktywny Stop w MAIN odróżniane przez sender, probe i response signal. v1.72.56: także async USER receipt ACK wymaga exact URL + niezmiennego route epoch przez cały await/pause.
- v1.72.57: `LiteDomOptimizer.busy` ignoruje Stop z sidebar, rozpoznaje rzeczywiście widoczne `Stop generating`/polskie warianty i nie modyfikuje CSS przy generowaniu. v1.72.58: `LiteResumeProbe` używa jednego scoped `main.querySelectorAll`, jednego przebiegu statycznego NodeList i jednego odczytu `innerText` ostatniej odpowiedzi zamiast dodatkowych `Array.from/filter/reverse`.
- Powyższe zmiany ograniczają konkretne ryzyka i koszty. **Nie potwierdzają** w praktyce autowybudzeń realnego ukrytego zalogowanego worker ani spadku zużycia RAM w produkcji.

## 5. Wyniki rzeczywistej weryfikacji na SatanAMD, 22.10 około14:08 CEST

**Nie dotykano PBOX1, nie publikowano kodu na PBOX1; testy wykonywano na SatanAMD.**

| Test / kontrola | Wynik |
|---|---|
| `node --test tests/lite-*.test.cjs` | **22/22 PASS**, exit0 |
| `dotnet build src/BridgeBrowserAlpha0/BridgeBrowserAlpha0.csproj -c Release --no-restore -v:q` | **Exit0; 0 errors; 1 znane ostrzeżenie MSB3277** WindowsBase/WebView2.Wpf |
| `tests/test-lite-resource-trends.ps1` | **24/24 PASS**, exit0, w tym przejście przez północ UTC i HTML dashboard |
| Opublikowany testowy v1.72.58 EXE: `--lite-version-selftest` | exit0 |
| `--lite-chat-wake-selftest` | exit0 |
| `--lite-dev-wake-selftest` | exit0 |
| `--lite-resume-selftest` | exit0 |
| `--lite-transport-selftest` | exit0 |
| `--lite-webview-integration-selftest` | exit0; zgodnie z ostatnim checkpointem **222/222 izolowanych real-WebView2 offline checks PASS** |
| `--lite-renderer-backpressure-selftest` | exit0 |
| `--lite-delivery-selftest` | exit0 |
| `scripts/verify-lite-release-manifest.ps1` dla testowego v1.72.58 | **RELEASE_IDENTITY_OK**, exit0, SHA jak w §0 |
| `scripts/check-agent-docs.ps1` | obowiązkowe pliki istnieją, folder policy passed |
| Pełne `git diff --check` dla całego dirty repo | **Exit2 — istniejące trailing whitespace w starszych modułach** (m.in. `NetworkLogger.cs`, `OpenBridgeEnvelopeObserver.cs`, `PageTap.js`, `ResponseExtractor.cs`, `WebViewMessageHandler.cs`). Ostrzeżenia LF→CRLF; NIE poprawiano ani NIE wykonywano resetu. To nie FAIL jednostkowych testów Lite. |

**Interpretacja**: SOURCE/TEST v1.72.58 ma poprawną kompilację i regresję izolowaną. Brakuje testu online na zalogowanym koncie w niewidocznym shared worker, sprawdzenia GUI właściciela po wdrożeniu i potwierdzenia stabilnej 8h pamięci/DOM nowego wydania. Nie nazywać programu „ukończonym” ani „wdrożonym”.

W ostatnim checkpointcie źródłowym (12:14 CEST) odnotowano `dotnet publish Release` exit0 oraz pełne 8/8 testów EXE i SHA, co audyt potwierdza na istniejącym pakiecie testowym. W ramach bieżącego handoffu wykonano `dotnet build`, **nie** nowy publish/instalację, dlatego nie pomylić etapów.

## 6. Faktyczne wyniki zasobowe — bez fałszywych tez o wycieku

- 8h pomiar na jednej nieprzerwanej produkcyjnej instancji PID8216 starej v1.72.21, z zachowaniem siedmiu tych samych procesów WebView2: 22.10 00:43:01→08:43:01 CEST, **241 próbek**, maksymalny odstęp161s, zero zmian identyfikatorów.
- Suma `privateMiB`: **561,9→1023,3 MiB (+461,4 MiB)**, maksimum **1073,2 MiB**. Historycznie próbki np. 11:53 1207,4MiB/7 WebView2, 12:13 1093,3MiB — widać zmienność, a nie wyłącznie monotoniczny wzrost.
- Nie ustalono przyczyny: długość rozmowy, DOM, wzrost pamięci silnika, cache, typ generowania, chwilowe obciążenie itp. wymagają korelacji. Liczniki pełnego DOM oraz możliwe optymalizacje wprowadzone później istnieją w źródłach, ale nie na LIVE1.72.21. Do porównania potrzebne drugie >=8h po dozwolonym wdrożeniu, te same metodologie PID/privateMiB/CPU i aktywne 5–10 czatów.
- Skrypty analityczne: `scripts/analyze-lite-resource-trends.ps1` obsługuje `-AdditionalPaths`, `scripts/export-lite-resource-dashboard.ps1` — `-AdditionalInputPaths`; 24h w dwóch plikach dziennych UTC nie może być obcięte do jednego dnia. Ostatnie testy skryptów 24/24 PASS.
- **Nie** zaleca się kasowania historii ChatGPT, włączania hard prune, restartowania wszystkich WebView2 ani modyfikacji profilu z powodu samej liczby MiB.

## 7. Stan planu i lista blokad uporządkowana według ryzyka

| ID | Stan faktyczny | Co naprawdę pozostaje |
|---|---|---|
| OBL-0.1 Keepalive | Mechanizm uruchamia proces; 14:03 ponownie uruchomił PID8516 | **Incydent**: proces/heartbeat/task success ≠ zdrowe GUI. Właściciel odłożył naprawę; najpierw diagnostyka read-only |
| OBL-0.2 Własny czat | Harmonogram i naturalne receipts istnieją | Wykrywać zmiany/kontekst bez duplikatów, nie ujawniać URL ani promptów |
| OBL-0.3 Background worker | **BLOCKER P0**: wiele poprawek safety/epoch/history offline; rzeczywista historia w hidden worker niepotwierdzona | Jeden zalogowany worker i realne 5–10 czatów E2E; composer oraz user/assistant w MAIN, exact URL, generowanie, receipt, brak błędnych/wielokrotnych wysyłek |
| OBL-0.4 Release gate | Testowy pakiet SHA i selftesty gotowe | Dozwolone PBOX1 staging/backup DPAPI, idle switch, weryfikacja, rollback; nie obchodzić wcześniejszej odmowy publish |
| OBL-0.5 Naturalny wake foreground | 37 udokumentowanych receipts do JEDNEJ rozmowy | Nie mylić z sukcesami w tle, po utracie GUI ponownie ocenić zdrowie; finał odpowiedzi nie zawsze potwierdzony |
| OBL-0.6 Recovery | Dużo ochrony przed duplicate/cross-chat i resume po restarcie w SOURCE | Prawdziwy end-to-end recovery po realnym procesowym restarcie i przy niepewnej dostawie |
| OBL-1.1 RAM/DOM baseline | **8h STAREJ v1.72.21 GOTOWE** | Nowy build / DOM census i korelacja RAM, CPU, tury, czas odpowiedzi |
| OBL-1.2 Jeden monitor/małe skany | Usunięto zbędne skany JS i timer; backend nadal stary | Pomiar faktycznego CPU/DOM przy włączonej nowej wersji |
| OBL-1.3 Soft DOM content-visibility | Legacy w main obsługiwany; DIL modern pilot OFF | QA na aktualnym zalogowanym DIL: kompozytor, active generation, przewijanie, inne czaty, focus, restore |
| OBL-1.4 Hard prune | Domyślnie OFF i bez timerów | Tylko po realnym pomiarze progu/odtwarzania React, na razie NIE włączać |
| OBL-1.5 Single worker | Architektura shared worker zaimplementowana | Zwalidować jego realną sesję/logowanie/hydration i RAM przy 5–10 harmonogramach |
| OBL-1.6 Soak | Stara wersja: 8h baseline zrobiony | **>=8h NOWEGO wdrożenia** na 2 długich + 3–5 dalszych rozmowach, bez RAM/DOM bez ograniczeń |
| Etap 2 UI | Projekt zakłada jedną wąską belkę, Wybudzenia, RAM/DOM, Diagnostykę | Realny wygląd i ergonomia; obecny interfejs właściciela niedostępny/niestabilny |

**Największe blokery na moment przekazania**: (1) właściciel widzi niepracujący interfejs PBOX1; (2) brak real background 5–10 chatów; (3) SOURCE/TEST v1.72.58 nie jest wdrożony; (4) brak real post-deploy 8h zasobów i DIL QA; (5) dirty worktree nie może być pochopnie czyszczony.

## 8. Git, Autosync, polityka zmian

**Stan na SatanAMD 22.10 około14:03 CEST:** `git status --short --branch` pokazuje `master...origin/master [ahead 27]`, wiele `??` w src/docs/scripts/tests, wiele `R/RM` (źródła starszego OpenBridge przeniesione z `releases/BridgeBrowserAlpha0/src/...` do `src/BridgeBrowserAlpha0`), odrębne istniejące modyfikacje agent-docs, skryptów i testów. Niektóre pliki, w tym sam nowy handoff, są w worktree untracked.

**Kluczowe reguły właściciela:**
1. **ZERO ChatGPT Tasks / przypominajek i nowych Windows scheduled tasks.**
2. **ZERO ręcznych Git commit/push** — istnieje natywna automatyczna synchronizacja. Nie wywoływać obejścia braku jej sukcesu.
3. Nie resetować, `git clean`, nie `checkout --`, nie kasować całego untracked worktree; jest to znaczna część istniejącego projektu. Nie scalać automatycznie wszystkiego w jedną zmianę.
4. Nie zamykać ani nie restartować procesów podczas handoffu; decyzję właściciela o odłożeniu naprawy GUI uszanować.
5. Wszelkie nowe logi i raporty metadata-only, bez sekretów, loginów i tekstów rozmów; istniejące zaszyfrowane DPAPI settings pozostawić nietknięte.
6. Jeśli narzędzie zablokowało publikację na PBOX1, **nie obchodzić odmowy** innymi komendami/narzędziami. Odnotować blokadę, skupić się na dozwolonym lokalnym rozwoju, a zgodę i ścieżkę wdrożenia ustalić formalnie.
7. Odwołać się do `docs/agent/READ_BEFORE_WORK_new.md` i siedmiu wskazanych w nim dokumentów przed zmianą kodu. Do handoffu tylko zaktualizowano dokumenty; w trakcie audytu nie zmieniano żadnej implementacji.

**Kontrola porządku Git**: `git diff --check` całego repo kończył się kodem2 przez liczne historyczne trailing whitespace w obszarze starego OpenBridge, w tym `NetworkLogger.cs`, `OpenBridgeEnvelopeObserver.cs`, `PageTap.js`, `ResponseExtractor.cs`, `WebViewMessageHandler.cs`. To nie test niezawodności Lite i nie uprawnia do masowych zmian formatowania. Jeśli nowy czat potrzebuje czystego gate dla konkretnego zakresu, uruchamiać `git diff --check -- <konkretne_pliki>`, osobno raportując historyczny dług; NIE przepisywać całego repo przy przejęciu.

## 9. Plan pierwszej sesji nowego czatu — bez angażowania właściciela, tylko po upoważnieniu do następnego etapu

**Krok A: przejęcie stanu (tylko odczyt).**
1. Otworzyć **ten** handoff, na SATA najpierw sprawdzić `git status --short --branch`, aktualną `<Version>` w .csproj, najnowsze końcowe sekcje `OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md` i plan. Jeśli pojawiły się świeższe checkpointy, one mają pierwszeństwo nad statusem 1.72.58 z tego dokumentu.
2. Na PBOX1 zweryfikować stan procesu i GUI, używając tylko read-only `Get-Process BridgeBrowserAlpha0`, `Get-ScheduledTaskInfo -TaskName 'Open Browser Lite Keepalive'`, `lite-keepalive-status.json`, ostatnich metadata-only zdarzeń. Stan `Responding=True` jest **niewystarczający**. Nie diagnozować przez restart aplikacji bez osobnego polecenia właściciela.
3. Wyświetlić użytkownikowi w 3–5 zdaniach porównanie: SOURCE/TEST vs TEST candidate vs STAGED na docelowej maszynie vs LIVE, faktyczny wynik GUI i najważniejszy blocker. Nie nazywać starych checkpointów v1.72.16 ani testowych v1.72.58 „produkcją”.
4. Ustalić, czy właściciel chce teraz badać/naprawiać PBOX1. Bieżąca instrukcja brzmi **NIE**, zatem wolno tylko raportować dostępny stan i kontynuować dozwolone, niezależne prace nad źródłami na SatanAMD.
5. Nie uruchamiać wysyłek testowych do innych czatów bez konkretnego, bezpiecznego scenariusza i wyraźnej podstawy w obowiązującym planie; dotychczasowe 37 naturalnych foreground receipts nie dowodzą wieloczatowości.

**Krok B: przygotowanie rzetelnej diagnozy i wydania (gdy właściciel zezwoli, nadal bez bezprawnego bypass).**
1. Osobno ustalić, dlaczego okno może działać jako PID, lecz nie wyświetlać treści (UI heartbeat, WebView2, response_probe_delayed, blokowanie wątku UI, rzeczywiste zdarzenia Windows). Na początku **bez ingerencji**; nie obwiniać automatycznie RAM.
2. Sprawdzić, czy w aktualnej autoryzowanej zalogowanej sesji hidden WebView2 ma historyczne USER/ASSISTANT i turn nodes w MAIN na dwóch wybranych czatach. W razie composer-only + 0 historii wymóg preflight nadal blokuje send; logować **liczby i powód**, nie treść wiadomości.
3. Przed jakimkolwiek wdrożeniem: dozwolony scenariusz testowy, prawidłowy backup zaszyfrowanego rejestru/profilu DPAPI, SHA manifest kandydata, kontrola idle, rollback, wersja w tytule procesu i diagnozowalna ścieżka odtworzenia. Starej odmowy publish nie omijać; w razie braku dozwolonej drogi **raportować blocker i nie wdrażać**.
4. Dopiero po sprawnym release: realne 5–10 harmonogramów, 2 długie czaty, faktyczny USER ACK/odpowiedź, brak duplikatów/mylonych czatów i ≥8h zasobów (WebView2 private, host, CPU, DOM census, frontend performance), osobno od starego baseline.
5. Po wynikach DIL real-site QA rozważyć soft CSS. Hard DOM prune wciąż wyłączone, chyba że istnieją pomiary i pozytywny test React restoration. UI etap2 zostawić po stabilnej samokontynuacji i RAM.

**Krok C: checkpointy i bezpieczeństwo przejęcia.**
Po każdej zmianie dopisać krótki rekord w `docs/OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md`: data i strefa, numery SOURCE/TEST/STAGED/LIVE, dokładny test/komenda/exit code, real-world limitation, PID, SHA, źródłowy plik zmiany i kolejny krok. Zaktualizować `OPENBROWSER_LITE_EXECUTION_PLAN`, `docs/agent/current-state_new.md` oraz `docs/feedback/field-notes_new.md` w zakresie realnego wpływu. Zero manual commit/push/Tasks.

## 10. Bezpieczne komendy kontrolne dla nowego czatu (PowerShell; tylko kiedy potrzebne)

W przykładach stosować najpierw **read-only**, bez dumpowania payloadów z logów. Nie używać `Stop-Process`, `Restart-Service`, `Start-ScheduledTask`, `git reset/clean`.

```powershell
# SatanAMD
$r = 'D:\projects\open-browser'
Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz'
git -C $r status --short --branch
Select-String "$r\src\BridgeBrowserAlpha0\BridgeBrowserAlpha0.csproj" -Pattern '<Version>'
Get-Content "$r\docs\OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md" -Tail 45
Get-Content "$r\docs\OPENBROWSER_LITE_EXECUTION_PLAN_2026-10-21.md" -TotalCount 16
Get-ScheduledTaskInfo -TaskName 'Autosync repozytoriów'

# PBOX1: odczyt. Nie zmieniać stanu aplikacji.
Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz'
Get-Process BridgeBrowserAlpha0 -ErrorAction SilentlyContinue |
  Select-Object Id, Responding, StartTime, MainWindowTitle
Get-ScheduledTaskInfo -TaskName 'Open Browser Lite Keepalive'
$d = Join-Path $env:LOCALAPPDATA 'OpenBrowser\ChatGPT-Lite\diagnostics'
Get-Content (Join-Path $d 'lite-keepalive-status.json') -Raw
Get-ChildItem $d -Filter 'lite-events-*.jsonl' |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 2 Name,LastWriteTime,Length
```

**Testy na SatanAMD bez wydawania ani restartowania PBOX1** (kandydat v1.72.58 może się zmienić — zawsze sprawdzić aktualną wersję i ścieżkę!):

```powershell
$r = 'D:\projects\open-browser'
$tests = Get-ChildItem "$r\tests" -Filter 'lite-*.test.cjs' -File |
  Select-Object -ExpandProperty FullName
node --test @tests
dotnet build "$r\src\BridgeBrowserAlpha0\BridgeBrowserAlpha0.csproj" -c Release --no-restore -v:q
& "$r\tests\test-lite-resource-trends.ps1"
$exe = Join-Path $env:TEMP 'OpenBrowserLiteCandidate_v1.72.58\BridgeBrowserAlpha0.exe'
foreach ($f in @('--lite-version-selftest','--lite-chat-wake-selftest',
 '--lite-dev-wake-selftest','--lite-resume-selftest','--lite-transport-selftest',
 '--lite-webview-integration-selftest','--lite-renderer-backpressure-selftest',
 '--lite-delivery-selftest')) {
  $p = Start-Process $exe -ArgumentList $f -PassThru -Wait
  "$f EXIT=$($p.ExitCode)"
}
```

## 11. Tekst pierwszej wiadomości do nowego czatu (do skopiowania)

> Przejmij projekt Open Browser Lite. Najpierw odczytaj na SatanAMD `D:\projects\open-browser\docs\OPENBROWSER_LITE_HANDOFF_2026-10-22_AUDYT_I_PRZEKAZANIE.md`, potem aktualny `docs\OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md`, `docs\OPENBROWSER_LITE_EXECUTION_PLAN_2026-10-21.md` i `docs\agent\READ_BEFORE_WORK_new.md`. Zrób read-only kontrolę SOURCE/TEST/STAGED/LIVE, Git, PBOX1/Keepalive/zdarzeń. PBOX1 miał niedziałający interfejs; właściciel na razie NIE zleca naprawy, reinstalacji, restartowania lub wysyłania dodatkowych wiadomości. Nie twórz ChatGPT Tasks ani nowych Scheduled Tasks, nie rób ręcznych commit/push, nie czyść dirty repo i nie obchodź odmowy narzędziowej publish. Daj mi krótkie ustalenia i proponowaną kolejność dalszych bezpiecznych działań. Jeśli wolno, kontynuuj wyłącznie niezależne zadania źródłowe z testami i checkpointem. Priorytet: sprawne realne wybudzenia 5–10 rozmów, potem RAM/CPU/DOM; UI na końcu. Oddzielaj wynik testów offline od produkcji.

## 12. Granice wiarygodności i decyzja końcowa

**Potwierdzone:** na SatanAMD jest SOURCE 1.72.58 z kompletnymi testami; istnieje odrębny testowy EXE i prawidłowy manifest SHA; PBOX1 uruchamia(ł) starszą produkcję1.72.21 i sam zareagował przez Keepalive o14:03; użytkownik zgłasza nieużywalny UI; 37 foreground receipt do12:07; 8h baseline starej produkcji pokazuje wzrost private RAM; pełny Git worktree jest brudny i szeroki.

**Niepotwierdzone:** że PBOX1 teraz działa z perspektywy właściciela, że 1.72.58 została wdrożona, że background worker prawidłowo hydratuje historie realnie zalogowanych czatów, że 5–10 rozmów potrafi niezawodnie budzić się bez mylenia kontekstu, że nowe DOM/CSS zmniejsza RAM produkcyjnie, że projekt jest zakończony. Nie wiadomo, czy przyczyną bieżącego braku GUI jest renderer, środowisko, pamięć, UI-thread, konto ChatGPT, błąd backendu lub inna przyczyna.

**Stan projektu: ACTIVE / HANDOFF READY / PRODUKCJA ZGŁOSZONA JAKO NIESPRAWNA / NIE NAPRAWIANO.** Kryteria `[OPENBROWSER-PLAN-COMPLETE]` **nie są spełnione**. Nie używać tego znacznika w odpowiedzi ani nie wstrzymywać przyszłych prac na tej podstawie.

**Koniec handoffu.**
