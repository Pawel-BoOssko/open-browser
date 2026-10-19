# Open Browser Lite 1.71 — wznawianie czatu: diagnoza i poprawka
Data: 2026-10-19 07:31 CEST. Bez przypominajek ChatGPT i bez ręcznych commitów/pushy.

## Stan PBOX1
- LIVE v1.70 PID 8704, DPAPI Enabled=true, 25-minutowy interwał, timer aktywny.
- Log PBOX1: co najmniej 18 par dev_wake_send_attempt / dev_wake_deferred_busy, brak dev_wake_ack w ostatnich pomiarach. Nadzorca działa, lecz sender odrzuca wiadomości przed wysłaniem.
- Źródło v1.70: busy() szukało globalnie dowolnego aria-busy=true albo Stop, bez weryfikacji czy to aktywne generowanie. Aria-busy w innej sekcji strony mogło powodować false positive; dokładny element w konkretnej produkcyjnej sesji nie został jeszcze ustalony.

## Zmiany SOURCE 1.71
- Sender: busy oparty na widocznym i aktywnym przycisku Stop; globalne aria-busy innych sekcji nie blokuje. Niewysłany szkic nadal blokuje.
- Sender.diagnose: tylko metadane i liczby, nigdy prywatne teksty. Oddzielne kody busy_missing_composer, busy_generating, busy_draft_present.
- LiteDevWakeUi: specyficzny busy przed dotknięciem edytora oznacza definitely-not-sent, 2-minutowy retry, raport przyczyny. Brak automatycznego ponawiania wysyłek o nieustalonym rezultacie bez istniejących bramek.
- Test: lite-resume-send-race 9 PASS, lite-resume-agent 10 PASS, pełne 15 suite JS PASS, promotion gate PASS, Debug 0 errors/1 known MSB3277.
- Offline fixture prawdziwego WebView2 zawiera ukryte unrelated aria-busy=true, aby zweryfikować wysłanie pomimo spinnera.

## Warunek LIVE_VERIFIED
1. Standardowy Autosync, PBOX1 czysty pull, STAGED side-by-side + manifest i testy.
2. Brak brutalnego restartu aktywnej rozmowy i brak równoległej instancji na tym samym profilu.
3. Jedno potwierdzone dev_wake_ack i widoczny DIL dymek dla przypiętego czatu. Testy nie zastępują dowodu z LIVE.
4. W razie busy_* ustalić logowany reason i rozwiązać prawdziwą przyczynę zamiast skracać interwał.
5. Nadzór cykliczny tylko podczas działania aplikacji i dopóki projekt ma rzeczywiście otwarte zadania; nie gwarantować działania przy zamkniętym oknie, uśpionym komputerze czy niedostępnej stronie.
## Produkcyjna promocja i test (07:45 CEST)
- Autosync zwykłą drogą: refs/heads/autosync/SATANAMD snapshot a2783b48efd1, bez ręcznego commit/push.
- PBOX1 czysty git pull --ff-only do a2783b4, v1.71.0 Release side-by-side w %LOCALAPPDATA%\Programs\OpenBrowserChatGPTLite_v1.71.0_20261019_0740. Manifest SHA256 i release gate PASS, 15/15 JS PASS, 8 wybranych .NET headless PASS, prawdziwy izolowany WebView2 GUI PASS (13 checks).
- PBOX1 uruchomiono --lite-dev-wake-rearm EXIT0 07:44:13 CEST, zachowując prywatny przypięty chat i interval25, firstCheck+2m.
- Dotychczasowe LIVE v1.70 PID8704 zamknięto WYŁĄCZNIE CloseMainWindow, wróciło true, notatki zabezpieczone przez FormClosing. Wcześniej uruchomiono kontrolowany watcher z wydania 1.71 w GUI session1. Old process exited, new LIVE v1.71 PID21560 start 07:44:51; Responsing True, navigation_completed IsSuccess true, high_guard_mode_changed high 07:45:05 CEST, dev_wake_loaded enabled=true, chatBound=true, interval25.
- Istniejące pojedyncze okno z profilem użytkownika, brak równoległych WebView2 produkcyjnych instancji; zero automatycznych przypominajek ChatGPT.
- **Ważne:** pierwsza produkcyjna wysyłka i ACK wciąż niezweryfikowane w momencie wpisu 07:45; oczekiwane sprawdzenie po 07:46:51. NIE oznaczać zadania DONE bez dev_wake_ack i dymka po stronie ChatGPT.
## Pierwszy LIVE tick 07:46:53 — WSTRZYMANY (nie FAIL wysyłki)
- PBOX1 proces produkcyjny 1.71 PID21560 działa i obserwuje jeden przypięty chat, High potwierdzony. Dev wake first tick 07:46:53 CEST: dev_wake_skipped reason=answer_missing, czyli aktualna odpowiedź asystenta jeszcze nie była zakończona lub widoczna jako ostatnia w obserwowanym DOM.
- Nie było żadnej nowej próby wysyłki ani ponowienia; jest to poprawne zachowanie przy trwającym bieżącym czacie. Program nadal co minutę sprawdza, czy odpowiedź jest ukończona i stabilna >=45 sekund; jeśli tak, przejdzie do sender. Stan Enabled=true i NextDue przeterminowany pozostają zapisane.
- Nie twierdzić, że pierwsze budzenie zostało potwierdzone; na 07:48 CEST wciąż nie widziano dev_wake_ack. Trzeba odczytać wynik po zakończeniu bieżącej odpowiedzi i sklasyfikować dokładny busy_reason, jeżeli nadal odrzuca wysyłkę.
- Dalsze przebiegi mają działać co ok. 25 minut, podczas gdy okno Open Browser działa, czat pozostaje właściwy, High i aktywność są prawidłowe, a backlog ma otwarte zadania. Żadnych natywnych przypominajek ChatGPT.