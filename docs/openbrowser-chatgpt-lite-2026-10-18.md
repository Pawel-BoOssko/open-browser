# OpenBrowser ChatGPT Lite, plan i dziennik, 2026-10-18

Cel: umozliwic samodzielne testowanie ChatGPT w lekkim, osobnym oknie Open Browser. Bez automatycznego wykonywania polecen przez modele, bez sprzatania lub usuwania tresci czatow.

Zakres MVP:
- oddzielny tryb `--chatgpt-lite` w aktualnym programie; jeden WebView2 i jedna karta ChatGPT;
- minimalistyczny pasek: Wstecz, Dalej, Odswiez, ChatGPT oraz stan nawigacji;
- profil przegladarki oddzielony od glownej wersji, aby nie laczyc stanu zalogowania i eksperymentow;
- domyslnie bez PageTap, OpenBridge, ConversationTrimmer, CDP i automatycznych iniekcji;
- brak mechanizmow samodzielnie wysylajacych wiadomosci lub czyszczacych historie;
- stale i czytelne uruchamianie w Windows, bez resetowania istniejącej aplikacji;
- calosc zapisac w tym pliku po kazdym kroku. Publikacja tylko przez wspolny Autosync.

Warunki: nie kasowac i nie nadpisywac istniejących zmian w drzewie roboczym (38 przeniesien i 7 zmodyfikowanych zrodel), bez recznego push. Nie udawac, ze login lub funkcjonalnosc strony sa zweryfikowane bez testu w sesji uzytkownika.

## Kroki
1. DONE: odczyt zasad projektu, zbadanie roboczej migracji oraz istniejących komponentow WinForms/WebView2. Stary tryb jest ciezki i aktywuje diagnostyke oraz wykonywanie komend.
2. DONE: kompilacja bazowa zablokowana przez dwie sciezki po migracji csproj (humanizer.json i app_icon.png); SDK 8.0.420, uzytkownik pboru aktywny w sesji console.
3. DONE: powstal ChatGptLiteForm.cs: jeden WebView2, ChatGPT, toolbar, osobny profil w LocalAppData, bez OpenBridge/CDP/DOM injections.
4. DONE: skorygowano 2 sciezki w przeniesionym csproj. dotnet build --no-restore PASS, 0 errors (znane ostrzezenie WindowsBase).
5. DONE: release zbudowany bez bledow, skrypt scripts/launch-chatgpt-lite.ps1 gotowy, task 'OpenBrowser ChatGPT Lite' uruchomil okno w sesji 1 SATANAMD. Stworzono i zweryfikowano skrot na pulpicie 'Open Browser - ChatGPT Lite.lnk'.
6. DONE: Debug i Release build PASS; 3 smoke PASS po odswiezeniu dotnet restore; proces aplikacji w sesji 1 oraz 6 procesow WebView2 Lite potwierdzone. Pelny test logowania ChatGPT wymaga oceny widocznej strony przez uzytkownika.
7. DONE: instrukcja testow na SATANAMD i zapis architektury w docs/agent/architecture-decisions_new.md, aktualizacja current-state i development-rules; skrypt dokumentacyjny PASS, scoped git diff --check PASS.
8. DOING: wywolanie wspolnego Autosync, bez manualnego push, oraz weryfikacja zdalnej migawki.

## Ostatni checkpoint
2026-10-18: Etapy 1-7 DONE, etap 8 w toku. Release 0 bledow, trzy smoke PASS, gate dokumentacyjny PASS. Aplikacja Lite uruchomiona w sesji 1 SATANAMD i skrot na pulpicie. Nastepnie uruchomic istniejacy Autosync (bez manualnego push) i potwierdzic zdalny hash. Nie modyfikowac istniejacych 7 roboczych zmian funkcjonalnych.
- 2026-10-18: Kompilacja Debug po wprowadzeniu trybu --chatgpt-lite: 0 bledow, ostrzezenie MSB3277 WindowsBase. Stary tryb domyslny bez zmian w logice.
- 2026-10-18: W trybie Lite nie powstaje MainForm, LogWriter, OpenBridgeHost, BrowserTabRuntime ani ConversationTrimmer. Profil uzytkownika: %LOCALAPPDATA%\\OpenBrowser\\ChatGPT-Lite\\Profile (poza synchronizowanym repo).
- 2026-10-18: Kompilacja Release: 0 errors, 1 MSB3277 warning. EXE: src/BridgeBrowserAlpha0/bin/Release/net8.0-windows/BridgeBrowserAlpha0.exe.
- 2026-10-18: Test launch: Windows task 'OpenBrowser ChatGPT Lite' Running, proces BridgeBrowserAlpha0.exe w sesji uzytkownika nr 1, argument --chatgpt-lite, profil przegladarki zostal utworzony.
- 2026-10-18: Skrot desktop (OneDrive/Pulpit) istnieje i wskazuje prawidlowy exe z argumentem. Nie wykonywano zalogowania w ChatGPT ani nie wysylano wiadomosci.

## Jak testowac na SATANAMD
1. W zalogowanej sesji Windows otworz pulpit i uzyj skrotu Open Browser - ChatGPT Lite. Jesli okno jest juz widoczne, korzystaj z obecnego okna zamiast otwierac drugie.
2. W jedynej karcie powinno byc https://chatgpt.com/. Zaloguj sie zwyklym sposobem. To oddzielny profil i nie przejmuje sesji z aplikacji ChatGPT/Edge.
3. Sprawdz dzialanie kilku rozmow, przewijanie dlugiej rozmowy, przyciski Wstecz/Dalej/Odswiez/ChatGPT, utrzymanie logowania po zamknieciu i ponownym uruchomieniu.
4. Nie wysylaj polecen do OpenBridge: Lite nie przechwytuje odpowiedzi, nie uruchamia kodu, nie archiwizuje jeszcze rozmow ani nie wznawia samodzielnie zadan. To celowa wersja pilotażowa.
5. Po zakonczeniu testu zamknij okno standardowym X; profil przegladarki zachowa stan. Nie kasowac %LOCALAPPDATA%\\OpenBrowser\\ChatGPT-Lite\\Profile.

## Techniczne punkty kontrolne
- Debug/Release dotnet build: PASS 0 errors, 1 znane MSB3277 WindowsBase warning.
- Po naprawie referencji csproj i dotnet restore: OpenBridgeProtocolSmoke PASS, OpenBridgeHostSmoke PASS, ResponseExtractorSmoke PASS.
- Jedna instancja procesowa w sesji 1 SATANAMD; glowny proces okolo 56.5 MiB, 6 procesow WebView2 z osobnego profilu okolo 315.2 MiB podczas proby.
- Sciezka launcher: scripts/launch-chatgpt-lite.ps1. Task systemowy 'OpenBrowser ChatGPT Lite' umozliwia uruchamianie zdalne w sesji zalogowanego uzytkownika. Skrot pulpitowy wskazuje binarke Release z --chatgpt-lite.
- Brak weryfikacji renderingu i logowania przez sam program (wymaga testu interaktywnego). Nie nalezy mylic procesu i gotowego WebView2 z potwierdzeniem poprawnej autoryzacji OpenAI.
- Zabronione zmiany w profilu bez zgody uzytkownika; poza zadaniem pozostaja mechanizmy backupu czatow i autonomicznego dyspozytora.
