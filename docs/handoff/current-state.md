# Aktualny stan projektu Open Browser (OpenBridge)

## Core goal (autonomous execution loop)

The LLM works autonomously with the local environment. It sends a command, the command executes, the result feeds back into the LLM conversation, and the LLM acts on that result in its next turn. There is no human in the runtime loop. Architecture: **LLM decides → executor executes → LLM receives the result.**

Open Browser is a local execution architecture for LLMs. It is not a Cloud Code connector. Cloud Code or any CLI tool can be launched through the general PowerShell/command-line executor.

## Najważniejsze fakty:
- **Lokalizacja repo:** `D:\projects\open-browser`
- **Aktywny projekt C#:** `releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0`
- **Nazwa user-facing:** Open Browser (widoczna w UI, tytułach okien itp.)
- **Techniczna nazwa projektu:** `BridgeBrowserAlpha0` (nie zmieniano ze względu na historię i ryzyko rozpadu konfiguracji).

## Zrealizowane kroki i obecny status (Handoff Check):
- `BridgeBrowserHelper` (stary helper i jego infrastruktura) został **trwale usunięty** z aktywnego kodu w `src`.
- Aplikacja **buduje się bez błędów**.
- **Smoke testy działają** (w tym dedykowany projekt dla parsera `OpenBridgeProtocolSmoke`).
- Logowanie zdarzeń NDJSON (`run_*.ndjson`) zostało **ograniczone i zoptymalizowane** - surowe payloady długich JSON-ów lub HTML-i są obcinane do rozsądnych rozmiarów, co zapobiega pęcznieniu plików podczas testów do setek MB.
- Kod źródłowy w C# stał się bardziej modularny dzięki wyodrębnieniu klas z przepastnego `MainForm.cs`:
  - `PageTap.js` został wydzielony jako osobny zasób wbudowany.
  - Kod layoutu UI został wydzielony do `MainForm.Ui.cs`.
  - Obsługa strumieni logiki zdarzeń okna WebVew w C# przeszła do `WebViewMessageHandler.cs`.
  - Cykl życia zakładki/runtime'u znajduje się w `BrowserTabRuntime.cs`.
  - Diagnostyka wydzielona do `DiagnosticsController.cs`.
- **Parser kopert wykonawczych OpenBridge (protokół EXEC/RAW)**:
  - Został dodany w całości do katalogu `OpenBridgeProtocol`.
  - Dodano małą klasę obserwatora `OpenBridgeEnvelopeObserver`.
  - Obserwator jest **pasywnie podłączony** do momentu zakończenia streamowania odpowiedzi przez model (w `ResponseExtractor.Finish()`).
  - Parser/Obserwator jedyne co robią, to **wykrywają kopertę, logują ją do pliku i kończą**. NIE WYKONUJĄ ŻADNYCH KOMEND (Hosta nie ma w kodzie).

## Ostatnie Commity
```
2c785e2 feat: passively detect OpenBridge envelopes in tab runtime
d2ef883 feat: add OpenBridge execution envelope parser
2cdae14 refactor: separate MainForm UI layout
bb61529 docs: add OpenBridge environment documentation skeleton
1319740 docs: update approved OpenBridge architecture decisions
e077119 fix: keep truncated log strings type-stable
5c94271 fix: rename UI and limit noisy runtime logs
458966f fix: align paths and packaging after project move
5e3be67 fix: remove hardcoded legacy project root
d86fc46 chore: remove dead modules/ root directory
d9c7600 Revert "refactor: extract browser tab runtime state"
8ee77fc refactor: extract PageTap JavaScript file
746af6f refactor: extract browser tab runtime state
39bc534 docs: add additional OpenBridge architecture decisions
bc92a7e refactor: extract diagnostics controller
```
