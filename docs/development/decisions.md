# Decyzje architektoniczne

Tylko żywe, obowiązujące decyzje. Jedna linia + data. Historyczne/usunięte nie są tu przechowywane.

---

## Architektura

- **2026-05-16** OpenBridge to architektura systemu, Open Browser to aplikacja.
- **2026-05-16** 5 warstw: LLM → browser tab → tab process → Host → executor.
- **2026-05-16** UI shell i tab runtime to oddzielne odpowiedzialności.
- **2026-05-16** Źródłem prawdy dla odpowiedzi modelu jest stream/delta, nie DOM.
- **2026-05-17** Autonomous execution loop: LLM decyduje → executor wykonuje → wynik wraca do LLM. Bez człowieka w pętli.

## Koperty

- **2026-05-17** Markery: `@@OPENBRIDGE_EXEC_BEGIN@@` i `@@OPENBRIDGE_EXEC_END@@`.
- **2026-05-17** Tylko komenda `PS` (PowerShell) jest akceptowana przez mapper.
- **2026-05-18** Parser bierze pierwszą kopertę, resztę ignoruje.
- **2026-05-18** Każda próba koperty dostaje feedback do LLM (wynik lub `[OpenBridge] ...` błąd).

## Host i executor

- **2026-05-17** `GeneralCommandExecutor` — jedyny aktywny executor, uruchamia dowolny proces przez `Process.Start`.
- **2026-05-17** `OpenBridgeHost` — współdzielony singleton, nie per-komenda. Zawiera lock współbieżności (`_busy`).
- **2026-05-17** `IOpenBridgeCommandExecutor` — interfejs dla hot-swap executora.
- **2026-05-18** Output executora cięty przy `MaxOutputChars` (domyślnie 50k) ze znacznikiem `[... truncated ...]`.

## UI i interakcja

- **2026-05-17** Wynik wstrzykiwany do `#prompt-textarea.ProseMirror` + kliknięcie Send buttona (`[data-testid='send-button']`).
- **2026-05-18** Human delay: 20s baza + rozkład normalny (mean=22s, std=11s, min=0, max=50s) przed wysłaniem odpowiedzi.

## Wersjonowanie

- **2026-05-18** Build number = `git rev-list --count HEAD`. Hash = `git rev-parse --short HEAD`. Data z kropkami.

## Bezpieczeństwo

- **2026-05-16** Granice bezpieczeństwa na poziomie OS i konfiguracji sandboxa — nie w runtime approval.
- **2026-05-16** `config/local/` w `.gitignore` — nie commitujemy sekretów.
- **2026-05-17** Zakaz komend z `--dangerously-skip-permissions`, `git push`, `dotnet add package` w konfiguracji — usunięte wraz ze starym `CommandExecutor`.

## Otwarte decyzje

- Format komend `HST_*` (HELP, STATUS, CAPABILITIES) — nierozstrzygnięty.
- Cross-tab communication — nierozstrzygnięty.
- Watchdog/recovery — nierozstrzygnięty.
- Czy approval panel UI ma zostać całkowicie usunięty.
- Docelowa implementacja linguistic response variants (humanizer).
