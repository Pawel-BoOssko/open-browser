# OpenBrowser: porzadki i utrzymanie, 2026-10-18

## Zasady i granice
- Glowny katalog: D:\projects\open-browser, komputer SATANAMD.
- Cel: uporzadkowanie repozytorium, dokumentacji, lokalnych artefaktow i synchronizacji.
- Bez zmian funkcji i logiki programu, bez naprawiania pobierania plikow ChatGPT ani innych bledow funkcjonalnych.
- Bez kasowania dokumentow uzytkownika, profilu, logow lub niezatwierdzonej pracy.
- Bez bezposredniego git push. Synchronizacja wylacznie przez wspolny Autosync, po weryfikacji ochrony danych.
- Nie zmieniac kopii open-browser-stable bez koniecznosci.
- Stan kroku: TODO, DOING, DONE, BLOCKED, DEFERRED. Dalszy agent rozpoczyna od pierwszego niewykonanego kroku.

## Plan i checkpoint
1. DONE: Odczytane zasady z docs/agent i sprawdzony dirty working tree; zastano 38 przeniesien, 7 modyfikacji oraz 31 nie sledzonych pozycji.
2. DONE: Skontrolowano Autosync SATANAMD. Na poczatku wpisu OpenBrowser brakowalo; po zabezpieczeniu prywatnych pobran zostal dodany w etapie 7.
3. DONE: Rozpoznano lokalne artefakty, 27 lokalnych commitow ponad GitHub i 38 staged przeniesien kodu. Nie dotykano danych ani kodu.
4. DONE: Sprawdzono git check-ignore; wykluczono dokumenty pobrane, lokalny runtime start prompt, exports, pycache i tymczasowe artefakty. Pozostaly one na dysku.
5. DONE: Dodano src/FOLDER.md, zaktualizowano kontrole katalogow i docs/agent/current-state_new.md. Niezakonczona migracja pozostaje odlozona.
6. DONE: Kontrola zasad dokumentacji zakonczona kodem 0; scoped git diff --check dla naszych zmian takze kod 0. Stare trailing whitespace w niezaleznych, lokalnych zmianach C#/JS pozostaja.
7. DONE: Dodano OpenBrowser do %LOCALAPPDATA%\Autosync\repos.json. Uruchomiono istniejace zadanie 'Autosync repozytoriow'; OK w autosync.log, branch autosync/SATANAMD na GitHub. Nie wykonano recznego git push.
8. DONE: Utrwalono koncowa inwentaryzacje, zakres wykluczen, kontrole i odlozone problemy. Przed zamknieciem odswiezyc ostatnia migawke Autosync i zweryfikowac log.

## Ostatni checkpoint
2026-10-18: Etapy 1-8 DONE. Po kazdej dalszej zmianie tego pliku synchronizacje zapewnia zadanie 'Autosync repozytoriow'. Potwierdzenie stanu: %LOCALAPPDATA%\Autosync\autosync.log i galaz autosync/SATANAMD. Nie prowadzono napraw funkcjonalnych ani zmian w kodzie aplikacji. Brak recznych pushy.

## Dziennik czynnosci
- 2026-10-18: Sprawdzono Autosync: %LOCALAPPDATA%\Autosync\autosync.ps1, repos.json (wylacznie CourtControl), harmonogram 'Autosync repozytoriow'.
- 2026-10-18: Stwierdzono, ze gitignore nie obejmowal release/downloads i cache Pythona, co grozilo objeciem ich snapshotem.
- 2026-10-18: Dodano wykluczenia dla lokalnych pobran, promptu z katalogu runtime, exports, pycache i artefaktow testowych. Nie usunieto plikow.
- 2026-10-18: W repo sa istniejace zmiany: 38 staged renames, 7 modified, 31 untracked przed obecnymi zmianami. Nie zmieniono ich kodu.
- 2026-10-18: git check-ignore potwierdzil wykluczenie pobranych dokumentow, start-prompt runtime, exports, cache i plikow testowych. Pozostaly tylko kontrolowane pliki nowej dokumentacji.
- 2026-10-18: Utworzono src/FOLDER.md; zmieniono tylko liste katalogow w scripts/check-agent-docs.ps1 i opis faktycznego stanu w docs/agent/current-state_new.md oraz folder-policy_new.md.
- 2026-10-18: Istniejace niezatwierdzone modyfikacje kodu zawieraja trailing whitespace (git diff --check). Zapisano jako dlug techniczny, bez naruszania ich.
- 2026-10-18: Kontrola docs: scripts/check-agent-docs.ps1 PASS, scoped git diff --check PASS. Stary kod C#/JS niezmieniany.
- 2026-10-18: Przed Autosync: status 44 wpisy, w tym 38 staged renames i 2 untracked: docs/maintenance-...md i src/FOLDER.md. Staged tree hash 068e7e47312ab2fc05ef78aef5db8303f9f06164.
- 2026-10-18: 27 nieopublikowanych commitow, analiza dodanych w historii plikow nie ujawnila rzeczywistych sekretow. Trzy flagi heurystyczne odnosza sie do dynamicznego uzycia tokenu przez tools/linkedin/linkedin_post_image.py, bez stalej wartosci tokenu.

## Final inventory and maintenance outcome
- Glowny katalog: D:\projects\open-browser (SATANAMD). Lokalna kopia: D:\projects\open-browser-stable, bez zmian.
- Branch lokalny: master, HEAD 374ed29 z 2026-05-21, 27 commitow ponad origin/master. Robocza migracja kodu: 38 staged renames + 7 preexisting source modifications, pozostawione bez zmian.
- Aktualna lokalizacja niezatwierdzonej migracji zrodel: src/BridgeBrowserAlpha0/. Historyczna sciezka releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/ nie istnieje. Plik csproj po przeniesieniu ma bledne sciezki do humanizer.json i app_icon.png. Nie naprawiano ani nie uruchamiano aplikacji.
- Dokumentacja: docs/agent/READ_BEFORE_WORK_new.md jest glownym punktem startowym. Stan migracji dopisano do docs/agent/current-state_new.md. Polityka nowego src: src/FOLDER.md, a walidator obsluguje te lokalizacje.
- Testy Smoke: OpenBridgeProtocolSmoke, OpenBridgeHostSmoke, ResponseExtractorSmoke. Nie uruchamiano testow funkcjonalnych. Dokumentacyjny gate i scoped git diff --check maja wynik PASS.
- Dane runtime w releases/BridgeBrowserAlpha0/downloads, cache, profil, lokalne sekrety, lokalne eksporty i testy zostaly pozostawione w miejscu. .gitignore chroni je przed snapshotami.
- Wspolny Autosync: C:\Users\pboru\AppData\Local\Autosync\autosync.ps1, repos.json oraz task 'Autosync repozytoriow'. Nowy wpis: open-browser, D:\projects\open-browser, branch autosync/SATANAMD, enabled true. Wczesniejszy courtcontrol pozostawiono bez zmian. Kopia poprzedniej konfiguracji: repos.before-open-browser.2026-10-18.json.
- Pierwszy snapshot OpenBrowser byl opublikowany przez Autosync na autosync/SATANAMD (34b6341cde26); zadanie zakonczone z LastTaskResult=0. Drzewo zdalne zawieralo 206 plikow, w tym 0 prywatnych pobran, 0 eksportow runtime i 0 plikow z lokalnych obszarow chronionych.
- Autosync nie modyfikuje lokalnego indeksu ani roboczych plikow: HEAD nadal 374ed29, hash drzewa indeksu 068e7e47312ab2fc05ef78aef5db8303f9f06164, 38 staged renames i 2 untracked pliki utrzymania.

## Odlozone do osobnego zlecenia
- Zakonczenie roboczej migracji kodu, korekta sciezek csproj i budowanie.
- Debugowanie przechwytywania odpowiedzi modeli, pobierania plikow, kodowania i funkcji portalu sadowego.
- Trwały podzial OpenBrowser oraz Otwarty Portal Sadowy.
- Naprawa preexisting trailing whitespace w C#/JS; nie wolno masowo formatowac roboczych zmian przed analiza.
- Decyzja o porzadkowaniu/mergowaniu lokalnych commitow na master; Autosync uzywa osobnej galezi. Nie trzeba wykonywac recznego push.

## Wznowienie pracy
Otworz ten plik i docs/agent/READ_BEFORE_WORK_new.md. Zweryfikuj stan git, skontroluj dziennik Autosync, a po poznaniu nowego celu zastosowania OpenBrowser opracuj osobny plan funkcjonalny. Nie rozpoczynaj zaleglych poprawek bez nowego zlecenia.
