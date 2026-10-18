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
