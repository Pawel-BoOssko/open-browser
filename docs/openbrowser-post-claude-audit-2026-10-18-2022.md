# Open Browser Lite — weryfikacja audytu Claude Code i zamknięcie blokad
Data: 2026-10-18 ~20:22 CEST
Zakres: wyłącznie odczyt stanu i porządkowanie dokumentacji. Bez COMMIT, manualnego push, automatyzacji ChatGPT i restartu okna.

## Źródła
- Najpierw odczytano z GitHub `Pawel-BoOssko/open-browser`, ref `autosync/SATANAMD`, `docs/claude-audit-2026-10-18-1955.md`.
- SATANAMD: SOURCE `D:\projects\open-browser`, branch master HEAD `374ed29`; zastana obszerna dirty migracja (~149 wpisów). Nie resetowano indeksu ani plików.
- GitHub Autosync SATANAMD najnowsza migawka na czas pomiaru: `e49d063b522f4d75b538ba27f0858aaac47929f7`. Autosync 20:01 OK, 20:11 SKIP unchanged. NIE jest to ręczny commit repo master.
- Skaner Autosync: wyjątek dodany wcześniej decyzją właściciela i Claude Code tylko dla `src/BridgeBrowserAlpha0/OpenBrowserLite.ico`, dokładny Git blob `50318466b3657f2df44428a9ab864208ee18992f`. Hash lokalnego pliku potwierdzony. Pozostałe binaria są blokowane. W tej sesji nic nie zmieniano w skanerze.

## PBOX1 LIVE
- Proces `BridgeBrowserAlpha0.exe`: PID `18412`, start `2026-10-18 20:10:37`, `Responding=True`, ścieżka `%LOCALAPPDATA%\Programs\OpenBrowserChatGPTLite_v1.64.0_20261018`. Brak restartu czy ingerencji w jego działanie.
- ProductVersion: `1.64.0+build.20261018T180325Z.e49d063b522f4d75b538ba27f0858aaac47929f7`.
- SHA256 pliku EXE: `F82A3F7D646AB67E24F484D04C9652A8900164784E6A1CBE10FC09E782EF4DA7`.
- Plik manifestu w tej wersji ma nazwę `lite-release-manifest.json` (nie `openbrowser-release-manifest.json`). Zawiera `executableSha256` równe zweryfikowanemu EXE; wersja `1.64.0`, sourceCommit `e49d063`, buildWarsaw `2026-10-18 20:03:25`. Uwaga: manifest nadal opisuje `channel=staged`, pomimo że EXE aktualnie działa w LIVE; potrzebna korekta metadanych w przyszłym wydaniu, bez nadpisywania bieżącej kopii.
- Wynik 10/10 zestawów JS i 16/16 headless selftestów pochodzi z audytu Claude Code / raportu właściciela; w tej turze nie przeprowadzano ponownego build ani testów na LIVE.

## Zamknięte wpisy
- `OB-20261018-164334-m2r`: nieaktualny czerwony test DOM default. Po późniejszej naprawie `_hardPruneEnabled=false`, JS `enabled:false`, testy 10/10 PASS.
- `OB-20261018-190904-autosync-secret-scan`: zamknięty po zgodzie na wąski wyjątek przypięty do jednego bloba; Autosync działa. Nie ma ogólnej zgody na inne binaria.
- `OB-20261018-191000-binary-scan-no-go`: przestarzałe NO-GO STAGED/LIVE, v1.64.0 działa na PBOX1.
- `OB-20261018-185900-pbox-publish`: historyczna odmowa publikacji *v1.63* pozostaje faktem, ale blokada wydania jako całości jest operacyjnie zamknięta po późniejszym wdrożeniu *v1.64* przez właściciela/Claude Code.
- Osiem kolidujących identyfikatorów `OB-12..15` przemianowano na unikalne `OB-20261018-1542xx/1543xx/1544xx/1545xx` z aliasami `dawniej OB-N`; pierwotne opisy zdarzeń zachowane. Nowe wpisy zawsze unikalne.
- Nie poprawiano na siłę innych historycznych odmów. Aktywne wywołanie zablokowane przez zabezpieczenia pozostaje niedozwolone bez odrębnej decyzji.

## Otwarte punkty
1. Windows: wizualne QA ikony 16px, przypięcia Start i paska zadań, tooltipa i stanu pracy. Właściciel zarządza swoimi pinami.
2. `OB-10`: harmonogram Windows `OpenBrowser ChatGPT Lite` nadal wskazuje `OpenBrowserChatGPTLite_20261018_telemetry\BridgeBrowserAlpha0.exe`, nie v1.64. Nie powtarzać poprzedniej zablokowanej operacji obejściem.
3. Luna: na PBOX1 nie ustawiono user-level `OPENAI_API_KEY` i `OPENBROWSER_LUNA_MODEL`. Ale kod źródłowy `LiteLunaAdvisor.cs` zawiera fallback `LiteApiCredential.ReadSharedKey()` do Windows Credential Manager; stwierdzenie „Lite wcale nie czyta klucza z OpenFill” wymaga doprecyzowania testem runtime. Nie ujawniać żadnego klucza ani nie wykonywać niezamierzonych wywołań płatnych.
4. `delivery_timeout`: błąd widoczny w ChatGPT, nie ma dowodu stanu serwera. Brak automatycznego `Ponów`.
5. Dom 0/0: realne QA struktury rozmowy, bez agresywnego prune w niezweryfikowanym DOM. High + Luna nadzór w prawdziwym testowym czacie E2E, bez automatycznych przypominajek.
6. Metadata release: manifest `channel=staged` mimo faktycznego LIVE. Rozwiązać w nowym wydaniu przy kontrolowanej promocji, nie zmieniając działającego EXE ani bezpośrednio manifestu.

## Zasady
- **DO NOT COMMIT**, nie robić ręcznego git push; wspólny Autosync pracuje.
- Zero automatycznych przypominajek ChatGPT i zero automatycznego uruchamiania równoległych czatów.
- Nie ruszać okna PID18412 bez potrzeby. Każda przyszła zmiana SOURCE / TEST / STAGED / LIVE osobno.
