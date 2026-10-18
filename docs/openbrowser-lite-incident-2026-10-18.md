# OpenBrowser Lite - incydent renderera i reakcja - 2026-10-18

Cel: zachowac ustalenia przed restartem, zbadac przyczyny i wprowadzic diagnostyke bez utraty profilu ChatGPT.

## Zgloszenie operatora
- WebView2 pasek w Open Browser: `Błąd procesu strony: RenderProcessUnresponsive`; strona nie odpowiada, nie mozna przełączyć czatów.
- ChatGPT Desktop: `Nie udało się wczytać tej odpowiedzi`, `Przetwarzano przez 6m 46s`, `Porządkując następne kroki`; inny widok pokazywal `Przerwano rozumowanie`.
- Brak dowodu czy odpowiedz zostala wygenerowana/wyslana przez serwer i utracona po renderowaniu; brak archiwizacji wiadomosci w Lite.
- W bocznym panelu innego ChatGPT widać czerwone oznaczenia przy części czatów, lecz samo znaczenie ikonek nie jest diagnostycznie potwierdzone.

## Snapshot z PBOX1 przed restartem
- 2026-10-18 12:57:54 lokalnie: BridgeBrowserAlpha0.exe --chatgpt-lite PID 48284, sesja 1, start 09:10:58, Responding=True (to NIE dowodzi dzialania renderera), MainWindowHandle=0 w odczycie zdalnym.
- WebView2: 8 procesów, renderer PID 51696 od 12:14:47 z 674,39 s CPU i 248,2 MiB RAM; GPU PID 36648 CPU 246,53 s; browser PID 51284.
- Wtedy pamiec wolna systemu ~360 MiB. Następny pomiar: RAM zainstalowany ~16 218 MiB, wolny ~85 MiB, zajetosc pagefile ~6428 MiB.
- Najwieksze procesy w chwili drugiego odczytu: msedge ~1842 MiB, ChatGPT ~871 MiB, Claude ~543 MiB, Memory Compression ~525 MiB, inne WebView2 i Firefox. Obciazenie ogólnosystemowe, nie tylko Lite.
- WebView2 Evergreen zainstalowane: 155.0.4283.45 i 155.0.4283.60. Brak potwierdzenia, która wersja byla uzywana przez uruchomiony profil.
- Wersja aplikacji Lite 1.0.0.0, tryb --chatgpt-lite, profil %LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\Profile.
- Kod src/BridgeBrowserAlpha0/ChatGptLiteForm.cs: tylko status w UI przy CoreWebView2.ProcessFailed, bez stalego logu. Chrome error reason, network lifecycle i state odpowiedzi nie sa archiwizowane.

## Blokady, przestoje i niedokonane operacje
- Narzedzie: LC MultiHost PBOX1, `execute_command` odczyt Get-WinEvent Application ID 1000,1001,1002,1026 i System ID 2004 (max 30, zakres 6h), WER metadata. Calosc pojedyncze zapytanie diagnostyczne; nie wykonywalo zmian.
- Rezultat: przez ponad 200s `status=running`, następie brak polaczenia przy próbie cancel_current_command (`target_unavailable`, timeout); sesja PowerShell LC MultiHost `shell_lifecycle=crashed`, `last_shell_end_reason=unexpected_exit`. Żaden wynik zdarzeń nie został uzyskany.
- Nie ponawiano tej operacji innym narzedziem.
- RDC na PBOX1 zdolal zapisac wstepny lokalny raport: %LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\diagnostics\incident-2026-10-18.md (potwierdzenie completed).

## Interpretacja: hipotezy, nie pewniki
- Microsoft: `RenderProcessUnresponsive` to brak odpowiedzi procesu renderowania, moze powodowac dlugi skrypt albo zajetosc systemu, event pojawia sie wielokrotnie. https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/process-related-events
- Windows moze przy malej wolnej pamieci stronic mocno na dysk. Zmierzone ~85 MiB RAM i >6 GiB pagefile to silny sygnal presji, ale bez temporalnego logu nie dowodzi bezposredniej przyczyny freeze.
- We wrzesniu i pazdzierniku 2026 pojawily sie publiczne zgloszenia regresji WebView2; nie wolno utozsamiac ich z naszym przypadkiem bez wersji renderera i crash dumpu.
- Reddit: istnieja relacje, ze dlugie rozmowy ChatGPT i aplikacja Windows czasem zwalniaja lub nie pokazuja odpowiedzi. Anonimowe relacje nie sa dowodem przyczyny incydentu.
- Dla utraty odpowiedzi trzeba rozdzielic: 1) odpowiedz nie wyszla z serwera; 2) wyszla, ale nie dotarla do klienta; 3) dotarla, nie zostala zapisana; 4) zapisano, ale klient nie umial jej wyrenderowac. Obecna Lite nie ma danych rozstrzygajacych te przypadki.

## Plan techniczny
1. Niezalezny, minimalny monitoring procesów, pamieci, CPU i stanu tasku na PBOX1, z limitami retencji; bez danych konta i tresci czatu.
2. W trybie Lite logowanie zdarzeń CoreWebView2, informacji o błędach, start/stop procesu, czasu nawigacji i heartbeat UI (brak ingerencji w przesylane wiadomosci).
3. Dodac bezpieczny sposob recznej rekonstrukcji WebView2, nie kasujac profilu ani aktywnej rozmowy.
4. Walidacja kompilacji, instalacja nowej wersji na PBOX1 dopiero po kontroli i odnotowaniu checkpointu. Zasady repo: edycja plików przez RDC, system commands przez LC, Autosync bez recznego push.
5. Niezależne od tego etap późniejszy: monitoring zmian/odpowiedzi widocznych w interfejsie, checkpointy i handoffy, świadoma polityka prywatności; nigdy nie obiecywać odzyskiwania danych niezapisanych po stronie usługi.

## Checkpoint wdrozenia instrumentacji
- Dodano `src/BridgeBrowserAlpha0/LiteEventLog.cs`: trwałe JSONL (UTF-8 bez BOM), rotacja dzienna i maksymalny rozmiar 10 MiB na dzien, retencja 14 dni, bez tresci rozmów, pelnych URL, cookies i tokenow.
- `ChatGptLiteForm.cs` emituje: window_constructed, ui_heartbeat co 30 s, window_closing, webview_initializing, webview_initialized (dokladna wersja Chromium), navigation_start (tylko host), navigation_completed (sukces/bląd), webview_process_failed (kind/reason/exitCode), webview_initialization_failed (typ błędu/HResult).
- Plik docelowy na komputerze uruchomieniowym: %LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\diagnostics\lite-events-YYYY-MM-DD.jsonl.
- Kompilacja Debug SATANAMD: 0 błędów, znane ostrzezenie MSB3277. Release build przez LC MultiHost SATANAMD `dotnet build ... --configuration Release --no-restore` zablokowany: MSB3021/MSB3027, exe zajety przez istniejący proces PID 13384. Zakres: zamiana jednego pliku hosta, inne skompilowane zrodla pozostały nietknięte. Nie probowano ponawiac tej samej operacji.
- Aktualna wersja PBOX1 pozostaje starsza: kod po kompilacji Debug na SATANAMD NIE JEST JESZCZE WDROŻONY na PBOX1 i nie wolno twierdzić inaczej.
- Zapytanie LC MultiHost o eventy PBOX1 zablokowalo sie na ponad 200 s; sesja zakończona jako crashed; brak danych WER; nie ponawiano zapytania w innym narzędziu.

## Checkpoint synchronizacji i wdrozenia
- SATANAMD Autosync manualnie wyzwolony przez istniejacy Windows Scheduled Task `Autosync repozytoriów`. Wersja gałęzi autosync/SATANAMD: 2b769de1f5aeb90524f94beced8fd5cd6ccf9407.
- PBOX1: git pull --ff-only wykonany, czysty katalog przed synchronizacją, commit 0fe0087 -> 2b769de, plik LiteEventLog.cs jest w klonie. Bez ręcznego git push.
- PBOX1: przygotowanie osobnego katalogu aplikacji `OpenBrowserChatGPTLite_20261018_telemetry`, bez zastępowania aktualnego exe i bez ruszania profilu. Publikacja w toku.
- Istniejaca aplikacja na SATANAMD PID 13384 nie zatrzymana; kompilacja Release do starego output była zablokowana plikiem uruchomionej aplikacji. Kompilacja Debug z nowym kodem powiodła się.

## Wynik wdrozenia na PBOX1
- PBOX1 `dotnet publish -c Release --no-restore -m:1` PASS, 0 błędów, znane ostrzezenie MSB3277; instalacja wersjonowana `%LOCALAPPDATA%\Programs\OpenBrowserChatGPTLite_20261018_telemetry`. Stara instalacja pozostala w oryginalnym katalogu.
- Kontrolowane zatrzymanie TYLKO zadania `OpenBrowser ChatGPT Lite`, PID 48284 zamkniety, bez usuwania profilu i bez zamykania innych przegladarek/ChatGPT.
- Aktualizacja zadania i skrotow Desktop/Start do nowej instalacji, restart. Task Running, proces PID 49928 `Responding=True`.
- PBOX1 zapisuje `diagnostics/lite-events-2026-10-18.jsonl`: `webview_initialized browserVersion=155.0.4283.60`, `navigation_start host=chatgpt.com`, `navigation_completed IsSuccess=true`, `ui_heartbeat` co ~30 s. Nie testowano logowania ani poprawnosci generowania odpowiedzi.
- PBOX1 po restarcie: 7 procesow WebView2; wolne RAM ~788 MiB w jednym pomiarze, ~631 MiB w kolejnym; pagefile używany ~5455 MiB (monitor).
- Zapisano skrypt niezależnego monitoringu zasobów `scripts/monitor-lite-resources.ps1` i ukryty launcher `scripts/monitor-lite-resources-hidden.vbs` do katalogu diagnostyki na PBOX1. Pierwszy odczyt `resources-2026-10-18.jsonl` PASS: hostCount=1, webviewCount=7, 631 MiB free, 5455 MiB zajete pagefile.
- Zablokowana/wolna operacja LC MultiHost PBOX1: rejestracja zadania `OpenBrowser Lite Resource Monitor` przez `Register-ScheduledTask` / `Set-ScheduledTask` w jednym wywolaniu i `Start-ScheduledTask`. Po ponad 100 s brak odpowiedzi (status running), następnie `target_unavailable` timeout przy status check. Nie powtarzać rejestracji. Status zadania harmonogramu należy potwierdzić odczytem; nie deklarować automatycznej cykliczności jako PASS bez weryfikacji.

## Ostateczny checkpoint tej sesji
- 2026-10-18: Potwierdzono zadanie PBOX1 `OpenBrowser ChatGPT Lite` Running, nowy exe/PID 49928, WebView2 155.0.4283.60. Zdarzenia `webview_initialized`, `navigation_completed true`, `ui_heartbeat` trwale zapisane w pliku JSONL.
- Manualne wywolanie `monitor-lite-resources.ps1`: PASS, plik `resources-2026-10-18.jsonl`, m.in. 631 MiB RAM wolne, 5455 MiB pagefile, 1 host, 7 WebView2.
- Automatyzacja 2-minutowego monitorowania NIE DZIALA: rejestracja/uruchomienie Windows task przez LC MultiHost byla za duza dla zablokowanej sesji; po >195 s anulowano, re-check jedynie odczytowy: `MONITOR_INSTALLED=NO`, `MONITOR_ROWS=1`. Operacji tworzenia nie ponawiano innym narzedziem. Skrypty i wynik manualny zachowane do kolejnej sesji.
- Diagnostyka server->client odpowiedzi ChatGPT NIE ma jeszcze telemetrii/dowodów transportu; nie wolno stwierdzać, czy odpowiedź dotarła, lecz znikła. Rowniez wczesniejsza odpowiedz nie jest odzyskiwalna wprost z instrumentacji dodanej dopiero po incydencie.
- Program nie wysyla automatycznie wiadomosci, nie klika kontynuacji i nie ingeruje w profile innych przegladarek. Wersja sprzed zmiany zachowana do ewentualnego rollbacku.

## Aktualizacja po instalacji monitora przez operatora — 2026-10-18, 13:44
- Użytkownik uruchomił ręcznie poprawiony skrypt rejestrujący zadanie przez `schtasks.exe /Create /SC MINUTE /MO 2 /IT` po niepowodzeniu wariantu `New-ScheduledTaskTrigger -Daily` (brak właściwości `Repetition.Interval`).
- Weryfikacja wykonana przez LC MultiHost PBOX1, bez zmian: zadanie `OpenBrowser Lite Resource Monitor` istnieje, `State=Ready`, `LastRunTime=2026-10-18T13:43:43+02:00`, `LastTaskResult=0`, `NextRunTime=2026-10-18T13:45:45+02:00`.
- Dziennik `resources-2026-10-18.jsonl` zawiera 3 wiersze, aktualizacja 13:43:30 lokalnie, ostatnia próbka 596 MiB wolnej RAM, 1 host i 9 procesów WebView2.
- ZADANIE MONITORUJĄCE AKTYWNE i poprawnie wykonuje próbki; wcześniejszy status NIEAKTYWNE jest historyczny, nie bieżący.
- Ponieważ zadanie utworzono z `/IT`, wymaga zalogowanej sesji Windows. Trwałość konfiguracji Task Scheduler jest systemowa, ale jeszcze nie zweryfikowano wykonania pomiaru po restarcie komputera.

## Nowy incydent: konieczny restart calego PBOX1 — 2026-10-18 13:53
- Użytkownik zgłosił całkowitą nieresponsywność komputera PBOX1, wymuszającą restart. Po restarcie sesja Open Browser Lite działa i wyświetla ChatGPT (zrzut dostarczony w rozmowie).
- LC MultiHost PBOX1 potwierdził boot time: 2026-10-18 13:53:23+02:00. Snapshot po restarcie 14:00: 9121 MiB RAM wolne, vs 274 MiB o 13:45; nie wolno uznawać presji pamięci za potwierdzoną przyczynę awarii.
- Ostatnie próbki przed rebootem: 13:41 free=831 MiB/pagefile=6069 MiB, 13:43 free=596/pagefile=6148, 13:45 free=274/pagefile=6061, 13:48 free=1004/pagefile=6299.
- W tych próbkach największe procesy: msedge ~1611-2445 MiB, aplikacja ChatGPT ~842-940 MiB, Claude ~549-660 MiB, procesy WebView2 i Memory Compression. Zasoby obciążone globalnie, brak dowodu wycieku Lite.
- Przeliczenie przyrostów CPU renderer WebView2 PID 31112: 13:41-13:43 ~16,4% jednego rdzenia, 13:43-13:45 ~28,0%, 13:45-13:48 ~20,9%; GPU WebView2 ~2,5-6,9% jednego rdzenia. Nie wykazano pojedynczego renderera konsumującego cały rdzeń CPU.
- Lite `ui_heartbeat` działał z lukami około 30 s aż do 13:52:54, tuż przed restartem, bez nowego `webview_process_failed` i bez `window_closing`. Heartbeat WinForms NIE sprawdza przetwarzania JavaScript w ChatGPT ani reakcji systemu na wejście myszy/klawiatury.
- Po restarcie Lite automatycznie/ponownie uruchomił PID 15860, `webview_initialized browserVersion=155.0.4283.60`, `navigation_completed IsSuccess=true`, heartbeat co 30 s. Screenshot użytkownika przedstawia renderowaną treść i napis „Gotowe”.
- Monitoring Windows Task Scheduler przetrwał restart. O 13:59:59 `LastTaskResult=0x800710E0` (odmowa wykonania przez operatora/administratora, chwilowy), ale o 14:01:01 zadanie wykonało się z `LastTaskResult=0` i dopisało próbkę. Nie oznaczać monitoringu jako stale niesprawnego.
- Task `OpenBrowser Lite Resource Monitor` nadal ma `DisallowStartIfOnBatteries=True` i `StopIfGoingOnBatteries=True`; laptop nie gromadzi próbek na baterii. Do poprawy po świadomej decyzji, bez powtarzania wcześniejszej zablokowanej rejestracji.
- Priorytet: pasywny detektor zawieszenia WebView2/JS niezależny od WinForms, rozróżnienie statusu backend i renderera, alert krytycznego memory/commit pressure, poprawny sampling CPU per sekunda, minimalny kosztem zasobów. Nie wznawiać modelu automatycznie po samym braku widocznego tekstu.
