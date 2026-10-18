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
2. DONE: Autosync SATANAMD sprawdzony. Skrypt i zadanie wspolne istnieja, lecz OpenBrowser nie jest jeszcze wpisany do repos.json. Wykryto ryzyko publikacji prywatnych pobran.
3. DONE: Rozpoznano lokalne artefakty, 27 lokalnych commitow ponad GitHub i 38 staged przeniesien kodu. Nie dotykano danych ani kodu.
4. DONE: Sprawdzono git check-ignore; wykluczono dokumenty pobrane, lokalny runtime start prompt, exports, pycache i tymczasowe artefakty. Pozostaly one na dysku.
5. DONE: Dodano src/FOLDER.md, zaktualizowano kontrole katalogow i docs/agent/current-state_new.md. Niezakonczona migracja pozostaje odlozona.
6. DONE: Kontrola zasad dokumentacji zakonczona kodem 0; scoped git diff --check dla naszych zmian takze kod 0. Stare trailing whitespace w niezaleznych, lokalnych zmianach C#/JS pozostaja.
7. DOING: Wlaczenie OpenBrowser do wspolnego Autosync SATANAMD i kontrola snapshotu. Bez bezposredniego git push.
8. TODO: Zapisac koncowy stan, wykonane zmiany, stan repo, wykluczenia, problemy odlozone i kroki do dalszej pracy.

## Ostatni checkpoint
2026-10-18: Plan zapisany. Ostatnio wykonano tylko inspekcje, brak zmian w kodzie lub historii Git.

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
