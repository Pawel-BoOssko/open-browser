# Current Open Browser Core Audit

## 1. Scope and repository state
- **aktualny commit**: `f953086`
- **katalog roboczy**: `D:\projects\open-browser`
- **wynik git status**: repozytorium czyste (brak zmian)
- **wynik builda**: Kompilacja powiodła się (0 błędów, 1 ostrzeżenie MSB3277 o konflikcie WindowsBase)

## 2. Active project structure
| path | role | key files | notes |
| --- | --- | --- | --- |
| `releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0` | Main application C# core | `MainForm.cs`, `PageTap.cs`, `ResponseExtractor.cs` | Contains WinForms UI, WebView initialization, stream extraction, and logging. |
| `releases/BridgeBrowserAlpha0/modules` | Module versions / Current | `conversation-trimmer/` | Local directory for module loading logic. |
| `modules` | Module source | `conversation-trimmer/` | Source tree for injected modules. |
| `config` | Configuration | `appsettings.example.json` | Only an example config is present. |

## 3. Active components
| component | file | current responsibility | future layer candidate | comments |
| --- | --- | --- | --- | --- |
| **Program** | `Program.cs` | Entry point | Host / Shared | Standard WinForms initialization. |
| **MainForm** | `MainForm.cs` | UI, WebView lifecycle, log management, stream handling | UI / Mixed | Massive class. Mixes UI controls with stream handling (`OnPageMessage`) and orchestrates other components. |
| **PageTap** | `PageTap.cs` | Injectable JS script | Card process (Injected) | Hardcoded inline JS. Intercepts `fetch`, `XHR`, `WebSocket`, `EventSource`. Calls module. |
| **ResponseExtractor** | `ResponseExtractor.cs` | Stream parsing (JSON/SSE) | Card process / Shared | Highly specialized logic for parsing ChatGPT chunked responses and delta updates. |
| **NetworkLogger** | `NetworkLogger.cs` | CDP network interception | Diagnostics | Subscribes to DevTools Protocol to log requests/responses independently of PageTap. |
| **BridgeBrowserModuleManager** | `BridgeBrowserModuleManager.cs` | JS module loading | Host / Card process | Manages versions and loads `conversation-trimmer.js` into WebView via CDP. |
| **AppPaths** | `AppPaths.cs` | Path resolution | Shared | Defines paths for logs, extracted files, profile, and modules. |
| **LogWriter** | `LogWriter.cs` | Logging system | Diagnostics / Shared | Handles structured JSON NDJSON logging on disk. |
| **Trimmer** | `conversation-trimmer.js` | Modifying responses | Card process (Injected) | Intercepts ChatGPT fetch requests and modifies response payload. |
| **Project** | `BridgeBrowserAlpha0.csproj` | Build configuration | N/A | Builds to `net8.0-windows`, depends on `WebView2.Wpf`. |

## 4. Runtime flow
1. **Start aplikacji**: `Program.cs` uruchamia `MainForm`.
2. **Inicjalizacja WebView2**: Tworzone jest środowisko (`CoreWebView2Environment`), włączane są DevTools i obsługa WebMessage.
3. **Wstrzykiwanie PageTap**: `AddScriptToExecuteOnDocumentCreatedAsync` wstrzykuje zawartość `PageTap.Script` by script działał od razu na nowej stronie.
4. **Ładowanie modułów JS**: `BridgeBrowserModuleManager` ładuje najnowszą wersję `conversation-trimmer.js` by był dostępny globalnie.
5. **Start diagnostyki**: `NetworkLogger` włącza rejestrowanie przez CDP. Nawigacja do `chatgpt.com`.
6. **Przechwytywanie fetch/SSE/delt**: `PageTap` w oknie przeglądarki nadpisuje natywne metody i nasłuchuje zdarzeń. Jeśli to strumień rozmowy, wywoływany jest trimmer.
7. **Przekazanie przez postMessage**: Zdarzenia stron i przechwycone payloady wysyłane są przez `chrome.webview.postMessage`.
8. **Obsługa w MainForm**: `OnPageMessage` parsuje przychodzący JSON.
9. **Rola ResponseExtractor**: Payloady tekstowe trafiają do `ResponseExtractor.AddRaw`, który buforuje SSE i klei ramki wiadomości asystenta.
10. **Logowanie**: Całość jest non-stop logowana na dysk przez `LogWriter`.
11. **Build**: Prosta kompilacja Release na `net8.0-windows`.

## 5. Stream and message capture
`PageTap` wstrzykuje proxy (nadpisanie natywnych Web API) na początku życia strony. Interceptuje `fetch`, przesyłając do modyfikacji przez wstrzyknięty moduł, po czym całą treść (`chunk`, `responseText`, `data`) wysyła z powrotem do hosta C# przez `chrome.webview.postMessage`. W C# wiadomości te parsuje `MainForm.OnPageMessage` i deleguje treść jako surowe stringi do `ResponseExtractor`. Ekstraktor implementuje logikę sklejania `data:` (Server-Sent Events) i nakładania operacji patch/replace ze złożonego JSONa na aktualny obiekt `AssistantMessageFrame`.

## 6. Module loading and trimmer
`BridgeBrowserModuleManager` zarządza wersjami skryptów JS. Wyszukuje najnowszy `conversation-trimmer` w strukturze plików, kopiuje jako `current` i odpala w WebView2 przez `ExecuteScriptAsync`. Moduł sam rejestruje się w `window.__BRIDGE_BROWSER_MODULES__`. W ten sposób `PageTap` może w locie wołać funkcję `trimConversationResponseText` zanim zwróci Promise do strony docelowej.

## 7. Diagnostics and logging
`NetworkLogger` omija w ogóle stronę i komunikuje się z przeglądarką z użyciem Chrome DevTools Protocol (`Network.enable`). Zapisuje czyste zdarzenia sieciowe (włączając w to pobieranie body żądania) podając równoległy ciąg informacyjny do tego co zgłasza `PageTap`. `LogWriter` zapewnia scentralizowane logowanie w formacie JSONL ze stemplami czasowymi i identyfikatorami runów i udostępnia pliki dla diagnostyki UI. `AppPaths` gwarantuje spójne mapowanie tych katalogów.

## 8. Helper removal verification
| searched term | result in active src | result elsewhere | conclusion |
| --- | --- | --- | --- |
| BridgeBrowserHelper | 0 | 0 | Usunięto (nieaktywny) |
| HelperCommandBus | 0 | 0 | Usunięto (nieaktywny) |
| \_\_BRIDGE_BROWSER_HELPER\_\_ | 0 | 0 | Usunięto (nieaktywny) |
| helper_command | 0 | 0 | Usunięto (nieaktywny) |
| requests_runtime | 0 | 0 | Usunięto (nieaktywny) |
| HelperExe | 0 | 0 | Usunięto (nieaktywny) |

## 9. Responsibility boundaries
Architektura w `MainForm.cs` jest mocno wymieszana. Główna forma obsługuje UI (przyciski), ale też proces i cykl życia WebView2, włącznie z parsowaniem JSON z wiadomości WebMessage i ręcznym zlecaniem ekstrakcji odpowiedzi. Nie ma jawnej granicy między *logiką UI*, *logiką domeny/karty* (stream handling) i koordynacją strumieni diagnostycznych. Diagnostyka (NetworkLogger) jest wpięta ad-hoc po uruchomieniu przeglądarki.

## 10. Missing architecture pieces
- **Host**: Brak bytu orkiestrującego karty na zewnątrz UI.
- **tab registry**: Aplikacja obsługuje obecnie twardo 1 kontrolkę WebView na formularzu, bez wsparcia na wiele kart.
- **executor router**: Wiadomości ze strony spływają do jednego `OnPageMessage` gdzie są ręcznie sprawdzane if-ami.
- **frame parser**: Rozwiązany prowizorycznie wewnątrz monolitu `ResponseExtractor`.
- **state machine**: Skrypt opiera się na kolejności logów, brak formalnej maszyny stanów określającej etap ekstrakcji czy loadingu.
- **command format**: Brak zunifikowanego formatu komend i eventów (poleganie na ad-hoc polach `eventType` i JSON.Parse).
- **cross-tab**: Całkowity brak infrastruktury do wymiany danych między kartami.
- **watchdog**: Brak mechanizmu odzyskiwania stanu po awarii WebView2.
- **self-discovery**: Brak zautomatyzowanego ładowania modułów lub weryfikacji ich API (na razie tylko try catch).

## 11. Risks
| risk | severity | why it matters | suggested mitigation later |
| --- | --- | --- | --- |
| Zależność od WinForms/WebView2 | Medium | Tworzy mocny "vendor lock-in" na środowisko Windows UI i określoną implementację. | Abstrahowanie obsługi widoku/kontrolki za interfejsem IBrowserView. |
| Inline JS w `PageTap.cs` | Medium | Kod źródłowy jest jako gigantyczny C# raw string, utrudnia tooling i linting. | Wydzielenie do pliku *.js i ładowanie jak w ModuleManager. |
| Zmieszanie UI i logiki z kartą | High | Utrudnia przejście na aplikację wielokartową (tab registry) i pisanie testów jednostkowych. | Ekstrakcja logiki message handlera do warstwy niezależnej od UI. |
| Podwójna diagnostyka | Low | Podwójny ruch sieciowy logowany przez `PageTap` i `NetworkLogger` zużywa I/O. | Wyraźny podział: PageTap -> biznes logic, NetworkLogger -> tylko fallback/debug. |
| Brak testów automat. | High | Logika ResponseExtractora jest złożona i krucha na zmiany w strukturze JSON OpenAI. | Dodanie projektów xUnit z snapshot tests na NDJSON. |

## 12. Recommended next 5 tasks
| task | goal | likely files | done criteria | risk | requires architecture decision |
| --- | --- | --- | --- | --- | --- |
| Extract Message Handler | Wyrzucenie `OnPageMessage` z `MainForm` | `MainForm.cs`, `TabMessageHandler.cs` | MainForm deleguje wszystkie webMessage do klasy dedykowanej. | Low | No |
| Move PageTap JS out | Poprawa DX poprzez dedykowany plik `.js` | `PageTap.cs`, `page-tap.js` | C# czyta JS z dysku/zasobów, a nie z hardcodowanego stringa. | Low | No |
| Abstract WebView Lifecycle | Odseparowanie incjalizacji od WinForms UI | `MainForm.cs`, `BrowserTabManager.cs` | Eventy typu NavigationCompleted zgłaszane są do managera. | Medium | Yes |
| Introduce Tab Registry | Przygotowanie pod architekturę hosta wielokartowego | `MainForm.cs`, `TabRegistry.cs` | Każde WebView ma przydzielone ID i rejestruje się. | Medium | Yes |
| Add Tests for Extractor | Stabilizacja logiki ekstrakcji SSE | `ResponseExtractor.cs`, projekt xUnit | `ResponseExtractor` może przetworzyć stary dump NDJSON w teście. | Low | No |

## 13. Recommended first implementation task
**Extract Message Handler** - Najniżej wiszący owoc, który najszybciej rozluźni "Boską Klasę" (MainForm) nie wymagając trudnych decyzji architektonicznych i odblokowując późniejsze dodawanie routingu komend czy wyodrębniania kart.
