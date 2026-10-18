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
6. DOING: walidacja kompilacji, testy istniejacych pakietow smoke i potwierdzenie izolacji Lite od OpenBridge.
7. TODO: dokumentacja instrukcji testowej, uzupelnienie kontroli projektu i finalny checkpoint.
8. TODO: Autosync, bez manualnego push, oraz zdalne potwierdzenie obecnosci migawki.

## Ostatni checkpoint
2026-10-18: Plan zapisany; kod jeszcze niezmieniony. Rozpoczecie etapu 2.
- 2026-10-18: Kompilacja Debug po wprowadzeniu trybu --chatgpt-lite: 0 bledow, ostrzezenie MSB3277 WindowsBase. Stary tryb domyslny bez zmian w logice.
- 2026-10-18: W trybie Lite nie powstaje MainForm, LogWriter, OpenBridgeHost, BrowserTabRuntime ani ConversationTrimmer. Profil uzytkownika: %LOCALAPPDATA%\\OpenBrowser\\ChatGPT-Lite\\Profile (poza synchronizowanym repo).
- 2026-10-18: Kompilacja Release: 0 errors, 1 MSB3277 warning. EXE: src/BridgeBrowserAlpha0/bin/Release/net8.0-windows/BridgeBrowserAlpha0.exe.
- 2026-10-18: Test launch: Windows task 'OpenBrowser ChatGPT Lite' Running, proces BridgeBrowserAlpha0.exe w sesji uzytkownika nr 1, argument --chatgpt-lite, profil przegladarki zostal utworzony.
- 2026-10-18: Skrot desktop (OneDrive/Pulpit) istnieje i wskazuje prawidlowy exe z argumentem. Nie wykonywano zalogowania w ChatGPT ani nie wysylano wiadomosci.
