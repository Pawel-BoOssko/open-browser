# Open Browser Lite — analiza przedprojektowa centrum sterowania (2026-10-18)

Status: PROPOZYCJA / PRZED AKCEPTACJĄ ARCHITEKTURY. Audyt i specyfikacja kierunku; **bez implementacji**. Formalne zatwierdzone decyzje są nadal w docs/agent/architecture-decisions_new.md.

## 0. Streszczenie — wymagania właściciela

Open Browser Lite ma ewoluować od pojedynczego WebView2 z ChatGPT do lekkiego centrum zarządzania projektami i czatami. Projekty: Open Browser Lite, Lupus Commander, OpenFill, Networking Radar, Transkrypcja AI, Lupus Commander MultiHost, CourtControl (kontrola sądowa). Więcej niż jeden czat może realizować projekt; rola głównego czatu jest przekazywalna. Właściciel zwykle wydaje polecenie i oczekuje długiej samodzielnej pracy, wraz z kontynuacją kolejnych partii bez ręcznej interwencji. Nie wolno utożsamiać końca jednej odpowiedzi z końcem zadania.

Kluczowa awaria: widoczna przez chwilę odpowiedź może zniknąć i przestać być dostępna również w kontekście ChatGPT. Historyczne ręczne rozwiązanie: handoff do pliku, nowy czat, import handoffu. Nowa aplikacja musi utrwalać postęp i utracone fragmenty poza ChatGPT, wykrywać rozbieżności i wykonywać kontrolowany handoff.

Monitorować zużycie pamięci oraz CPU w kontekście stanu działania, bez a priori założenia wycieku pamięci. Preferencja operacyjna: LC MultiHost do poleceń systemowych; RDC do pracy na plikach (write_file / edit_block) według wcześniejszych instrukcji; Autosync zamiast ręcznego push. Inne potrzeby: późniejsza obsługa instalowania/dodawania MCP; API modelowe Luna i ewentualnie inny model wysokiej klasy i niższy oznaczony w dyktowaniu jako „Eve”/„V” (dokładna identyfikacja wymaga pytania).

## 1. Wyniki audytu źródeł i środowiska

- Repo źródłowe na SATANAMD: D:\projects\open-browser, stan roboczy z istniejącymi zmianami i częściową migracją źródeł, nie resetować ani nie mieszać zmian.
- Sklonowana migawka na PBOX1: C:\Users\pboru\Documents\Projects\open-browser-lite-source, autosync/SATANAMD commit 0fe008728586bb853e7e88d8f8830e08b2c4e46b.
- Instalacja Lite: C:\Users\pboru\AppData\Local\Programs\OpenBrowserChatGPTLite, tryb --chatgpt-lite, odrębny profil %LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\Profile.
- Lite obecnie nie aktywuje: MainForm, OpenBridgeHost, PageTap, ConversationTrimmer, ResponseExtractor, NetworkLogger, DomDumper ani diagnostyki. Nie przechwytuje treści i nie utrwala odpowiedzi.
- W repo istnieją moduły: BrowserTabRuntime.cs (nawigacja, ProcessFailed, status, PageTap, moduły, network), DiagnosticsController.cs, DomDumper.js, BridgeBrowserModuleManager.cs, conversation-trimmer.js.
- ConversationTrimmer core-v06: zachowuje 40 tekstowych wiadomości; obsługuje trymowanie kopii JSON payloadu rozmowy ze spójnością mapowania; monitor liczy DOM elementy conversation-turn, próg 100, poll 600000 ms, cooldown 600000 ms; odświeża gdy heurystyka wskazuje bezczynność. To NIE jest wyłącznie usuwanie elementów DOM i może niebezpiecznie zmieniać prezentowany kontekst. Włączyć wyłącznie po testach i z opt-in/fallback.
- Historyczny błąd: NetworkLogger logował pełne ciała odpowiedzi sieciowych (w tym JS) i app.ndjson osiągnął ~476 MB w kilka godzin; poprawiono logowanie metadanych. Nie powtarzać.
- Firefox PBOX1: profil g9ubntkr.default-release; aktywny ChatGPT Bulk Delete v6.4 (usuwanie historii, nie wykazano trimowania DOM), uBlock Origin i inne rozszerzenia. Wśród aktywnych dodatków nie zidentyfikowano jednoznacznie rozszerzenia DOM cleaner. Jego źródeł należy poszukać w odrębnym, ograniczonym audycie. Bez wykorzystywania Bulk Delete do optymalizacji wydajności.
- Wcześniej preferowany interfejs OpenFill: stały pasek kluczowych technologii/statusów, rozwijane szczegóły architektury; wyraźne statusy zadania, rozróżnienie sukces/partial/failed, brak duplikowania operacji, minimum pytań użytkownikowi. W OpenFill jeden czat + domena = preferowana ponowna obsługa w tej samej karcie, brak samoczynnego zamykania tylko z powodu wieku.
- Istnieją połączone MCP GitHub, LC MultiHost, RDC, OpenFill, Mailroom, Trello; rejestracja Mailroom istnieje, ale nie jest docelowym systemem trwałości projektów.
- 2026-10-18 weryfikacja PBOX1 pokazała działający proces Lite i wiele procesów WebView2; sumy RAM nie dowodzą wycieku.
- Dokumentacja OpenAI: istniejące czaty można przenosić do ChatGPT Projects przez UI, gdy dostępne jest „Move to project”; nie wszystkie czaty kwalifikują się. Przeniesienie zmienia ich kontekst projektu. Nie zakładać oficjalnego API do reorganizowania konsumenckich ChatGPT Projects.
- API OpenAI udostępnia gpt-6-luna (Responses API). Nie potwierdzono modelu o jednoznacznym identyfikatorze „Eve”; potrzebna dokładna nazwa/ID. API modelu nie daje automatycznie dostępu do sesji i historii webowego ChatGPT.

## 2. Model danych i centrum prawdy

Lokalny, trwały rejestr na PBOX1 (propozycja: SQLite WAL z zapisami atomowymi i bezpiecznym backupem). Rozdzielić:
- Project {stable_project_id, title, aliases, description, active, priority, owner, repo, backlog, dashboard_order, native_chatgpt_project_ref_optional};
- Chat {stable_chat_id, canonical_url, platform, project_id, role, lifecycle, created_at, last_seen_at, last_event_at, session_ref_optional, parent_handoff_id_optional};
- Assignment {project_id, chat_id, role: lead/worker/support/observer/retired, effective_at, rev, lease_token};
- Task {task_id, project_id, owner_chat_id, scope, status, deadline, checkpoint, last_action_at, evidence, blocker};
- Observation/Event {immutable_sequence, source, observed_at, chat_id, event_type, state, content_digest, payload_path, confidence};
- Artifact {file_path, sha256, origin_chat_id, related_task, status, version};
- Checkpoint/Handoff {checkpoint_id, project_id, from_chat, to_chat, task_cursor, decisions, completed, in_progress, blocked, links, source_revision, created_at};
- Runtime {webview_id, process_id, phase, browser_version, health, last_signal, resources}, ModelCall {provider/model, purpose, cost, usage, timings}.

Każdy rekord powinien odróżniać potwierdzone dane, deklaracje modelu i hipotezy. Zdarzenia zapisujemy append-only; stan panelu jest wyliczaną projekcją. Niezmienny identyfikator projektu nie jest adresem czatu. URL czatu może się zmienić; wymagane uzgadnianie tożsamości i aliasów, nie duplikowanie projektu.

## 3. Dashboard użytkownika

Strona startowa „Projekty” wyświetla siedem kart. Każda karta pokazuje nazwę, status projektu, ostatnią aktywność, główny czat, liczbę aktywnych/pomocniczych czatów, ostatni checkpoint, liczbę zadań, blokady i stan niezapisanych danych. Kliknięcie otwiera w centralnym WebView2 wybrany czat, bez tworzenia siedmiu stale aktywnych WebView2. Obok: widok listy czatów projektu, ich ról, sesji i historię przekazań.

Stały, cienki pasek: nazwa projektu/czatu, czy generuje/pracuje/czeka, ostatni checkpoint, stan monitoringu, RAM/CPU w kontekście obciążenia, liczba błędów, kontrola „Pauza automatyki”. Rozwijany panel techniczny: zdarzenia, procesy, webview, DOM, handoffy, identyfikatory, diagnostyka, koszty API, MCP. Strona „Wszystkie projekty” musi działać na danych cache lokalnego rejestru, nie na równoczesnych rendererach ChatGPT.

Role: lead = jeden zapisujący/koordynujący (lub kolejka przypisanych prac), worker = zadania rozłączne, support = odczyt/analityka, observer = wyłącznie podgląd, retired/OLD = historia. Wyraźnie pokazuj „co wiadomo / co nie zostało zweryfikowane”, kto ostatnio aktualizował i jakie zadanie jest następne.

## 4. Zapobieganie utracie odpowiedzi

Przed wysłaniem promptu: bezpiecznie utrwal lokalnie tekst polecenia + idempotency token + identyfikator celu, bez zapisywania haseł/tokenów. Podczas odbierania: rejestrować przyrosty wyświetlonej odpowiedzi, role, ID tur, timestampy i sygnały zakończenia; zapis partii w bezpiecznym dzienniku WAL (nie pełen DOM ani sieć na dysku). Po zakończeniu: finalny snapshot odpowiedzi i digest. Po reloadzie ponownie obserwować i porównywać digesty/ID. W razie zniknięcia: alert „widziano lokalnie, brak w historii po reloadzie”, kopia ostatniego zaobserwowanego fragmentu, link do pliku/checkpointu. Utrwalenie widocznego fragmentu nie dowodzi zapisania po stronie OpenAI. Treść, która nie dotarła do WebView2, nie jest odtwarzalna tą metodą. Odpowiedzi można weryfikować przeciwko repo, logom testów i plikom; oświadczenie modelu „zrobiłem” to nie dowód.

Zapisy robić po krótkich zmianach i po zmianie fazy, z ograniczaniem kosztu I/O; szyfrowanie lokalnych treści wrażliwych (CourtControl itd.), polityka retencji, backup poza profilem WebView2 i repo Git. Żadnych surowych rozmów w Autosync/GitHub bez odrębnej decyzji. Handoff generować z trwałych zapisów, nie tylko z ostatniej widocznej odpowiedzi.

## 5. Nadzorowanie i kontynuowanie długiej pracy

Osobny watchdog działa niezależnie od WebView2 i sesji czatu. Nie używa automatycznych przypominajek jako mechanizmu wykonawczego; wykrywa zdarzenia oraz przerwy na podstawie sygnałów i postępów.

Stan czatu: unknown -> loading -> ready -> drafting -> submitted -> generating -> awaiting_tool -> awaiting_user -> idle -> stalled -> handoff_needed -> retired. Stan projektu i jego kolejki zadań jest niezależny od stanu przeglądarki. Nie wznawiać po samym braku tekstu przez 10 sekund; nie mylić długiego tool-calla, tła, CAPTCHA czy błędu UI z zakończeniem. Przed „kontynuuj” sprawdzić: ostatnie zadanie i dowody, czy są niewykonane punkty, stan przycisków i transakcji, czy nie ma aktywnego generowania oraz limitów usługi. Ponowić tylko po potwierdzeniu, że nie tworzy się duplikatu. Dokładne krótkie polecenie może zawierać ID zadania i zakres kolejnego pakietu, a nie tylko słowo „kontynuuj”. Gdy brak pracy, zakończyć, a nie zapętlać.

Każdy projekt ma backlog i checkpoint, czat ma lease/fencing token na akcje. Handoff: freeze przydziału, flush dziennika, zbudowanie pliku, sprawdzenie pliku, rejestracja nowego czatu, przypisanie roli lead, potwierdzenie odbioru, zwolnienie starego lead. Awaria w połowie nie powoduje dwóch leaderów. Stary czat pozostaje dostępny jako archiwum.

## 6. Rejestracja czatów przez MCP

Propozycja lokalnego control-plane API plus oddzielny MCP na PBOX1, dostępny tylko przez uwierzytelniony kanał (np. secure tunnel), nigdy publiczny niechroniony port.
- register_chat: prośba o przypisanie (projekt, URL/ID, rola, tytuł, możliwości, czego dotyczy praca);
- heartbeat/report_status/checkpoint/report_artifact/list_tasks/claim_task/complete_task/release_task/request_handoff;
- list_projects/list_chats/get_project_state/get_handoff; jawne uprawnienia read vs write.
- Rejestrować można też przez panel (checkbox + URL czatu), niezależnie od dostępności MCP. Dane deklarowane przez czat nie stanowią same w sobie weryfikacji tożsamości; wymagane powiązanie z zaobserwowaną sesją i kontrola uprawnień.
- Rejestr trwa niezależnie od obecności MCP w nowym czacie. Brak MCP nie może zatrzymać całego projektu.

## 7. Podłączenie API modeli

Warstwa racjonalnego użycia modeli: zdarzenia deterministyczne klasyfikowane regułami, niejasne przypadki oceniane przez gpt-6-luna (oficjalny identyfikator). Wyższy model (np. gpt-6.1-sol lub gpt-6-astra) wyłącznie przy trudnej analizie; kolejny tani model nazwany przez właściciela „Eve/V” jako opcjonalny provider po identyfikacji dokładnego model ID i kosztów. Model dostaje tylko potrzebny fragment lokalnego stanu, ustrukturyzowany schemat JSON i narzędzia kontrolowane uprawnieniami; weryfikacja wyniku przez aplikację. Wbudowany model nie jest „tym samym czatem” co webowy ChatGPT. API billing ma osobną telemetrię i budżet.

## 8. Projekty natywne ChatGPT

Opcjonalne powiązanie jednego lokalnego projektu z jednym natywnym ChatGPT Project. Migracja istniejących rozmów tylko po weryfikacji uprawnień i dostępności „Move to project”, z zachowaniem URL, stanu, backupu i planem odwrócenia. Zmiana projektu ChatGPT może zmienić instrukcje i pamięć. Nie zakładać publicznego, wspieranego API konsumenckiego do przenoszenia czatów. Lokalny projekt i handoff pozostają źródłem prawdy niezależnie od tej organizacji.

## 9. DOM, wydajność, telemetry i bezpieczeństwo

Należy rozdzielić: (A) wizualny DOM, (B) stan aplikacji JS i pamięć React, (C) rozmowy w backend OpenAI, (D) pamięć i CPU procesów Chromium/WebView2, (E) stan projektu/handoff na dysku. Czyszczenie A nie rozwiąże automatycznie B-D i nie może usuwać bez backupu wiadomości. Mierzyć liczbę tur, węzłów DOM, wielkość DOM, long tasks, interakcje, opóźnienie scroll, fazę generowania, CPU, RAM working set/private bytes, role poszczególnych procesów WebView2 przez GetProcessInfos, liczbę rendererów, odpowiedź na ping z JS, obciążenie przy bezczynności i aktywności. Zbierać krótkie dane agregowane, nie pełne HAR/net payload. Wyznaczyć baseline po 1/10/50/100/500 turach oraz idle vs streaming vs scroll.

Optymalizacje w kolejności: observability -> brak zbędnych aktywnych WebView2 -> wirtualizacja kart panelu i odciążanie nieaktywnych kart -> oszczędne snapshoty -> jawne bezpieczne czyszczenie renderowanych starszych tur -> ew. zaawansowane trymowanie odpowiedzi JSON tylko po szerokich testach z możliwością rollbacku. Nie odświeżać czatu w trakcie generowania/narzędzi ani niesłanego draftu. Utrzymać sekrety poza Git/Autosync; CourtControl i inne dane wrażliwe — retencja, szyfrowanie, nie logować prywatnych treści do publicznego logu.

## 10. Kolejność przyszłej realizacji po akceptacji planu

Faza 0: architektura, testy referencyjne, wymagania i decyzje użytkownika; identyfikacja źródła rozszerzenia Firefox.
Faza 1: lekka telemetria, logi i status endpoint, crash guard, bez tworzenia nowego ciężkiego loggera.
Faza 2: schema SQLite, rejestr 7 projektów, ręczne przypisywanie i dashboard.
Faza 3: obserwacja czatu w WebView2, utrwalanie append-only i detekcja zniknięć.
Faza 4: role wielu czatów, lease, handoffy, walidacja odzyskiwania po awarii.
Faza 5: nadzorca kolejek, automatyczne wznowienie tylko zaakceptowanych zarządzanych czatów, ochrona przed dublem.
Faza 6: model API + heurystyki + monitor wydajności, benchmark.
Faza 7: opcjonalne natywne ChatGPT Projects i optymalizacja DOM.
Faza 8: kreator MCP i integracje rozszerzające.
Każda faza: unit/contract/integration tests, akceptacja wizualna, wdrożenie PBOX1, rollback, Autosync bez ręcznego push, stan pracy zapisywany do plików. Nie zaczynać części poza zatwierdzonym zakresem.

## 11. Otwarte pytanie — wymagane przed implementacją providera

Co dokładnie oznacza wymawiany „Eve”/„V” — jaki dostawca i identyfikator modelu? Do czasu odpowiedzi wpisać jako nieustalony dodatkowy model; podstawową ścieżkę Luna można zaplanować niezależnie.

## 12. Granice gwarancji

Open Browser może gwarantować trwały lokalny zapis tego, co zaobserwował oraz co sam wysłał, gdy storage działa. Nie może gwarantować odzyskania tokenów, które nigdy nie dotarły do klienta, ani odtworzenia prywatnego ukrytego stanu modelu ChatGPT. Nie może utożsamiać lokalnego zakończenia generacji z finalną realizacją wieloetapowego zadania. Nie należy automatycznie wykonywać działań nieodwracalnych lub działań wymagających jawnego zatwierdzenia platformy.

## Źródła zewnętrzne zweryfikowane 2026-10-18

- https://help.openai.com/en/articles/10169521-projects-in-chatgpt
- https://help.openai.com/en/articles/12584461-connecting-custom-mcp-servers-in-chatgpt
- https://developers.openai.com/api/docs/models
- https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2environment.getprocessinfos
- https://help.openai.com/en/articles/7260999-export-your-data-from-chatgpt

Dokument jest analizą/propozycją, nie zobowiązaniem do wdrożenia wszystkich punktów, nie zawiera surowych rozmów, haseł ani kluczy API.
