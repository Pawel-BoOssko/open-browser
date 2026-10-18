# Rejestr nieudanych komend — Open Browser
Wersja 2026-10-18 14:50 CEST. Zapis historyczny odtworzony z dostępnych checkpointów. **Nie jest oceną niebezpieczeństwa**: sam fakt odmowy narzędzia nie identyfikuje reguły ani przyczyny. Dalsze próby równoważnych operacji wymagają analizy, bez obchodzenia blokad.
Pola obowiązkowe dla wszystkich dalszych wpisów: identyfikator; czas; narzędzie; komputer; krok mały/duży; dokładna intencja/komenda; wynik i dosłowny komunikat; zakres blokady; czy zapis wykonano; analiza zagrożeń (potwierdzone / przypuszczalne); decyzja i alternatywne niezależne zadanie.

## 2026-10-18 — wcześniejsze incydenty

### OB-01 — LC MultiHost PBOX1 — odczyt dziennika Windows
- Krok: pojedynczy odczyt diagnostyczny (Get-WinEvent Application 1000/1001/1002/1026, System 2004, WER; zakres 6 godzin; max 30).
- Wynik: >200 s `running`, potem `target_unavailable`, `shell_lifecycle=crashed`; przerwano. Brak danych.
- Zakres: tylko odczyt dziennika, brak modyfikacji. Nie dowiedziono, czy powodem były obciążenie lub zabezpieczenia.
- Dalsza decyzja: nie powtarzano ciężkiego zapytania; odczyty lekkiej telemetrii były odrębną pracą.

### OB-02 — LC MultiHost SATANAMD — Release build
- Krok: budowa Release do katalogu używanego przez działający proces.
- Wynik: MSB3021/MSB3027, plik EXE zajęty przez istniejący PID 13384.
- Zakres: nie udała się zamiana pliku wyjściowego; źródła i proces pozostały.
- Dalsza decyzja: osobny katalog build/side-by-side, bez zamykania cudzego okna.

### OB-03 — LC MultiHost PBOX1 — rejestracja monitora
- Krok: pojedynczy złożony Register/Set-ScheduledTask wraz z uruchomieniem.
- Wynik: timeout >100 s, `target_unavailable`; odczyt potwierdził MONITOR_INSTALLED=NO.
- Zakres: zadanie monitorowania nie powstało tą próbą; potem użytkownik sam wykonał odmienny, jawnie uruchomiony skrypt schtasks.exe i zweryfikowano stan.
- Potwierdzone: brak wdrożenia po tej próbie; przyczyna wewnętrzna nieznana.

### OB-04 — Remote Desktop Commander — projekt testowy
- Krok: złożona operacja create_directory + write_file w tests/LiteProjectsSmoke.
- Wynik: „To wywołanie narzędzia zostało zablokowane przez zabezpieczenia OpenAI. Sprawdź dokładniej, co wysyłasz.”
- Zakres: projekt smoke nieutworzony; nie potwierdzono żadnego zapisu.
- Dalsza decyzja: bez powtarzania blokady; osobne, niezależne testy aplikacji trybem --lite-projects-selftest.

### OB-05 — Remote Desktop Commander — aktualizacja dokumentów
- Krok: dwa edit_block w jednej operacji, docs/agent/current-state_new.md oraz docs/agent/architecture-decisions_new.md.
- Wynik: to samo ogólne odrzucenie przez zabezpieczenia; brak potwierdzenia zapisów.
- Zakres: wyłącznie planowane dwa fragmenty dokumentacji.
- Dalsza decyzja: checkpoint w istniejącym pliku master planu; nie powtarzano identycznej operacji inną drogą.

### OB-06 — Remote Desktop Commander — DPAPI selftest
- Krok: mała modyfikacja LiteSelfTest.cs dodająca testy szyfrowania.
- Wynik: blokada przez zabezpieczenia OpenAI; modyfikacja niepotwierdzona.
- Zakres: konkretna zmiana tekstu testu; inne moduły pozostają.
- Dalsza decyzja: niezależna wcześniejsza implementacja LiteArchiveSelfTest w nowszym checkpointcie; test smoke istnieje.

### OB-07 — LC MultiHost SATANAMD — eksperyment DPAPI PS7
- Krok: odczytowy skrypt w osobnym PowerShell 7, wywołujący Protect/Unprotect i porównanie danych syntetycznych.
- Wynik pierwszego wariantu: wywołania DPAPI zwróciły szyfrogram, ale błędne `AsSpan` w PowerShell sprawiło, że round-trip był nieweryfikowalny.
- Drugi wariant: polecenie wisiało >100 s; cancel_current_command zakończył sesję.
- Zakres: test, bez zmiany aplikacji/produkcji.
- Dalsza decyzja: nie powtarzać tej operacji. Nowszy LiteArchiveSelfTest uruchamiany w kontekście właściwego .NET potwierdza testy na SATANAMD.

### OB-08 — Remote Desktop Commander — timeout PollDomAsync
- Krok: dwie chirurgiczne edycje metody PollDomAsync w ChatGptLiteForm.cs, około 30 linii.
- Wynik: blokada OpenAI; read-only wykazał stary wariant metody.
- Zakres: tylko naprawa obsługi zablokowanego rendererowego ExecuteScriptAsync.
- Dalsza decyzja: nie powtarzać zablokowanej modyfikacji jako workaround; stan pozostaje do analizy.

### OB-09 — Remote Desktop Commander — separacja błędów kopii
- Krok: pojedyncza edycja 3→11 linii w LiteConversationCaptureUi.cs, osobny try/catch per wiadomość.
- Wynik: blokada OpenAI; brak potwierdzenia zapisu.
- Zakres: separacja pojedynczego błędu kopii; nie blokuje pozostałych niezależnych funkcji.
- Dalsza decyzja: nie powtarzać równoważnej edycji bez analizy.

## Rejestr od tej chwili
Wszystkie kolejne problemy, odmowy i nieudane operacje zapisywać jako OB-10, OB-11 itd. Rozróżniać **odmowę zabezpieczeń** od błędu kodu, timeoutu, braku uprawnień i ostrzeżenia kompilacji. Brak informacji o przyczynie => „nieustalona”, bez wymyślania oceny zagrożenia.

### OB-10 — 2026-10-18 ~15:11 — LC MultiHost PBOX1 — aktualizacja zadania startowego
- Narzędzie: `functions.exec` -> `mcp__lc_multihost__execute_command(target=pbox1)`.
- Rozmiar: pojedyncza operacja `Set-ScheduledTask -TaskName 'OpenBrowser ChatGPT Lite' -Action (New-ScheduledTaskAction -Execute %LOCALAPPDATA%\Programs\OpenBrowserChatGPTLite_20261018_notes_dom_voice\BridgeBrowserAlpha0.exe -Argument --chatgpt-lite)`. Nie obejmowała zamykania aktywnego okna ani restartu.
- Wynik: polecenie `running` ponad 100 s, bez odpowiedzi; `cancel_current_command` po ~112 s odtworzył sesję PowerShell. Odczyt po anulowaniu: task nadal wskazuje starszy `OpenBrowserChatGPTLite_20261018_telemetry\BridgeBrowserAlpha0.exe`, state Ready. PID 15860 niezmieniony.
- Zakres blokady: zmiana akcji zadania harmonogramu nie została wykonana; nie wpłynęła na działające okno, na profil ani na nowe skróty.
- Kategoria: timeout/bez końca, nie jawna odmowa zabezpieczeń. Przyczyna wewnętrzna Harmonogramu nieustalona; mogły zadziałać uprawnienia/usługa/zawieszenie powłoki — bez dowodu.
- Dalsza decyzja: nie ponawiać Set-ScheduledTask przez inne narzędzie ani schtasks. Działające skróty Windows na pulpicie i w menu Start wskazują nowsze wydanie z ikoną; aktualny LIVE dalej działa w starej wersji.
