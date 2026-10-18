# Open Browser Lite — checkpoint: autonomiczne budzenie bieżącego czatu

Czas: 2026-10-19, ok. 01:39 Europe/Warsaw.

## Polecenie użytkownika (decyzja architektoniczna)
- **Własny mechanizm Open Browser Lite**, nie OpenAI ChatGPT Tasks/reminders. Co około 25 minut budzi jedną rozmowę „Open Browser — rozwój aplikacji”. Program NIE wybiera zadań; model sam ma otworzyć swój pełny plan, checkpoint i działać dalej.
- W nocy należy podejmować kolejne zadania bez interwencji użytkownika. Nie gwarantować ukończenia całości ani dostawy API, jeśli dowodu brak.
- Projektowy panel po lewej to tylko lokalne aliasy; prawdziwe Projekty ChatGPT i przenoszenie rozmów mają wejść do pełnego planu, ale nie kosztem P0 budzenia.
- 86-punktowy plan: `docs/design/openbrowser-master-roadmap-v3-2026-10-19.md`. Dotychczasowy backlog v2 pozostawiono jako historyczny.

## Stan źródeł
- SATANAMD repo `D:\projects\open-browser` dirty/staged historyczny, master HEAD `374ed298`; obowiązuje **DO NOT COMMIT**, zero ręcznych push. Autosync techniczny only.
- PBOX1 LIVE o 01:26 CEST `v1.67.0`, PID `13032`, uruchomiony 01:11:49, tryb `--chatgpt-lite`. Window/profil nie modyfikowane w trakcie pracy.
- Identyfikator tej konkretnej rozmowy został odczytany z kopii lokalnej historii WebView2 PBOX1; NIE publikować go do Git/repo. Docelowy URL zapisuje się wyłącznie w DPAPI profilu użytkownika PBOX1.
- SOURCE `1.68.0`: `LiteDevWakeState.cs` (DPAPI `%LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\dev-wake-settings.dpapi`, dokładnie 1 chat URL, 25m, 40/day, stabilny hash ostatniej odpowiedzi, commit przed send, brak retry dla identycznej odpowiedzi, completion marker), `LiteDevWakeUi.cs` (WinForms Timer 60s, bramka High+idle+URL+no draft+no error, preflight recheck, potwierdzenie DIL, UI przełącznik/status). `Program.cs` nowe `--lite-dev-wake-arm https://chatgpt.com/c/<ID>` i `--lite-dev-wake-selftest`. Na autostart otwiera jedyny wcześniej przypięty czat, jeśli armed; dla innych URLs nie działa.
- Budzący tekst jest krótki, nie przydziela punktów; wskazuje ścieżkę trwałego planu. Po ukończeniu planu czat może zwrócić marker `[OPENBROWSER-PLAN-COMPLETE]`, watcher się wyłącza. Manualny OFF zawsze możliwy.
- Wewnętrzny czasomierz działa tylko gdy jest uruchomiona aplikacja. Po restarcie Windows sam nie uruchomi procesu bez osobnej konfiguracji autostartu; nie obiecywać autonomicznego wykonania bez działania UI i połączenia.
- Kod źródłowy został zbudowany Debug: 0 errors, 1 known MSB3277, `--lite-dev-wake-selftest` exit 0 (10 checks). JS regression suites 12/12 PASS i promotion gate PASS, doc gate PASS, git diff --check PASS przed ostatnią kosmetyczną poprawką budzącego tekstu. Wymaga powtórzenia po zmianie kodu i staged PBOX.
- Nie zmieniać istniejącego ustawienia trwałej kwarantanny wcześniejszych senderów; nowa funkcja nie powtarza niepewnych wysyłek; w razie nowej niepewności disable i wymaga odrębnej decyzji.

## Następne kroki P0
1. Testy Debug/JS i synchronizacja przez istniejący Autosync, żadnych przypominajek.
2. PBOX1 czysty klon autosync/SATANAMD `git pull --ff-only`, side-by-side Release v1.68.0, manifest, 16+ selftestów plus dev-wake selftest.
3. Lokalnie na PBOX1 uruchomić tylko headless `--lite-dev-wake-arm` dla rozmowy z bieżącej historii, potwierdzić DPAPI; pierwsze budzenie nie wcześniej niż 25 min.
4. Zastąpić produkcyjne okno v1.67 nową wersją tylko przy możliwie bezpiecznym stanie idle, bez utraty notatek i aktywnego generowania. Sprawdzić LIVE wersję, UI Autopraca: WŁ., rzeczywisty tick i ewentualny pierwszy ACK.
5. Jeśli praca ChatGPT/renderer blokuje wysłanie albo aplikacja nie została wymieniona, raportować BLOCKED i nigdy nie ogłaszać działającego nadzoru bez realnego potwierdzenia.
6. Czat po każdym prawdziwym wznowieniu otwiera plan i sam wybiera zadania. Test zgodności z planem i checkpointem.
