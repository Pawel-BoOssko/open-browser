# Dziennik rozwoju

Luźna, chronologiczna lista co się zmieniło. Najnowsze na górze.

---

## 2026-05-18

- **dodane:** wersjonowanie z gita — build number = `git rev-list --count HEAD`, hash = `git rev-parse --short HEAD`, data z kropkami. Wyświetlane w tytule okna.
- **dodane:** human delay (20s baza + rozkład normalny: mean=22s, std=11s, min=0, max=50s). W `SendTextToChatAsync` — obejmuje wynik i błędy.
- **dodane:** `docs/development/` i `docs/runtime/` — nowa struktura dokumentacji.
- **dodane:** `docs/README.md` — indeks docs/ z zasadami.
- **przeniesione:** 10 historycznych plików docs do `old-files/`.
- **zmiana:** uporządkowane `D:\projects\` — stare testowe repo do `old-files/`.

## 2026-05-17 (późne)

- **dodane:** truncation stdout/stderr w `GeneralCommandExecutor` — `MaxOutputChars` (domyślnie 50k), znacznik `[... truncated ...]`.
- **dodane:** pierwsza koperta tylko — parser bierze pierwsze `@@OPENBRIDGE_EXEC_BEGIN@@`, ignoruje resztę.
- **dodane:** feedback błędów do LLM — każda próba koperty dostaje odpowiedź (błąd parsera, nieobsługiwana komenda, pusty prompt, busy). `SendTextToChatAsync` wysyła `[OpenBridge] ...` do czatu.
- **usunięte:** `CommandExecutor`, `CommandExecutorOptions`, `CommandExecutorOptionsLoader`, `CommandExecutorMode` — martwy kod po Cloud Code.
- **usunięte:** `ApproveProcessAsync`, `IsProcessAvailable`, `ProcessAvailableMessage` z `OpenBridgeRuntimeApproval`.
- **zmiana:** `OpenBridgeHost` — executor wymagany w konstruktorze (bez defaultu).
- **zmiana:** `OpenBridgeRuntimeApproval` — przyjmuje `OpenBridgeHost` przez konstruktor.
- **zmiana:** `ApproveDryRunAsync` → `ExecutePendingAsync`.
- **zmiana:** `GeneralCommandExecutor` — walidacja pustego prompta (`PROMPT_EMPTY`).

## 2026-05-17 (wczesne)

- **dodane:** auto-execution — koperta wykryta → PS wykonane → wynik wstrzyknięty do `#prompt-textarea.ProseMirror` → Send kliknięty.
- **dodane:** Message ID dedup — `_observedMessageIds` w `ResponseExtractor.Finish()` zapobiega ponownemu przetwarzaniu tej samej wiadomości.
- **dodane:** observer tylko z nowych ramek — `GetCurrentAnswerTextForFrames(newFrames)` zamiast pełnego `GetCurrentAnswerText()`.
- **usunięte:** cooldown 15s — zastąpiony przez message ID dedup.
- **dodane:** Send button click — szuka `[data-testid='send-button']`, potem `button[aria-label*='Send']`, potem `button svg`.
- **dodane:** ikona (ChatGPT PNG, konwersja przez `Bitmap.GetHicon()`).
- **dodane:** timestamp kompilacji w tytule okna.
- **usunięte:** duplikat `_buildLabel` z paska statusu.
- **zmiana:** markery kopert z `<<<OPENBRIDGE:EXEC:BEGIN>>>` na `@@OPENBRIDGE_EXEC_BEGIN@@` (hard migration).
- **usunięte:** komenda CC (Cloud Code) — tylko PS akceptowane.
- **zmiana:** `IClaudeCodeExecutor` → `IOpenBridgeCommandExecutor`.
- **zmiana:** `ClaudeCodeExecutor` → `CommandExecutor` (później usunięty).
- **dodane:** `GeneralCommandExecutor` — uruchamia dowolny proces przez `System.Diagnostics.Process`.
- **sprzątnięcie UI:** usunięte nieużywane przyciski, panel diagnostyczny, result text selectable.
- **dodane:** `PipelineRawDump` — diagnostyczne zrzuty na każdym etapie pipeline.
- **naprawione:** CDP body routing do ResponseExtractor przez `isCdpConversationBody` bypass.
- **naprawione:** rekurencyjne odpakowanie CDP body (loop do 5).
- **dodane:** `_sawPageStream` gate na CDP dane po WebSocket.

## 2026-05-16 i wcześniej

- Struktura projektu: `MainForm`, `PageTap.js`, `ResponseExtractor`, `NetworkLogger`, `BrowserTabRuntime`, `WebViewMessageHandler`.
- OpenBridge Protocol: `OpenBridgeEnvelopeParser`, `OpenBridgeEnvelopeObserver`, `OpenBridgeHostCommandMapper`.
- `OpenBridgeHost`, `HostCommandRequest`, `HostCommandResult`, `HostErrorCodes`.
- Smoke testy: `OpenBridgeProtocolSmoke`, `OpenBridgeHostSmoke`, `ResponseExtractorSmoke`.
- Usunięty stary `BridgeBrowserHelper` i Cloud Code connector.
- `config/local/` w `.gitignore`.
