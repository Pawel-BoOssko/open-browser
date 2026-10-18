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
