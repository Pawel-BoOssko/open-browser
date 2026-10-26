# Open Browser Lite — obowiązujące zasady operacyjne
Data decyzji: 2026-10-25. Właściciel: projekt Open Browser Lite.

## 1. Jedyne środowisko produkcyjne
- **SATANAMD**: jedyny host SOURCE, kompilacji, testów GUI, LIVE, silnika wybudzeń, wdrożeń i rollbacku.
- **PBOX1 jest poza zakresem**. Żadnych wdrożeń, restartów ani skryptów OBL na PBOX1. Stare prompty mówiące „wdrażaj na PBOX1” są nieaktualne i nie wolno ich wykonywać.
- Dostęp operacyjny: **LC MultiHost, target=satamad**, sprawdzać COMPUTERNAME=SATANAMD. Nigdy nie używać domyślnego target=pbox1 dla tej pracy.

## 2. Najważniejsza zasada: ostrzony nóż ma być używany
**Cyklem podstawowym jest: POPRAWKA → TESTY → STAGED → WDROŻENIE NA SATANAMD → POTWIERDZENIE LIVE → KORZYSTANIE Z NOWEGO LIVE PRZY TWORZENIU NASTĘPNEJ POPRAWKI.**
- Nie kumulować wielu ukończonych poprawek offline bez aktualizowania działającej wersji.
- Nie kończyć iteracji po samych testach. Jeżeli przejście na LIVE jest możliwe i akceptowalne, należy uruchomić nową wersję i pracować odtąd właśnie w niej.
- Gdy release nie jest bezpieczny, zapisać jawnie **NO-GO: powód, aktualne LIVE, kandydat, rollback i krok odblokowania**. Nie udawać wdrożenia.
- Decyzja właściciela: preferować szybkie, małe wdrożenia z odwracalnym rollbackiem zamiast długiego czekania na perfekcję.
- Rozróżniać cztery dowody: testy źródeł, testy uruchomionego EXE, **rzeczywisty proces LIVE na SATANAMD**, efektywność odczuwalna w codziennej pracy.

## 3. Niezawodność wybudzeń i samodoskonalenie
- OBL jest narzędziem PRACY TEGO CZATU. Celem jest ciągła, samodzielna i wydajna praca nad OBL, a nie samo generowanie raportów czy kolejnych testów offline.
- P0: wybudzenie, bezpieczne wznawianie zadania, tylko jeden aktywny czat wykonawczy, brak duplikacji niepewnych wysyłek, CHECKPOINT i ACK potwierdzony dowodem silnika.
- Silnik wybudzania OBL jest źródłem harmonogramu; nie używać ChatGPT Tasks.
- Dziennik silnika: %LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\diagnostics\wake-lifecycle.jsonl na SATANAMD. Nigdy nie fabrykować ACK.
- Status realizacji i checkpoint: D:\projects\open-browser\docs\OPENBROWSER_LITE_CHECKPOINT.md oraz OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md na SATANAMD.
- Każde wybudzenie rozpoczyna się od odczytu bieżących LIVE, SOURCE, stanu silnika i checkpointu; kończy zapisem faktycznie wykonanych czynności, ich weryfikacją oraz następnym krokiem.
- Po faktycznym restarcie LIVE konieczne potwierdzenie pojedynczego PID, wersji, WebView2, działania UI, dziennika i rollbacku. Nie uruchamiać równolegle drugiego LIVE.

## 4. Kryteria prac i zasoby
- RAM/DOM/CPU: ostre ograniczanie starej zawartości DOM, archiwizowanie do trwałych plików bez utraty danych, testy na długich rozmowach, kontrola kosztów zasobów.
- Nowe funkcje weryfikować w prawdziwej interaktywnej sesji Windows; testy statyczne w Pythonie nie dowodzą działania PowerShell/C#/WebView2.
- Nie robić ręcznie git commit/push/reset. Nie przerywać niepewnej wysyłki i nie powtarzać jej bez dowodu.
- Rzeczywiste wersje: od 2026-10-25 13:50:59 CET LIVE 1.72.95 PID51892, poprzednia 1.72.92 jako fallback (potwierdzić przy następnym odczycie, bo PID może się zmienić).

## 5. Zakaz regresji konfiguracji
- Każda aktualizacja instrukcji wybudzeń musi wymieniać tylko SATANAMD. Błędne, starsze opisy hostów nie są wiążące.
- Naprawić zapisany prompt harmonogramu OBL u źródła przez zweryfikowane API/serializację i odczyt po zapisie; nie edytować DPAPI jako zwykłego tekstu.
- Przy każdym wydaniu sprawdzić, czy nowa instancja jest uruchomiona i nadaje się do dalszego własnego rozwoju; dopiero wtedy przejść do następnej poprawki.

## 2026-10-26 17:25 CET — Nowsza decyzja właściciela: wymuszone wdrażanie i auto-recovery (NADRZĘDNE)
- Właściciel jednoznacznie zmienił wcześniejszą zasadę czekania na idle: wdrożenie NIE czeka na zakończenie generowania, szkic, toolBusy ani stare odpowiedzi. Proces OBL należy wymuszenie zakończyć, potem uruchomić nowy sprawdzony EXE, zweryfikować jeden PID i GUI, a przy błędzie wykonać rollback.
- Obowiązuje wyłącznie host SATANAMD, LC MultiHost target=satamad. Zakaz PBOX1, ChatGPT Tasks, ręcznego Git commit/push.
- Kanoniczny skrypt wymuszonego wdrożenia: D:\projects\open-browser\scripts\obl-force-deploy-satanamd.ps1 -Mode Deploy. -Mode Restart dotyczy obecnego LIVE i napraw awarii. Skrypt używa wersjonowanego EXE/SHA, zabija wyłącznie BridgeBrowserAlpha0 z jego procesami potomnymi, stosuje blokadę Keepalive, sprawdza zdrowie GUI i atomowo zapisuje release state.
- Windows Scheduled Task OBL Force Recovery SATANAMD co 5 minut uruchamia scripts\obl-watch-recover-satanamd.ps1. Wykrywa queued release >1min, brak procesu, brak świeżego preflight >5min, wybudzenie z ponad trzema odroczeniami i brakiem ACK >=25min. Wtedy wymusza procesowy restart; 10min cooldown ogranicza restart loop.
- Zachowaj w dzienniku fakty, nie wymyślaj ACK. Przed restartem checkpoint, ale nie czekaj na stop/generating/draft/toolBusy. Użytkownik zaakceptował utratę trwającej niezapisanej odpowiedzi w zamian za brak wielogodzinnej blokady.
- Pozostałe dotychczasowe instrukcje o bezwzględnym wymogu idle przed restartem są uchylone w zakresie wdrożeń i auto-recovery.
