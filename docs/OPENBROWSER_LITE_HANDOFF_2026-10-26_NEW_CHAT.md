# OPEN BROWSER LITE — HANDOFF DO NOWEGO CZATU
Przygotowano 2026-10-26 13:07 Europe/Warsaw. STATUS: PREPARED / NEW_CHAT_NOT_VERIFIED / SEED_NOT_SENT.
JEDYNY HOST: SATANAMD. PBOX1 WYKLUCZONY.

## Cel
Open Browser Lite służy jako własne narzędzie ciągłej pracy ChatGPT. Priorytety P0: niezawodne wybudzenia i kontynuacja, tylko jeden aktywny wykonawca, brak powtórzeń niepewnych wysyłek, agresywny limit DOM 10 tur bez uszkodzenia generowania i edytora, trwałe archiwizowanie odpowiedzi, szybkie sprawdzone wdrożenia z rollbackiem na SATANAMD.

## Stan zweryfikowany na SATANAMD 2026-10-26
- LC MultiHost target=satamad, COMPUTERNAME=SATANAMD. Standalone Lupus Commander trafia na PBOX1 i nie służy do tej pracy.
- Jeden główny proces BridgeBrowserAlpha0.exe PID 67152, Responding=True, D:\projects\open-browser\src\BridgeBrowserAlpha0\bin\Release\net8.0-windows\BridgeBrowserAlpha0.exe. WebView2 pracuje; z procesu potomnego wersja 1.72.96.
- Najnowsza sekwencja silnika 98: deferred/generating w wake-lifecycle.jsonl, UTC 2026-10-26 12:03. BRAK gwarancji końca generowania. Nie wolno przełączać strony podczas generacji ani ignorować szkicu.
- Próbka DOM 2026-10-26 12:06Z: turns=11, optimized=0, totalDomElements=1387, modeEnabled=true. Wymagany limit 10 tur nieutrzymany w tej próbce.
- Projekty: Open Browser Lite Id 6e84929068ad4746983ce3b21d7e9088; poprzedni lead https://chatgpt.com/c/6adca60a-9980-83ed-943b-fff6ef7fcdfd. Nie promować na podstawie samego URL z pliku.
- Ostatni checkpoint 2026-10-25: v1.72.96 źródło i candidate; wcześniejszy fallback 1.72.95, stan release-managera może mieć stary PID. Sprawdzić przed przełączeniem.
- Brak potwierdzenia utworzenia nowego czatu, wysyłki pierwszej wiadomości lub ACK. Pliki HANDOFF nie są dowodem sukcesji.

## Główne pliki do odczytu
D:\projects\open-browser\AGENTS.md
D:\projects\open-browser\docs\OPENBROWSER_LITE_CHECKPOINT.md
D:\projects\open-browser\docs\OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md
D:\projects\open-browser\docs\OPENBROWSER_LITE_MASTER_PLAN_MIGRACJI_I_ROZWOJU_2026-10-24.md
D:\projects\open-browser\docs\OPENBROWSER_LITE_OPERATING_RULES_2026-10-25.md
D:\projects\open-browser\docs\OPENBROWSER_LITE_AUTO_HANDOFF_P0_GATE_2026-10-24.md
D:\projects\open-browser\docs\OPENBROWSER_LITE_CHAT_HANDOFF_ROTATION_2026-10-23.md
D:\projects\open-browser\src\BridgeBrowserAlpha0\LiteHandoffSuccession.cs
D:\projects\open-browser\src\BridgeBrowserAlpha0\LiteHandoffEvidence.cs
D:\projects\open-browser\src\BridgeBrowserAlpha0\LiteProjectsUi.cs
D:\projects\open-browser\src\BridgeBrowserAlpha0\LiteDomHardPrune.js
%LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\diagnostics\wake-lifecycle.jsonl
%LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\projects.json

## Ograniczenia wdrożenia automatycznej sukcesji
LiteHandoffSuccession.cs implementuje fail-closed ledger faz; NIE tworzy sam nowego czatu ani NIE wysyła seed. UI "Przejmij otwarty czat jako wykonawcę" wymaga prawdziwego nowego /c/ URL i stanu WAIT_USER starego leada. Obowiązuje algorytm z AUTO_HANDOFF_P0_GATE: prawdziwy URL, pusty composer, brak generowania, seed dokładnie raz, obserwacja user bubble i odpowiedzi, checkpoint nowego czatu, dopiero wtedy wyłączenie starego leada i uruchomienie nowego. NIGDY dwa leady jednocześnie.

## Zadania po przejęciu
1. Odczytać checkpoint, operating rules, source i live-engine; sprawdzić aktualne generating/draft/toolBusy, jedno PID i włączony lead.
2. Doprowadzić do bezpiecznego utworzenia nowego prawdziwego chatu w OBL; nie przełączać podczas generowania ani nie powtarzać niepewnych wysyłek.
3. Po pierwszej odpowiedzi nowego czatu zapisać trwały checkpoint, potwierdzić rzeczywisty URL i dopiero wtedy zarządzić sukcesją projektu.
4. Zbadać nieutrzymanie <=10 DOM tur, naprawić pruning, mierzyć RAM/CPU, 8h/24h soak.
5. Zbadać mechaniczny wake/ACK, wznowienie po crashu i PARTIAL/FINAL archiwum; wdrażać tylko po kompilacji/testach WebView2 z rollbackiem.

## Zakazy
PBOX1 nie dotykać. Bez ChatGPT Tasks, ręcznych git commit/push/reset/clean, fake ACK i ponownych niepewnych kliknięć. Bez niszczenia profilu i draftu. Nie zamykać aktywnej generacji.

## Mechanizm checkpointów
Wszystkie etapy zapisywać do OPENBROWSER_LITE_CHECKPOINT.md. Trwały status próby przekazania: docs\OPENBROWSER_LITE_NEW_CHAT_TRANSFER_STATUS_2026-10-26.json. Nie deklarować NEW_CHAT_VERIFIED, dopóki nowy chat realnie nie odpowie.