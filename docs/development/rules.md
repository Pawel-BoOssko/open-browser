# Zasady pracy nad Open Browser

Obowiązują wszystkich agentów (Claude Code, ChatGPT, Opus i inne) pracujących nad kodem projektu.

## Start pracy

1. Pracuj tylko w `D:\projects\open-browser`.
2. Zawsze zaczynaj od `git status --short`.
3. Nie pracuj na brudnym repo — jeśli są niestackowane modyfikacje, zgłoś użytkownikowi.
4. Przeczytaj `docs/development/journal.md` — zobacz co się ostatnio działo.

## Build i testy

5. Po każdej zmianie w `.cs`: `dotnet build`.
6. Warning MSB3277 (WindowsBase) ignoruj — póki 0 błędów.
7. Po zmianach w `OpenBridgeProtocol`: `dotnet run --project tests/OpenBridgeProtocolSmoke/OpenBridgeProtocolSmoke.csproj`.
8. Po zmianach w `OpenBridgeHost` lub executorach: `dotnet run --project tests/OpenBridgeHostSmoke/OpenBridgeHostSmoke.csproj`.
9. Pełna komenda builda: `dotnet build D:\projects\open-browser\releases\BridgeBrowserAlpha0\src\BridgeBrowserAlpha0\BridgeBrowserAlpha0.csproj`.

## Commity

10. Utrzymuj małe commity — jeden logiczny pakiet na commit.
11. Nie amenduj cudzych commitów. Nie używaj `--no-verify`.
12. Nie commituj plików z `config/local/`.

## Zakazy

13. **NIE** wykonuj `git push` — nigdy, pod żadnym pozorem.
14. **NIE** instaluj paczek (`dotnet add package`) bez wyraźnej zgody użytkownika.
15. **NIE** przywracaj starego `BridgeBrowserHelper` ani Cloud Code connectora.
16. **NIE** twórz nowych warstw, serwisów, managerów, registry, fabryk, dispatcherów, kolejek bez zatwierdzenia.
17. **NIE** projektuj pod niezatwierdzone przyszłe mechanizmy (watchdog, cross-tab, multi-tab registry) chyba że zadanie tego wymaga.
18. **NIE** wychodź poza `D:\projects\open-browser`.
19. **NIE** wstawiaj emoji do kodu.

## Decyzje

20. Gdy potrzebna decyzja architektoniczna — zatrzymaj się, przedstaw opcje, czekaj na użytkownika.
21. Po podjęciu decyzji — zapisz ją w `docs/development/decisions.md`.

## Dokumentacja

21. Każdy nowy folder w `docs/` musi mieć `README.md`: po co folder, kto stworzył, kiedy, jakie zasady.
22. Wchodzisz do folderu — najpierw czytasz jego `README.md`.
23. `docs/development/journal.md` aktualizuj po każdej sesji — data + co zrobione.
24. `docs/development/decisions.md` aktualizuj po każdej podjętej decyzji architektonicznej.
25. Pliki historyczne/superseded trafiają do `D:\projects\old-files\` z nazwą: `<opis>__by-<autor>__keep-until-YYYYMMDD`.

## Po sesji

26. Zaktualizuj `docs/development/journal.md` — dodaj datę i co zrobione/zmienione.
27. Jeśli powstał handoff — trafia do `docs/handoff/`, po użyciu do `old-files/`.
