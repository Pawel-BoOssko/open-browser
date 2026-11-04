# OBL: audyt niepotwierdzonego wybudzenia 140
Data analizy: 2026-10-31, SATANAMD. Wybudzenie: 140, 2026-10-30.

## Fakty z rejestrów
1. Silnik: attempt_reserved 20:40:20.227 UTC, receipt dc5f94c8e67e450aa48bde89feb6329f.
2. Silnik: submission_attempted click_invoked 20:40:20.392 UTC; notes_submitted 20:40:20.395 UTC. W wake-lifecycle.jsonl BRAK stage receipt_confirmed i chat_started/chat_finished z tym receipt.
3. lite-events-2026-10-30.jsonl: rotation_first_wake_reserved wakeSequence=140 20:40:20.229 UTC i rotation_seed_click click_invoked 20:40:20.392. To pierwsze wybudzenie nowego leada przy rotacji, nie zwykły termin na starym czacie.
4. 20:40:22.923 UTC: HTTP 200 z endpoint conversation, worker. Kod HTTP sam w sobie nie potwierdza odpowiedzi modelu.
5. 20:41:52.500 UTC: rotation_foreground_follow_requested z old /c/6ae4d648-73fc-83e9-ab2a-f9c8840ed007 do rzeczywistego server /c/6ae500b2-38a8-83e9-a86b-f2652d3ce21b.
6. 20:41:53.979 UTC: navigation_completed IsSuccess=true, a potem GET/POST rozmowy ze statusem HTTP 200 na foreground.
7. 20:42:02.261 UTC: assistant_markdown_saved PARTIAL (345 znaków) w foreground; 20:42:32.275 UTC: FINAL (1794 znaki); 20:42:33.393 UTC: generating=false, assistantCount=1.
8. Odbiór 24 h obliczony 2026-10-31 19:24:10 UTC: 90 prób, 89 effectiveConfirmed, seq140 jedyna pozycja unproven; definicja liczy tylko journal DOM-ACK albo niezależny wpis chat_started/finished.

## Interpretacja i ograniczenia
- Powyższe dowodzi, że po kliknięciu seq140 powstał i otworzył się rzeczywisty nowy czat oraz wygenerowano w nim odpowiedź asystenta. Nie dowodzi niezależnego odczytu exact receipt w bąbelku użytkownika; nie dopisywać brakującego ACK.
- Niewyjaśniona przyczyna braku receipt_confirmed/chat_started w dzienniku. Rozważyć utracony zapis po promocji, różnicę implementacji historycznego LIVE albo brak osobnego zapisu przez czat. Nie przypisywać jednej przyczyny bez dowodu.
- Obecny SOURCE LiteWakeRotationUi.cs: po VerifySeedAsync wywołuje MarkAndPromote, a dopiero później LiteWakeJournal.Write(receipt_confirmed). Występuje okno rozdzielenia zapisu promocji i dowodu; nie wnioskować, że ono wyjaśnia konkretną historyczną sytuację 140.
- Dla testu 229/230 utrwalić niezależnie: kliknięcie, rzeczywisty /c/, dowód pierwszego USER bubble, receipt_confirmed, chat_started/finished, jednego leada. Nie utożsamiać końcowego FINAL z automatycznym ACK.

Dzienniki źródłowe: %LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\diagnostics\wake-lifecycle.jsonl i lite-events-2026-10-30.jsonl. Brak zmian w historycznych wpisach.
