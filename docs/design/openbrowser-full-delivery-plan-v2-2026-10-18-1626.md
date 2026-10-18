# Open Browser Lite — kompletny plan wdrożenia v2.0 — 2026-10-18 16:26 CEST

Zakres: do ukończenia WSZYSTKICH omówionych funkcji. Priorytety uporządkowane, ale żaden etap końcowy nie znika z backlogu. Każdy punkt przechodzi SOURCE -> TEST -> STAGED -> LIVE VERIFIED; niedowodnione nie jest DONE.

## Reguły wykonania
- **NOWE nadrzędne polecenie 18.10.2026 18:16 CEST:** żadnych automatycznych przypominajek ani nowych zadań ChatGPT. Wszystkie 32 zapisane automatyzacje ChatGPT są wyłączone. Rozwój tej aplikacji wyłącznie w bieżącej rozmowie do odwołania. Usługi techniczne Autosync/monitor zasobów nie są przypominajkami.
- Zweryfikowane odzyskiwanie: `docs/openbrowser-audit-recovery-2026-10-18-1828.md` wraz z sumami SHA256 i rozróżnieniem LIVE/STAGED/SOURCE.
- SATANAMD `D:\projects\open-browser` to źródło; PBOX1 klon i wersjonowane wydania; Autosync na autosync/SATANAMD bez ręcznych push.
- Nie wstrzymuj pracy przy pierwszym blockerze. Realizuj inne niezależne zadania, loguj dokładnie odmowy w `docs/Rejestr nieudanych komend.md` z unikalnym ID, nie obchodź odmów alternatywnym narzędziem.
- Każdy krok prowadź od małej implementacji do testów negatywnych, side-by-side release i realnej obserwacji; nie przerywaj aktywnej odpowiedzi aktualnego WebView2.
- Model projektu domyślnie w trybie **High**, jeśli ustawienie jest dostępne. Rejestruj odchylenie od High (np. Instant/Medium), proponuj poprawkę użytkownikowi, nie zakładaj możliwości samodzielnego przestawiania ustawień ChatGPT bez udokumentowanej drogi. Nie przypisuj automatycznie blokad OpenAI ustawieniu Instant: przyczynę wymagają dowodów; zabezpieczeń nie obchodzić.
- Godziny w interfejsie lokalne Europe/Warsaw z jednoznaczną strefą, timestamp kompilacji pochodzi z builda, nie z czasu uruchomienia ani nadpisania pliku.

## P0: natychmiast — wersja i nadzorca rozmów
1. [PARTIAL: v1.64.0 LIVE na PBOX1; manifest SHA potwierdzony] Wersja czytelna w titlebar oraz toolbar z datą i godziną *zbudowania* (Europe/Warsaw), SHA źródła i sprawdzalną ścieżką EXE; na PBOX1 plik `lite-release-manifest.json` (nie `openbrowser-release-manifest.json`) ma poprawny SHA, lecz oznaczenie `channel=staged` nadal wymaga uporządkowania; testy UI i odtworzenia wydania pozostają TODO.
2. [TODO] Panel diagnostyczny: wersja aplikacji, WebView2, kanał wydania, PBOX1/SATANAMD, ostatnie uruchomienie, repo SHA, pomiary CPU/RAM.
3. [TODO] Wykrywanie faz odpowiedzi: initial, submitted, generating, tool pending, streaming, final stable, crashed/unresponsive, needs_user, done/all_tasks_completed, blocked, unknown. Multi-signal + stabilizacja >2 próbek; timeout != completed.
4. [TODO] Pobieranie **ostatniej widocznej odpowiedzi** i kontekstu blokad, checkpointów oraz niezrealizowanego backlogu. Przed przesłaniem do zewnętrznego API redakcja sekretów, ograniczenie długości, jawny zakres i zaufane dane.
5. [PARTIAL: kod Luna planner i wcześniejszy test syntetycznego API PASS, LIVE E2E nadal TODO] Integracja Luny przez obsługiwane API z decyzjami CONTINUE / WAIT / ASK_USER / STOP i limitami kosztów. Kod źródłowy `LiteLunaAdvisor.cs` ma fallback na `LiteApiCredential.ReadSharedKey()` (`FileScannerAI/OpenAIAPIKey` w Windows Credential Manager) i model konfigurowalny; zmienne `OPENAI_API_KEY` i `OPENBROWSER_LUNA_MODEL` nie były ustawione dla użytkownika PBOX1. Audyt wskazuje wymóg zmiennych w praktyce — trzeba rozstrzygnąć faktyczną dostępność poświadczenia z procesu LIVE, bez wyświetlania klucza. Brak automatycznego nadzoru innych projektów i brak wysyłania promptów przy niepewnym statusie.
6. [TODO] Luna powinna pisać do czatu jak współpracownik: „Kontynuuj pozostałe zadania, jeżeli są jednoznaczne, pomiń zablokowane i je zgłoś; jeśli wszystko wykonane, nie twórz nowych zadań; jeśli odpowiedź niekompletna, wskaż co brakuje”. Bez ogólnego „kontynuuj” w pętli, bez zgadywania zezwoleń ani namawiania do obchodzenia zabezpieczeń.
7. [PARTIAL: DPAPI ledger i testy PASS, E2E pending] Trwały nadzorca jednego czatu: lease/fencing/once-only request key, zapis planu, re-check bezpośrednio przed wysłaniem; dwufazowy status Pending -> SendingUncertain -> Delivered (lub NeedsReview), crash recovery bez ponowienia niewiadomych wysłań.
8. [PARTIAL: race tests PASS, real WebView2 E2E pending] Wysłanie przez legalną/obsługiwaną warstwę UI WebView2 przy potwierdzonym idle/kompozytorze, ack widocznej *konkretnej* wiadomości; brak prób podwójnych po błędzie; absolutnie nie automatyzować captcha, logowania, płatności i odmów zabezpieczeń.
9. [TODO] Przy aktywnym domyślnym nadzorze: w panelu można pauzować/wyłączyć, wskazać czaty/projekty, limity wywołań na godzinę/dobę, allowlist tematu i dozwolonego działania; awaria -> alarm + STOP, a nie auto-resubmit.
10. [PARTIAL: czat przypięty, 1 ręczny prompt High, Lite E2E pending] E2E na czacie „testowy” z książką przygodową: najpierw ręczne przypisanie URL, bezpieczna próbka 1 rozdział, potem różne stany: naturalne zakończenie, blocker, prośba o użytkownika, niejasne zakończenie, timeout, brak API, długa odpowiedź, awaria/restart, poprawne zatrzymanie po zakończeniu całej książki.
11. [PARTIAL: v1.64.0 LIVE PBOX1 18.10.2026 20:10, PID 18412; 16/16 selftestów i manifest SHA PASS wg audytu/wdrożenia Claude Code, EXE/manifest zweryfikowane w tej sesji] Release minimalne P0: side-by-side build oraz uruchomienie LIVE wykonane; nadal TODO prawdziwy end-to-end Luna/High/reaktywność WebView2, sprawdzony rollback i spójne oznaczenie kanału manifestu (plik produkcyjny nadal ma `channel=staged`).

## P1: diagnostyka i zachowanie rozmów
12. [PARTIAL] Zewnętrzny monitor CPU/RAM co 2 minuty przetrwał reboot, metryki i logowanie; rozszerzyć o normalized CPU/process trees, private bytes, commit, progi niedoboru RAM.
13. [PARTIAL] Lite event log WebView2/lifecycle, DOM obserwatory, błędy renderowania, DPAPI archive; połączyć telemetrię etapów żądania -> UI -> snapshot, korelacje bez payloadów.
14. [PARTIAL: analizator incydentów SOURCE i realny odczyt logów PBOX1, dalsza korelacja nadal TODO] „Nie udało się wczytać odpowiedzi”: porównanie etapów transportu, WebSocket, React, renderera, archiwum oraz kodu błędu z confidence i dowodem tego, co realnie dotarło. Nie udawać wiedzy o backendzie.
15. [TODO] Gwarantowany prywatny checkpoint widocznej odpowiedzi w trakcie streamu, samoweryfikacja i wybiórcze odtwarzanie fragmentów; osobne backupy, policy retencji.
16. [PARTIAL / PROBLEM CONFIRMED] DOM: przy realnej długiej rozmowie PBOX1 w 136 ostatnich próbkach telemetrii występowało 0 rozpoznanych tur / 0 odciążonych, mimo obsługi CSS content-visibility — obecny selektor `[data-testid^="conversation-turn-"]` nie jest wiarygodny dla bieżącej struktury strony (albo w danym widoku tur nie ma). SOURCE LiteDomSelectorAudit.js jest wyłącznie metadanym odczytem alternatywnych selektorów, 4 syntetyczne testy PASS; do rzeczywistego DOM QA i korelacji z `[data-message-author-role]`, bez zawartości rozmów. Dopiero po weryfikacji React E2E bezpiecznie przycinać starsze wiadomości (24/90), z wyłączonym wysyłaniem w trakcie streamingu, zapisem checkpointów i rollbackiem.
17. [TODO] Zasoby po awarii: eksport raportu dla operatora, timeline, błędy/stany/zwieszenia; zero zapisu sekretów do Git.

## P2: dashboard i projekty
18. [PARTIAL] Rejestr dowolnej liczby projektów, wieloczaty i przypięcie URL, ręczna rola lead; wersjonowanie schemy, CRUD, filtry, archiwizacja, import obecnych z ChatGPT.
19. [TODO] Karty dashboardu: per projekt lead/worker, stan pracy, godziny, tokeny lub koszt jeśli udokumentowany, CPU/RAM/DOM, ostatni checkpoint, blokery, logi, skróty do aktywnej karty.
20. [PARTIAL] Trwała ewidencja zadania i lease/fencing, historii handoff; jedna spójna idempotentna transakcja przejęcia roli i dobry rollback.
21. [PARTIAL] Natywny per-chat panel notatek z Win+H; naprawić microphone permissions web ChatGPT, wznowienie dyktowania, copy, obsługę focus i zapisu.
22. [PARTIAL] Zaszyfrowana kolejka notatek per czat oraz ack; dołączyć UI i bezpieczne **jednokrotne** przekazanie „Informacje od Pawła:” do właściwego czatu; nie przerywać trwającego tool call.
23. [TODO] Karty pracy utrzymywane przez adaptacyjny bounded warm pool WebView2 (1 foreground + max 2 warm, według RAM); odstawianie tylko idles, UI notifies.
24. [PARTIAL: v1.64.0 LIVE; QA Windows 16 px nadal TODO] Własny wygląd: osadzona w EXE ikona Open Browser w 8 rozmiarach (16–256 px) oraz status Gotowe/Generowanie/Błąd w tytule okna dostępne w wydaniu 1.64.0 na PBOX1. Zgłoszenie operatora 18.10: poprzednia ikona nieczytelna i brak czytelnej informacji na pasku zadań. Nadal TODO realne wizualne QA 16 px / przypiętej ikony i tooltipa, przepięcie Start/taskbar przez właściciela, pin/cache Windows, natywne nakładki postępu i statusu, Ctrl+Tab, onboarding bez danych kont.

## P3: koordynacja oraz odporność
25. [TODO] Własny lokalny MCP server z register_chat/heartbeat/task_checkpoint/lead_handoff/report_blocker/notes_ack, z uprawnieniami i kontrolą kto jest kim; nie ufać bezkrytycznie samoopisowi.
26. [TODO] Obsługa autoryzacji, ról lead/worker/support, handoff do nowego czatu i archiwizacja starych danych bez przypadkowego przecieku.
27. [TODO] Rozpoznawanie zakończenia zadania, nie jedynie zatrzymania UI; bezpieczne kontynuowanie po restarcie przez własny nadzorca Open Browser. **Bez godzinowych przypominajek ChatGPT.** Przed E2E supervisor jest OFF/opt-in.
28. [TODO] Panel dodawania MCP, test dostępności/instalacji, brak wklejania sekretów do czatu; infrastruktura pluginów zgodna z aktualną platformą.
29. [TODO] API Luna/niższy model jako konfigurowalne strategie, monitor rachunków/timeout/429, fallback na WAIT lub model zatwierdzony, logger kosztów bez ujawniania promptu.

## P4: produkcyjne zakończenie
30. [TODO] Wielogodzinny soak test CPU/RAM /10k tur/hard prune na długich rozmowach, autostart, bateria, standby, reboot, crash/back-pressure; brak memory leak assumptions.
31. [TODO] Spójny system konfigurowania projektów i rozdzielenie ChatGPT native Projects od lokalnych aliasów bez założeń o publicznym API.
32. [TODO] Testy integracyjne 2 hosty, różne czaty/projekty, równoległe handoffy, blokady, offline API i rollback; QA wizualne Windows11.
33. [TODO] Polityka prywatności, backup/retencja, eksport i import ustawień bez cookies; szyfrowanie i prywatny katalog użytkownika Windows.
34. [TODO] Wersjonowany instalator i updater, kontrolowany restart i bezpieczne uwolnienie procesu, sygnatura, równoległy STAGED, promocja do LIVE i rollback.
35. [TODO] Dokumentacja operatora i dewelopera, rejestr odrzuconych komend, dowody końcowego testu, wszystkie pozycje DONE i live verified, kompletny stabilny Open Browser.

## Stan na początek 16:25
- PBOX1 **LIVE zmienił się od poprzednich checkpointów**: PID 18676, start 16:16:29, aktualna binarka %LOCALAPPDATA%\Programs\OpenBrowserChatGPTLite_20261018_notes_dom_voice\BridgeBrowserAlpha0.exe, Responding=True. Nie uśmiercać.
- PBOX1 Windows scheduled task nadal wskazuje starszy telemetry, co wymaga osobnego rozwiązania wcześniej zablokowanego problemu i nie jest jeszcze DONE.
- Zdania o przyczynach odmów narzędzi wynikających z Instant traktować jako hipotezę operatora, nie fakt potwierdzony przez telemetrię.
