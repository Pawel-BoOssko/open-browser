# DeepSeek Handoff - Open Browser / OpenBridge

Ten dokument jest przeznaczony dla kolejnego modelu wykonawczego (np. DeepSeek w środowisku Cloud Code), który przejmuje pracę nad projektem **Open Browser (wcześniej Bridge Browser)**. Został przygotowany przez poprzedniego agenta (Antigravity).

## Idea Projektu
Open Browser to celowo wyizolowana przeglądarka internetowa z protokołem OpenBridge, pozwalająca modelom LLM pracującym w chat interfejsie w przeglądarce, manipulować systemem użytkownika w sposób nadzorowany, na warunkach agentowych. Obejmuje pasywny zrzut zdarzeń przeglądarki i wstrzykiwanie specjalnych kopert w treść generowaną przez LLM.

## Zatwierdzona Architektura Warstw
Projekt został odchudzony z przestarzałych "helperów" i monolitów. Posiada twarde granice oddzielające moduły.
- **UI Shell**: WinForms (tylko layout, obsługa przycisków, proste okno WebView).
- **Tab Runtime**: Miejsce uruchomienia WebVew2, PageTap.js i trzymania stanu karty (`BrowserTabRuntime`).
- **Response Extraction**: Logika analizująca przychodzące wiadomości z przeglądarki pod kątem odpowiedzi LLM (tzw. "Message Frames"). Składa rozbite streamy.
- **OpenBridge Parser**: Wyizolowana, testowalna, bezstanowa logika parsująca koperty i polecenia bez dotykania infrastruktury. (Obecnie: tylko parsowanie/logowanie pasywne).
- **Host (PRZYSZŁOŚĆ)**: System weryfikacji i rutowania komend (nieistniejący na chwilę obecną).

### Granica UI shell ↔ runtime karty
UI nie obsługuje logiki stron czy strumieni – zajmuje się tym wyizolowany runtime w C# przypisany do WebView. Interakcja z modelem jest łapana za pośrednictwem nasłuchiwaczy JavaScript (`PageTap`) i składana przez `ResponseExtractor`.

## Zasady kopert EXEC / RAW
Komunikacja z i do OpenBridge bazuje na rygorystycznym stosowaniu znaczników.
- **Koperty EXEC**: `<<<OPENBRIDGE:EXEC:BEGIN>>>` i `<<<OPENBRIDGE:EXEC:END>>>` zamykają prosty JSON wewnątrz. Na wiadomość dozwolona jest maksymalnie JEDNA koperta wykonywalna.
- **Bloki RAW_PAYLOAD**: Jeśli zawartość skryptów jest trudna do ucieczkowania w JSON-ie (jak Python), jest przekazywana pomiędzy `<<<OPENBRIDGE:RAW_PAYLOAD:BEGIN>>>` oraz `END`. Parser automatycznie zakoduje to do jednowierszowego `Base64` i przekaże do wartości `payload64`.
- Wszystkie koperty są parsowane przy użyciu silnika `System.Text.Json`. Wszelkie odstępstwa od standardów JSON powodują odrzucenie wykonania. Wersją protokołu jest string `"001"`.

## Parser – Aktualny Stan i Czego (jeszcze) NIE robi
- Parser i pasywny observer odczytują komendy w chwili, gdy `ResponseExtractor.Finish()` ogłosi zadowalający koniec strumieniowania.
- Obserwator rzuca wpis do pliku logu. **Nic nie wykonuje**. Hosta, walidacji bezpieczeństwa ani komend nie ma na ten moment w architekturze.

## Lokacje Dokumentów i Testów
- Dokumenty środowiskowe/architektury: `docs/environment/*` i `docs/architecture/additional-architecture-decisions.md` (absolutny priorytet)
- Parser: `releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/OpenBridgeProtocol/`
- Testy: `tests/OpenBridgeProtocolSmoke/`

## Instrukcje Wykonawcze
- **Uruchomienie Builda**:
  `dotnet build D:\projects\open-browser\releases\BridgeBrowserAlpha0\src\BridgeBrowserAlpha0\BridgeBrowserAlpha0.csproj -c Release`
- **Uruchomienie Smoke Testu parsera**:
  `dotnet run --project D:\projects\open-browser\tests\OpenBridgeProtocolSmoke\OpenBridgeProtocolSmoke.csproj -c Release`
- **Runtime Smoke Test** (Powershell - ok. 25s testowania, zbadaj proces czy nie padł i zamknij okno):
  ```powershell
  $exe = "D:\projects\open-browser\releases\BridgeBrowserAlpha0\src\BridgeBrowserAlpha0\bin\Release\net8.0-windows\BridgeBrowserAlpha0.exe"
  $p = Start-Process -FilePath $exe -PassThru
  Start-Sleep -Seconds 25
  $p.CloseMainWindow() | Out-Null
  ```
- **Sprawdzenie Braku Helpera**:
  `git grep -n -i -E "BridgeBrowserHelper|HelperCommandBus|__BRIDGE_BROWSER_HELPER__|helper_command|requests_runtime|HelperExe" -- releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0` (Ma nie zwrócić żadnych wyników C# z `src`).
- **Sprawdzenie Starych Ścieżek**:
  `git grep -n -i "D:\\temp\\bridge-browser"` (może zwrócić jedynie wyniki w dokumentacji markdown, co jest dozwolone, ale w C# niedozwolone).

## Najważniejsze zakazy dla Agenta
1. **NIE** przywracaj starego systemu Helpera/CommandBusa/BridgeBrowserHelpera.
2. **NIE** wychodź poza repozytorium `D:\projects\open-browser`.
3. **NIE** wymyślaj abstrakcyjnej, przyszłej architektury (Cross-Tab, Watchdog, Tab Registry), jeśli nie pracujesz w dedykowanym do tego pakiecie na wyraźne zadanie. Zawsze dostarczaj to co zostało poproszone, i ani grama architektonicznego narzutu więcej.
4. **NIE** wykonuj `git push` ani instalacji paczek NuGet bez wyraźnej zgody użytkownika.
