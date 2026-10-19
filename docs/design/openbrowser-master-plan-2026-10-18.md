# Open Browser — plan rozwoju i realizacji v1.0 (2026-10-18)
Status: zaakceptowany zakres przez użytkownika 2026-10-18; plan etapowy, aktualizować DONE/PARTIAL/TODO po każdej weryfikacji.
Dokument źródłowy: docs/design/openbrowser-control-center-analysis-2026-10-18.md; obecna diagnostyka: docs/openbrowser-lite-incident-2026-10-18.md.

## Zasady nienegocjowalne
- Rejestr projektów dynamiczny, bez limitu 7; siedem nazw z rozmowy to stan startowy.
- Jeden projekt: wiele czatów; osobna tożsamość projektu i czatu; właściciel/lead z przejmowaniem roli poprzez rev/lease; brak dwóch głównych zapisujących.
- Nie utożsamiać wygenerowanej odpowiedzi z ukończoną pracą ani z dostarczeniem do UI; nie automatyzować kontynuacji na podstawie pojedynczego heurystycznego sygnału.
- Lokalny WAL, checkpointy i kopie: bez treści poufnych w Git/Autosync, brak przechowywania haseł/cookies/kluczy; zaszyfrowane archiwum prywatnych treści (DPAPI). Backup poza profilem WebView2.
- Model API to oddzielna instancja od webowego ChatGPT; najpierw reguły deterministyczne, model tylko do niejednoznacznych decyzji, limity kosztów i jawna autoryzacja działań.
- Telemetria minimalna, limit retencji, nie powielać sieciowych ciał odpowiedzi; osobno OS, WebView2, DOM, backend, zadania i projekty.
- Nie agresywnie usuwać węzłów React/DOM bez przywracalnego lokalnego snapshotu i testów; nie przycinać network JSON w Lite automatycznie.
- Nie nadpisywać cudzych zmian; rozpocząć od git status, używać LC MultiHost do system commands i RDC do edycji; bez ręcznego push, Autosync istniejącym zadaniem.
- Każdy pakiet: kod, test, dokumentacja, backup/rollback, instalacja osobna wersja, weryfikacja lokalnego JSONL i widoku. Aktualizować ten plik często.

## Audyt decyzji — dlaczego plan różni się od prostego „włącz wszystko”
- Istniejący conversation-trimmer core-v06 zachowuje 40 renderowalnych wiadomości w zmodyfikowanej kopii JSON mapping + wykonuje auto reload po >100 turach heurystycznie idle. To NIE jest tylko czyszczenie DOM, i może zmieniać prezentowany kontekst. Wykorzystać sprawdzony pomiar liczby tur; nie aktywować automatycznej modyfikacji danych sieciowych i reload bez odrębnego testu.
- Do bezpiecznego ograniczania pracy renderera: najpierw pomiar DOM i CSS `content-visibility: auto` dla starszych tur, bez usuwania węzłów; z możliwością wyłączenia, małym samplingiem i testami. To odciąża layout/paint, ale NIE gwarantuje obniżenia pamięci React ani fizycznego rozmiaru DOM. Dopiero dalsza izolowana próba snapshot+reversible removal po zachowaniu treści (zależna od struktury ChatGPT, wrażliwa na React).
- Zamiast wielu aktywnych WebView2: jeden aktywny widok ChatGPT + natywne WinForms centrum projektów. Oddzielna strona UI nie powinna mnożyć rendererów.
- Modele zewnętrzne traktować jako narzędzie z potwierdzanymi skutkami, nie „samosterujący agent z dostępem do wszystkiego”. Wymagana autoryzacja per projekt/akcja.
- HTTP/API ChatGPT konsumenckiego nie należy traktować jak oficjalnego API do przenoszenia czatów pomiędzy Projects. Lokalny rejestr pozostaje źródłem prawdy.
- Zabezpieczenie kopii odebranych odpowiedzi nie odzyska treści, których przeglądarka nigdy nie otrzymała. Nie odczytujemy ukrytego stanu modelu.

## P0: dowody, baseline i ochrona sesji
- [DONE] Audyt istniejącego Lite na SATANAMD/PBOX1, plików procesu, aktualnej dokumentacji i brudnego Git worktree.
- [DONE] Audyt conversation-trimmer core-v06: transformacja kopii JSON + monitor DOM + heurystyczny reload; Lite go dotąd nie używa.
- [DONE] Microsoft WebView2 ProcessFailed: unresponsive renderer to nie to samo co zawieszony host; nie resetować automatycznie w trakcie generacji.
- [TODO] Uporządkować alarmy dla dostępnej pamięci, commit private bytes, CPU znormalizowany w czasie; sprawdzić na baterii i po restarcie.
- [TODO] Dodawać małe checkpointy pracy i dokładny rejestr blokad.

## P1: bezpieczna diagnostyka strony i optymalizacja DOM
- [TODO] Minimalny script-observer na ChatGPT: counts DOM turns, node size, phase busy/editing, performance timing; nigdy nie przechwytuje sekretów.
- [TODO] CSS content-visibility dla starszych tur z zachowaniem ostatnich i aktywnego/focusa; wyłączalny opt-in w UI, wyniki liczbowe logować bez treści rozmów.
- [TODO] Sprawdzić integrację WebView2, odróżnić JS hang vs host/UI hang; timeout bez restartowania.
- [TODO] Testy niezależne logiki DOM na syntetycznych turach; nie modyfikować mapping sieciowego w tym pakiecie.
- [TODO] Osobna bezpieczna wersja wdrożeniowa na PBOX1, zweryfikować profile/logi/rollback.

## P2: zabezpieczenie treści rozmów
- [TODO] Trwały snapshot tego co widzialne, incremental/append-only journal; atomowe fsync/checkpoint; wersjonowanie i hash.
- [TODO] Bezpieczne i jawne ograniczenie prywatności: DPAPI Windows, folder użytkownika, retencja, wykluczenia Autosync.
- [TODO] Porównanie snapshotów z późniejszym odczytem; oznaczenie complete/partial/unknown; brak automatycznej rekonstrukcji nieodebranych tokenów.
- [TODO] Testy zamknięcia/crasha w środku generacji, profilu WebView2 i rollback.
 
## P3: rejestr i panel projektów
- [TODO] Niezależny od WebView2 lokalny rejestr (SQLite WAL po weryfikacji doboru zależności; lub na start atomowe JSON ze schema migration) Project, Chat, Assignment, Task, Observation, Handoff.
- [TODO] Import 7 obecnych projektów, bez limitu liczby, aktywne/archiwalne; ręczne dodawanie i edycja.
- [TODO] WinForms dashboard projektów z klikanymi kartami, listą czatów i stanem; jeden renderer.
- [TODO] CRUD, walidacja URL/ID, aliasy, historie zmian, testy migracji schematu.

## P4: koordynacja wielu czatów
- [TODO] Role lead/worker/support/observer/retired, rev/lease/fencing i historię właścicieli.
- [TODO] Atomowy handoff: freeze -> flush -> zapis -> walidacja -> nowy lead -> potwierdzenie -> release.
- [TODO] Checkpointy własnych działań kontra deklaracje modelu; rejestr zadań, blokad i dowodów.

## P5: nadzór nad pracą
- [TODO] Życie zadania oddzielone od generowania; brak pętli kontynuuj.
- [TODO] Wznawianie wyłącznie gdy wybrany projekt/chat objęto zarządzaniem, jest niewykonany backlog i brak generacji / narzędzi / blokady.
- [TODO] Ochrona idempotency, limity promptów i kosztów, przejęcie ręczne, testy dublowania.

## P6: model API
- [TODO] Zweryfikować dostępne aktualne identyfikatory modeli i koszty; nie zakładać istnienia „Eve” bez precyzyjnego ID.
- [TODO] Adapter providerów z ustawieniami bez sekretów w repo, budżet per projekt; schemas JSON i ewaluacja błędów.
- [TODO] Model tylko doradza/klasyfikuje; narzędzia wywołuje program po regułach kontroli i autoryzacji.

## P7: MCP / natywne ChatGPT Projects / zaawansowany DOM
- [TODO] Własny serwer MCP register_chat/report_status/list_tasks/checkpoint/claim/release; uwierzytelnienie i no-public-port.
- [TODO] Panel MCP instalacji i odkrywania integracji, bez ręcznego wpisywania kluczy przez model.
- [TODO] Opcjonalna migracja do natywnych ChatGPT Projects, bez zakładania publicznego API konsumenckiego.
- [TODO] Ostrożnie testować agresywną archiwizację DOM po snapshot + odtwarzanie, Chrome/WebView/React regression tests.

## Kryterium ukończenia całości
- Projekt dowolnego rozmiaru, wiele czatów i trwały lead; dowolna liczba równoległych projektów bez mnożenia rendererów.
- Awaria renderer / host / Windows nie powoduje utraty lokalnie utrwalonej treści; brak obietnicy odzysku nieodebranych danych.
- Wyraźnie pokazane potwierdzone wyniki i niepewności; alarmy i bezpieczne rejestrowanie CPU/RAM/DOM; zamknięta ścieżka diagnostyki.
- Automatyka działa dopiero po testach dublowania, stanów długiego tool call, błędów serwera, awarii podczas checkpointu.
- Testy wszystkich komponentów PASS, wdrożenie PBOX1, rollback, Autosync, aktualne instrukcje operatorskie.

## Checkpoint implementacji 2026-10-18 / pakiet A
- [DONE] Kod `src/BridgeBrowserAlpha0/LiteDomOptimizer.js`: analiza liczby tur ChatGPT, nieinwazyjny `content-visibility:auto` z `contain-intrinsic-size` dla starszych tur, bez usuwania rozmów; chroni ostatnie 40 tur, skupiony element i stany busy, opt-in przełączany w toolbar.
- [DONE] Dodano `tests/lite-dom-optimizer.test.cjs`: pięć scenariuszy (poniżej progu, >progu, wyłączanie/włączanie, focus/streaming, nieobsługiwana CSS / obca domena), 5 PASS. Node --check PASS.
- [DONE] Integracja Lite z jednym WebView2: osadzenie skryptu dokumentu, pomiary DOM przez bounded single-flight ExecuteScriptAsync, logi dom_status / dom_probe_timeout, toolbar z ON/OFF.
- [DONE] Podstawowy rejestr `LiteProjectRegistry.cs` i WinForms `LiteProjectsUi.cs`: dowolna liczba projektów, 7 startowych, przypięcie bieżącego adresu ChatGPT, role lead/worker wybierane ręcznie; serializacja atomowa w osobnym `%LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\projects.json`.
- [DONE] SATANAMD dotnet build Debug 0 błędów, 1 istniejące ostrzeżenie MSB3277.
- [TODO] Zrealizować instalację side-by-side na PBOX1 i test po bezpiecznym uruchomieniu; nie przerywać aktualnie aktywnego strumienia czatu.
- [TODO] Uzupełnić oddzielne testy regresji rejestru i scenariuszy nadzoru. Pełnego dashboardu statusu pracy, backlogów i historii NIE należy jeszcze raportować jako ukończonego.
- [BLOCKED] Narzędzie: `functions.exec` -> Remote Desktop Commander `create_directory` oraz planowane `write_file` dla nowego `tests/LiteProjectsSmoke/LiteProjectsSmoke.csproj`. Jedna złożona operacja utworzenia folderu testowego i pliku projektu została zablokowana zabezpieczeniem. Zakres blokady: utworzenie nowego katalogu projektu smoke test; nie potwierdzono wykonania żadnej modyfikacji. Nie powtarzano tej operacji innym narzędziem; nie jest to blokada testów JS ani build C#.

## Checkpoint testów 2026-10-18 / pakiet B
- [DONE] Dodano bezinterfejsowy tryb `--lite-projects-selftest` w `Program.cs` z testem `LiteSelfTest.cs`: bez otwierania WebView, kont użytkownika ani modyfikowania profilu. Sprawdza 7 projektów startowych, ponad 7 projektów, wielu czatów, jeden lead, zapis/odczyt oraz odrzucenie obcego hosta.
- [DONE] SATANAMD: `dotnet build Debug --no-restore` PASS (0 błędów, znane MSB3277); `Start-Process BridgeBrowserAlpha0.exe --lite-projects-selftest -Wait -PassThru` zakończył się kodem 0; Node test DOM 5 PASS.
- [PARTIAL] PBOX1 sklonował kod do rewizji 624fb2f i zbudował w katalogu `%LOCALAPPDATA%\Programs\OpenBrowserChatGPTLite_20261018_projects_dom`; PUBLISH_EXIT 0, JS test 5 PASS. Obecnego procesu przeglądarki NIE zamknięto, ponieważ aktywny użytkownik prowadzi w nim rozmowę. Program jest przygotowany side-by-side, nie raportować nowego dashboardu jako już widocznego.
- [BLOCKED] Próba `functions.exec` zawierająca dwa wywołania `Remote_Desktop_Commander__edit_block` w dokumentach `docs/agent/current-state_new.md` i `docs/agent/architecture-decisions_new.md` została odrzucona przez zabezpieczenia; nie potwierdzono wykonania żadnego edytowania. Zapis w niniejszym planie jest alternatywnym checkpointem merytorycznym, nie powtórzeniem zablokowanej operacji. Czynności systemowych nie wykonywano tym wywołaniem.

## Checkpoint pakiet C — zaszyfrowane przyrostowe kopie
- [DONE] Źródło `LiteSecureArchive.cs`: Windows DPAPI CurrentUser, tylko szyfrogramy .dpapi w katalogu %LOCALAPPDATA%/OpenBrowser/ChatGPT-Lite/archive, zapis append-only odrębnych wersji z czasem i skrótem SHA-256, deduplikacja identycznych wiadomości, retencja 30 dni.
- [DONE] Źródło `LiteConversationSnapshot.js`: wybiera najwyżej dwie ostatnie widoczne wiadomości assistant w otwartym czacie chatgpt.com, nie odczytuje konta / cookies / historii sieciowej. 5 testów Node PASS.
- [DONE] Źródło `LiteConversationCaptureUi.cs` i integracja ChatGptLiteForm: cykliczny odczyt co 12 s, single-flight, timeout renderera nie wywołuje restartu; przycisk wyłączenia kopiowania.
- [DONE] SATANAMD Debug build 0 błędów, Node testy DOM 5 PASS + snapshot 5 PASS. Tryb rejestru project selftest PASS.
- [PARTIAL] Pierwszy eksperymentalny test CryptProtectData/CryptUnprotectData przez PowerShell 7 na SATANAMD wywołał obie funkcje i zwrócił ciphertext 326 bajtów, ale porównanie wywołało błąd skryptu (AsSpan nie jest PowerShell method). Nie uznawać testu round-trip za potwierdzony.
- [BLOCKED] Próba RDC edit_block dodania 5 testów DPAPI do `LiteSelfTest.cs` była jedną małą modyfikacją tekstową, zablokowaną przez OpenAI safety. Nie powtórzono zmiany innym narzędziem.
- [BLOCKED] Skorygowany test DPAPI PS7 przez LC MultiHost SATANAMD wisiał ponad 100 s; `cancel_current_command` zakończył go i odtworzył sesję. Wynik testu nieznany; nie powtarzać identycznej operacji. Sam kod aplikacji nadal nie był wdrożony produkcyjnie na PBOX1.

## Checkpoint pakiet D — ręczny odczyt archiwum
- [DONE] Dodano `LiteArchiveViewer.cs`: ręcznie otwierany lokalny panel 300 najnowszych zaszyfrowanych kopii; odszyfrowanie DPAPI na żądanie, tekst tylko w read-only TextBox i przycisk kopiowania do schowka. Nie wstrzykuje kopii z powrotem do web ChatGPT.
- [DONE] W ChatGptLiteForm.cs przycisk `Archiwum` i `Kopie: włączone/wyłączone` oraz jeden aktywny WebView2. Zapis nie jest uzależniony od API zewnętrznego ani działania chatu narzędzi.
- [DONE] SATANAMD: Node DOM test 5 PASS, Node Snapshot test 5 PASS, dotnet Debug build 0 błędów / 1 historyczne MSB3277, `--lite-projects-selftest` 0.
- [PARTIAL] Szyfrowanie w lokalnym .NET Windows wymaga jeszcze potwierdzenia odczytu kompletnego archiwum użytkownika na PBOX1 po aktywacji wersji; wcześniejsze dwie próby diagnostyczne PS7 były niejednoznaczne/zawieszone. Nie oznaczać end-to-end snapshot decrypt jako PASS.
- [NEXT] Bezpieczny rollout PBOX1 side-by-side z zachowaniem profilu i bez przerywania aktywnego czatu, weryfikacja procesów/logów po naturalnym uruchomieniu wersji.

## Checkpoint PBOX1 2026-10-18 ~14:45 — staged release v2
- [DONE] SATANAMD Autosync push 2bde07e72ebb (branch autosync/SATANAMD) do GitHub.
- [DONE] PBOX1: Git klon czysty przed pull, `git pull --ff-only` 624fb2f -> 2bde07e. Zbudowano wydanie Release `%LOCALAPPDATA%\Programs\OpenBrowserChatGPTLite_20261018_archive_dashboard`. Wyniki: node test DOM 5 PASS, snapshot 5 PASS, dotnet publish Release 0 błędów, aplikacja `--lite-projects-selftest` exit 0, pliki JS dołączone.
- [STAGED, NOT LIVE] Nowy dashboard, CSS DOM, archiwum odpowiedzi i viewer są w tym oddzielnym katalogu. Bieżąca sesja ChatGPT w PBOX1 pozostaje na starej stabilnej wersji `...OpenBrowserChatGPTLite_20261018_telemetry\BridgeBrowserAlpha0.exe`, PID 15860. Nie przerywać tej rozmowy, żeby nie zgubić odpowiedzi.
- [BLOCKED] LC MultiHost PBOX1 `execute_command` - jeden duży krok systemowy: `Set-ScheduledTask` zmiana akcji istniejącego task `OpenBrowser ChatGPT Lite` + COM zapis dwóch skrótów Desktop/Start na nowy exe. Wywołanie trwało >138 s bez odpowiedzi i anulowano przez `cancel_current_command`, które odtworzyło sesję PowerShell. Odczyt późniejszy potwierdził, że harmonogram i skrót Desktop nadal wskazują starą wersję, a PID 15860 pozostał aktywny. NIE deklarować LIVE i nie ponawiać tej samej zablokowanej operacji pod pozorem innego narzędzia.
- [PENDING] Kiedy można to wykonać bez utraty aktywnej odpowiedzi, kontrolowane przełączenie launchera/task i uruchomienie nowej wersji z testem DOM status, projektów oraz DPAPI, przy zachowaniu starego exe jako rollback. Jeżeli operacja nadal wymaga ręcznego działania, przygotować pojedynczy krótki zweryfikowany skrypt.
- [DONE] Utworzono cogodzinne wznowienie prac `Open Browser — rozwój aplikacji`. Ma w każdej iteracji korzystać z tego planu, realizować kod/testy/staging/wdrożenia i aktualizować status; zadanie nie oznacza, że wykonanie tej sesji jest zakończone merytorycznie.

## Zmiana wymagań i decyzja architektoniczna — 2026-10-18 14:50
- Użytkownik chce agresywnego redukowania historycznych tur/DOM: nie potrzebuje oglądać ich po scrollowaniu w Open Browser, historia na serwerze zostaje nienaruszona i jest dostępna przez Firefox.
- **Badanie pochodzenia błędu „Nie udało się wczytać tej odpowiedzi”**: instrumentacja kolejnych etapów (prompt accepted, navigation, renderer alive, JS alive, typy błędów HTTP/transport, widoczna odpowiedź, stabilny lokalny snapshot, stan na ponownym wejściu). Nie da się bez oficjalnego serwerowego potwierdzenia udowodnić wszystkich etapów dostarczania.
- **Tab management**: karty są bytami projektu i rozmowy. Domyślnie 1 aktywny + maksymalnie 1-2 gorące widoki, ograniczone adaptacyjnie presją RAM; pozostałe zaparkowane z URL, notatkami i snapshotem. Nie zabijać streamującego widoku; zatrzymanie nawigacji/zmiana karty nie jest bezpiecznym wznawianiem zadania.
- **Notes inbox**: stałe natywne okno notatek per czat; trwały zapis po każdym edytowaniu, prywatna kopia szyfrowana DPAPI; etap B — sekwencjonowanie informacji Pawła i dostarczanie do właściwego czatu wyłącznie gdy confirmed idle i zabezpieczenie idempotency. Dyktowanie lokalne Windows Win+H z przycisku oraz uprawnienia mikrofonu chatgpt.com WebView2.
- **Icon & taskbar**: własna identyfikacja wizualna oraz skrót w menu Start i możliwość przypięcia do paska Windows. Nie obiecywać, że Windows 11 zezwala na automatyczne przypięcie bez gestu użytkownika; stosować wspierane API i zalecane metody.
- **DOM**: najpierw reprodukcja testów portowanego core-v06, rozdzielić bezpieczny trim historycznych snapshotów od aktualnego renderera i od zapisu danych. Przycinanie starych wiadomości tylko gdy potwierdzono spokój UI; agresywne usuwanie węzłów zarządzanych przez React jest ryzykowne i może prowadzić do niezgodności drzewa. Preferować testowany etap po stronie payload z możliwością wyłączenia/rollbacku, ewentualnie kontrolowany hard-prune starszych tur jako tryb eksperymentalny. Nigdy w środku odpowiedzi.
- **Wymóg rejestru**: główny plik `docs/Rejestr nieudanych komend.md` — istnieje, numeracja OB-01...; każda dalsza nieudana czynność natychmiast zapisywana i oceniana dowodowo.
Status: decyzje architektury zatwierdzone jako kierunek, dopiero poszczególne elementy wdrażane i testowane.

## Checkpoint 2026-10-18 ~15:08 — odpowiedź na dodatkowe wymagania
- [DONE-SOURCE] Główny rejestr błędów: `docs/Rejestr nieudanych komend.md`, wpisy OB-01 do OB-09, rozdziela odmowę zabezpieczeń, timeout, błąd programistyczny i blokadę pliku.
- [DONE-SOURCE] Lokalny per-chat inbox: `LiteChatNotes.cs` (szyfrowane DPAPI pliki + zapis atomowy), `LiteNotesUi.cs` (stały panel po prawej, autozapis 850 ms, oddzielny klucz per URL czatu, kopiowanie „Informacje od Pawła”), dyktowanie użytkownika przyciskiem Win+H bez samoczynnego otwierania mikrofonu, oraz obsługa CoreWebView2PermissionRequested Microphone dla chatgpt.com. Zapis NIE jest automatycznie wysyłany do czatu — wymaga osobnego, bezpiecznego przekaźnika z idempotency.
- [DONE-TEST] `--lite-notes-selftest` PASS (szyfrowanie, izolacja czatów, zapis, zmiana, katalog testowy, bez prywatnych danych).
- [DONE-SOURCE] `LiteResponseSignal.js` + `LiteResponseDiagnostics.cs`: rozpoznaje widoczne statusy `response_load_failed`, `delivery_timeout`, `reasoning_interrupted`, sygnał generowania i wybrane HTTP statusy backend-api, bez zapisania pełnych URL/body. 7 testów JS PASS. Test E2E etapu utraty odpowiedzi nie jest potwierdzony; nie dowodzi stanu serwera.
- [DONE-SOURCE] Agresywne DOM: `LiteDomHardPrune.js` usuwa stare węzły przy >=90 turach, zachowuje 24 końcowe, wstrzymuje gdy generuje, jest draft, zaznaczenie tekstu; przełącznik WinForms `DOM mocne` i logi. 8 testów syntetycznych PASS. **Rzeczywista regresja ChatGPT React i wielogodzinne obciążenie wymagają walidacji, agresywne usuwanie pozostaje eksperymentalne.**
- [DONE-SOURCE] `LiteBrandIcon.cs`: własna błękitna ikona natywna; eksport .ico sprawdzony na SATANAMD (10366 bajtów). `LiteWindowShell.cs`: widoczność w taskbar i ikonka tray z przywróceniem okna.
- [DONE-TEST] 25 testów Node PASS (8 hard prune+7 response+5 DOM CSS+5 snapshot), Debug C# build 0 błędów / historyczne MSB3277, selftests notes, projects, archive, lead all exit 0.
- [TODO] Po synchronizacji PBOX1 wykonaj side-by-side publish do osobnego katalogu, wszystkie testy headless i eksport ikony; nie restartuj na siłę bieżącej aktywnej rozmowy.
- [TODO] Bounded warm tabs, trwała kolejka notatek i idempotentne dostarczenie po potwierdzonym zakończeniu, diagnostyka cyklu błędu od prompt->transport->render->persisted, agresywna optymalizacja snapshot payload po testach portowanego conversation-trimmer.

## Postęp 2026-10-19 07:24, DOM P1
- [DONE-SOURCE/TEST] Migracja legacy -> DIL czyści własne znaczniki `data-ob-lite-offscreen` ze starych wrapperów, bez ingerencji w nowe wiadomości i bez usuwania DOM. Test regresyjny dodany; Node 18/18 PASS, w tym DOM 6/6.
- [PENDING] SOURCE 1.71.0, PBOX1 LIVE 1.70.0. Nadzorca nadal zgłasza `dev_wake_deferred_busy`; brak `dev_wake_ack`. Nie wykonywano restartu ani retry.
- [BLOCKED] Zbiorczy odczyt i test LC MultiHost, próba dopisania do rejestru oraz utworzenia nowego checkpointu przez RDC zostały odrzucone przez zabezpieczenia narzędzi. Szczegółowa przyczyna nieznana. Ten wpis zachowuje zakres blokad; nie ponawiać tych operacji innymi narzędziami.
- [FAILED] LC MultiHost SATANAMD: pojedynczy odczyt `rg -n RevalidateResumeBeforeSendAsync` nie wykonał się, ponieważ `rg` nie jest zainstalowany (`The term 'rg' is not recognized`). Nie była to blokada bezpieczeństwa. Rejestr główny nadal nieprzyjmujący dopisania.
