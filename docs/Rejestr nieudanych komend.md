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

### OB-11 — 2026-10-18, kolejna iteracja: rejestracja dwóch sesji LC MultiHost
- Narzędzie: `functions.exec`, jedno wywołanie JS `Promise.all` obejmujące dwa `mcp__lc_multihost__register_chat` (target SATANAMD i PBOX1).
- Rozmiar: dwa małe niezależne wywołania rejestracji nazw tej samej rozmowy, bez wykonywania komend systemowych i bez zapisu plików.
- Rezultat: `To wywołanie narzędzia zostało zablokowane przez zabezpieczenia OpenAI. Sprawdź dokładniej, co wysyłasz.`; nie otrzymano potwierdzenia rejestracji żadnego hosta.
- Zakres: wyłącznie złożona rejestracja obu sesji; nie obejmuje repozytorium ani kodu. Przyczyna decyzji zabezpieczeń nie została ujawniona.
- Dalsze postępowanie: bez ponawiania tej samej podwójnej operacji; zadania czytania i edycji plików przez RDC pozostają niezależne od tej rejestracji.

### OB-12 — 2026-10-18, drobna edycja inicjalizacji notatek
- Narzędzie: `functions.exec` -> `Remote_Desktop_Commander__edit_block` (drugie wywołanie tego samego skryptu; pierwsze wykonano i potwierdzono odczytem).
- Rozmiar: jedna mała zmiana 1 linii w `src/BridgeBrowserAlpha0/LiteNotesUi.cs` (`_noteKey` ustawiany początkowo na pusty zamiast `inbox`), aby odczytać notatki ogólne przy starcie.
- Rezultat: `To wywołanie narzędzia zostało zablokowane przez zabezpieczenia OpenAI.` Odczyt pliku potwierdził, że linia pozostała `_noteKey = "inbox"`, a wcześniejszy, odrębny dodany field `_notesOutbox` jest obecny.
- Zakres: tylko inicjalizacja odczytu ogólnych notatek, NIE zapis szyfrowanej kolejki ani dotychczasowe dane; przyczyna blokady nieudostępniona. Nie ponowiono równoważnej zmiany alternatywnym narzędziem.

### OB-12 — 2026-10-18 ~15:43 — LC MultiHost SATANAMD — publikacja STAGED
- Narzędzie: `functions.exec` -> `mcp__lc_multihost__execute_command(target=satamad)`.
- Rozmiar: jeden średni krok systemowy: `dotnet publish src/BridgeBrowserAlpha0/BridgeBrowserAlpha0.csproj -c Release --no-restore -m:1 -v:q -p:OutputPath="$build\\" -p:PublishDir="$base\\"`, gdzie `$build=%TEMP%\\ob-lite-build-1543`, `$base=%LOCALAPPDATA%\\Programs\\OpenBrowserChatGPTLite_20261018_1543_outbox_tabpolicy`; następnie odczyt statusu i istnienia EXE.
- Wynik dosłowny: `This tool call was blocked by OpenAI's safety checks. Please double check what you are sending.` Nie otrzymano wyników polecenia ani potwierdzenia rozpoczęcia publish.
- Zakres: wyłącznie próba przygotowania odrębnego wydania STAGED na SATANAMD; LIVE, profil WebView2 i procesy nie były celem operacji. Nie potwierdzono jakiegokolwiek zapisu.
- Kategoria: jawna odmowa zabezpieczeń; przyczyna nieujawniona, nie dowiedziono ryzyka samej kompilacji ani uprawnień systemu.
- Decyzja: nie powtarzać równoważnej publikacji innym narzędziem. Kontynuować niezależne testy i dokumentację; status STAGED dla tego pakietu pozostaje NIEPOTWIERDZONY.

### OB-13 — 2026-10-18 ~15:44 — LC MultiHost PBOX1 — testy headless STAGED i odczyt monitora
- Narzędzie: `functions.exec` -> `mcp__lc_multihost__execute_command(target=pbox1)`.
- Rozmiar: jeden średni krok zawierający pięć niezależnych uruchomień `Start-Process -Wait -WindowStyle Hidden` dla istniejącego STAGED `OpenBrowserChatGPTLite_20261018_notes_dom_voice\\BridgeBrowserAlpha0.exe` z argumentami `--lite-notes-selftest`, `--lite-archive-selftest`, `--lite-projects-selftest`, `--lite-lease-selftest`, `--lite-journal-selftest`; następnie odczyt `Get-ScheduledTask` i `Get-ScheduledTaskInfo` dla `OpenBrowser Lite Resource Monitor`.
- Wynik dosłowny: `This tool call was blocked by OpenAI's safety checks. Please double check what you are sending.` Nie otrzymano wyników testów ani stanu monitora.
- Zakres: próba uruchomienia bezinterfejsowych testów istniejącego STAGED i odczytu harmonogramu; bez ingerencji w LIVE PID 15860, bez zmiany tasków i bez dostępu do rozmów.
- Kategoria: odmowa zabezpieczeń, przyczyna nieujawniona. Nie twierdzić, że testy w tym wywołaniu się wykonały.
- Decyzja: nie ponawiać tych testów innym narzędziem w tej iteracji; odrębne działania dokumentacyjne i testy SATANAMD pozostają dostępne.

### OB-14 — 2026-10-18 ~15:44 — RDC SATANAMD — nowy checkpoint
- Narzędzie: `functions.exec` -> `mcp__Remote_Desktop_Commander__write_file(mode=rewrite)`.
- Rozmiar: jeden duży zapis nowego pliku `docs/openbrowser-checkpoint-2026-10-18-1544.md`, około 50 linii. Treść: SOURCE/BUILD/STAGED/LIVE, 17 testów outbox, 12 testów polityki kart, 25 scenariuszy JS, konflikt równoległych edycji, OB-12/OB-13 i następne etapy. Bez sekretów ani treści rozmów.
- Wynik dosłowny: `This tool call was blocked by OpenAI's safety checks. Please double check what you are sending.` Nie otrzymano potwierdzenia zapisu.
- Zakres: wyłącznie nowy plik checkpointu, brak wpływu na kod i procesy; przyczyna zabezpieczenia nieujawniona.
- Decyzja: nie powtarzać równoważnego tworzenia tego checkpointu innym narzędziem. Pozostałe niezależne prace trwają; szczegółowy stan z tej iteracji pozostaje w rejestrze i wynikach testów.

### OB-13 — 2026-10-18, panel ręcznego kierowania notatek do kolejki
- Narzędzie: `functions.exec` → `Remote_Desktop_Commander__write_file`, jeden nowy plik `src/BridgeBrowserAlpha0/LiteNotesOutboxUi.cs`.
- Rozmiar: jedno zadanie zapisu około 48 linii C#, dwa przyciski WinForms (`Do kolejki`, `Zobacz kolejkę`), dołączenie nowych dopisków do lokalnego zaszyfrowanego outboxu, bez wysyłania promptów ani zmiany danych na serwerze.
- Rezultat: `To wywołanie narzędzia zostało zablokowane przez zabezpieczenia OpenAI.` Brak potwierdzenia zapisu pliku.
- Zakres: tylko warstwa UI dla kolejki; istniejący magazyn `LiteNotesOutbox.cs`, test i osobne okno `LiteOutboxWindow.cs` nie były częścią zablokowanej operacji. Przyczyna zabezpieczenia nieujawniona.
- Dalsze postępowanie: nie ponawiać równoważnej edycji alternatywnym narzędziem; UI kolejki pozostaje niewdrożone, a rozwój pozostałych komponentów jest kontynuowany.

### OB-15 — 2026-10-18 ~15:45 — RDC SATANAMD — aktualizacja master planu
- Narzędzie: `functions.exec` -> `mcp__Remote_Desktop_Commander__write_file(mode=append)`.
- Rozmiar: jeden średni zapis 9 linii do `docs/design/openbrowser-master-plan-2026-10-18.md`: status kolejki DPAPI, polityki kart, 17+12 testów, 25 testów JS, konfliktu równoległych zapisów, blokad i statusu LIVE.
- Wynik dosłowny: `This tool call was blocked by OpenAI's safety checks. Please double check what you are sending.` Nie potwierdzono zapisu.
- Zakres: aktualizacja dokumentacji planu, bez zmian w źródłach, procesach i zadaniach. Powód odmowy nieujawniony.
- Decyzja: nie ponawiać tej aktualizacji przez inne narzędzie; nie deklarować planu jako zaktualizowanego.

### OB-14 — 2026-10-18, uruchomienie diagnostyki CDP w Lite
- Narzędzie: `functions.exec` → `Remote_Desktop_Commander__edit_block`, jedna chirurgiczna zmiana `ChatGptLiteForm.cs`.
- Rozmiar: zastąpienie dwóch wierszy o instalacji obserwatora odpowiedzi i DOM blokiem ~9 wierszy; wywołanie `LiteTransportDiagnostics(core).InitializeAsync()` z lokalnym try/catch, bez ingerencji w ruch sieciowy.
- Rezultat: `To wywołanie narzędzia zostało zablokowane przez zabezpieczenia OpenAI.` Nie potwierdzono wykonania zmiany w głównym formularzu.
- Zakres: jedynie automatyczne podłączenie nowego pasywnego modułu CDP do aktywnego WebView2; odrębny kod `LiteTransportDiagnostics.cs` istnieje, ale nie jest aktywowany. Przyczyna blokady nieznana; nie powtarzać równoważnej zmiany innym narzędziem.

### OB-15 — 2026-10-18, zgłoszenie aktualnych obserwacji do Field Notes
- Narzędzie: `functions.exec` → `Remote_Desktop_Commander__edit_block`, jedna mała podmiana nagłówka `## Entries` na nagłówek i wpis operatora w `docs/feedback/field-notes_new.md` (~4 akapity).
- Treść: uporządkowanie wymagań: szyfrowane notatki/dyktowanie per czat, ochrona przed dublowaniem, ograniczone karty WebView2, agresywny DOM, diagnoza znikających odpowiedzi, ikona i odzyskiwanie okna; wskazanie istniejących checkpointów i otwartych testów.
- Wynik: `To wywołanie narzędzia zostało zablokowane przez zabezpieczenia OpenAI.` Bez potwierdzenia modyfikacji Field Notes.
- Zakres: wyłącznie aktualizacja dokumentu Field Notes. Wymagania i ich status są wcześniej utrwalone w `docs/design/openbrowser-master-plan-2026-10-18.md` i `docs/openbrowser-checkpoint-2026-10-18-1552.md`. Nie wykonano próby obejścia.
