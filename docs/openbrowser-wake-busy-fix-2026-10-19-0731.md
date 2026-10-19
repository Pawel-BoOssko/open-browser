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