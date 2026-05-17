# Proponowana kolejność dalszych prac (Next Work)

## Current priority: close the autonomous execution loop

The LLM must work autonomously: send command → executor executes → result feeds back to LLM conversation. No human in the runtime loop. The remaining gap is result delivery from Host back to the WebView conversation.

## Etap A: Domknięcie procesu karty bez Hosta
- **Cel:** Ostatnie ewentualne sprzątanie plików i izolowanie warstw przed wprowadzeniem w projekt logiki biznesowej, która dotknie wykonywania poleceń.
- **Pliki prawdopodobnie dotknięte:** Pliki UI lub LogWriter.
- **Warunki startu:** Przejście handoff.
- **Warunki zakończenia:** Architektura gotowa na bycie klientem Hosta.
- **Zakazy:** Tworzenie przyszłościowych obiektów, takich jak watchog czy event bus. Wydzielaj komponenty TYLKO na wniosek faktycznych odpowiedzialności.

## Etap B: Projekt Hosta
- **Cel:** Stworzenie czystej dokumentacji (architektoniczny dokument projektowy) definiującej, jak parser przekaże skompletowaną instrukcję (np. powłokę JSON EXEC) do centralnego silnika autoryzacyjnego Hosta. Określenie boundary, komend wewnętrznych `HST_`, formatek statusu i odpowiedzi systemowych.
- **Pliki prawdopodobnie dotknięte:** Katalog `docs/architecture/`.
- **Warunki startu:** Realne zamknięcie Etapu A.
- **Warunki zakończenia:** Kompletny dokument projektowy, opisujący co najmniej `HST_HELP` oraz zasady braku autoryzacji poleceń.
- **Zakazy:** Żadnych zmian w kodzie C#!

## Etap C: Host skeleton
- **Cel:** Opracowanie interfejsu/klasy głównej Hosta bazując ściśle na zaakceptowanym dokumencie z Etapu B.
- **Pliki prawdopodobnie dotknięte:** Moduły/Katalog w `src/BridgeBrowserAlpha0/OpenBridgeProtocol/` albo `src/BridgeBrowserAlpha0/Host/`.
- **Warunki startu:** Zatwierdzony i kompletny projekt (Etap B).
- **Warunki zakończenia:** Działający szkielet Hosta połączony z pasywnym Obserwatorem, odpowiadający poprawnie tylko i wyłącznie na komendę systemową `HST_HELP` w testach (komunikacja wewnętrzna).
- **Zakazy:** Kategoryczny brak wprowadzania executorów dla systemu plikowego (`FS_`) czy shell (`SH_`).

## Etap D: Executor model audit
- **Cel:** Uprzątnięcie zaszłości i zrozumienie na jakim wzorcu kiedyś opierały się executory, a na jakim powinny w stelażu OpenBridge.
- **Pliki prawdopodobnie dotknięte:** Nowy plik `docs/architecture/executor-audit.md`.
- **Warunki startu:** Pomyślne przejście testów szkieletu Hosta z `HST_HELP`.
- **Warunki zakończenia:** Spisane i zatwierdzone standardy dla podziału poleceń i wstrzykiwania `Payload64`.
- **Zakazy:** Implementowanie executorów. Najpierw audyt!

## Etap E: Routing / Registry / Cross-tab / Watchdog
- **Cel:** Budowa pełnej state machine dla Hosta i zarządzanie cyklami życia procesów uciekających.
- **Pliki prawdopodobnie dotknięte:** `Host.cs`, mechanizmy wygaszania.
- **Warunki startu:** Zrealizowane decyzje.
- **Warunki zakończenia:** System samoczynnie odrzucający polecenia w trybach asynchronicznych podczas pracy, i informujący model o statusie zadania.
- **Zakazy:** Zakaz startu bez osobnych zatwierdzonych zgód! Zawsze pytaj lub weryfikuj przed pisaniem Watchdoga.
