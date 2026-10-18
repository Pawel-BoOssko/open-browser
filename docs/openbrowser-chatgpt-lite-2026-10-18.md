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
3. DOING: dodanie izolowanego okna ChatGPT Lite i przelacznika startowego w Program.cs.
4. TODO: przywrocenie poprawnych sciezek assetow potrzebnych po przeniesieniu csproj, bez zmian historycznych funkcji.
5. TODO: skrypt startowy i kontrola logowania/stanu procesu.
6. TODO: walidacja kompilacji i uruchomienia, testy negatywne (brak uruchamiania OpenBridge w trybie Lite).
7. TODO: dokumentacja instrukcji testowej, uzupelnienie kontroli projektu i finalny checkpoint.
8. TODO: Autosync, bez manualnego push, oraz zdalne potwierdzenie obecnosci migawki.

## Ostatni checkpoint
2026-10-18: Plan zapisany; kod jeszcze niezmieniony. Rozpoczecie etapu 2.
