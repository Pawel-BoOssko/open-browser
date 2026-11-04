# Open Browser Lite: radykalne uproszczenie architektury
Data: 2026-10-31. Host: SATANAMD. Polecenie właściciela: ciągła autonomiczna praca jest ważniejsza od unikania duplikatów.

## Cel i dwie reguły
Celem jest wielodniowa autonomiczna realizacja zadań przez jednego aktywnego leada, z naturalnymi wybudzeniami, rotacją i odzyskiwaniem po awarii.
Reguła 1: termin wybudzenia powoduje próbę dostarczenia polecenia. Niepewny wcześniejszy wynik i ryzyko duplikatu nie wyłączają kolejnej próby.
Reguła 2: cała procedura operacyjna należy do plików projektu, nie do wiadomości wysyłanej do ChatGPT.

## Kontrakt komunikatów, wersja 2
Zwykłe: [OB-WAKE-NNNNNN] oraz nowa linia `Kontynuuj pracę w swoim projekcie zgodnie z zasadami.`
Handoff: [OB-WAKE-NNNNNN] oraz nowa linia `Zapisz aktualny handoff projektu zgodnie z zasadami.`
Pierwsze wybudzenie nowego czatu: identyczne ze zwykłym. Bez osobnego `SUCCESSOR READY` w wiadomości.
Bez: czasu silnika, receipt GUID, instrukcji CLI, list narzędzi, checkpointów, zasad Beads, logów, statusów, opisu rotacji, pełnych promptów i notatek.
Silnik sam zapisuje czas próby, receipt i dowody w plikach. Czat sam zapisuje rzeczywisty czas rozpoczęcia pracy, jeśli wymaga tego projekt, bez kopiowania czasu podanego przez silnik.
Potwierdzenie DOM dotyczy jednoznacznego numeru wybudzenia w realnej wiadomości użytkownika w docelowym czacie. Wewnętrzny GUID nie jest częścią tekstu.

## Audyt architektury i decyzje
1. LiteChatWakeRegistry.NumberedMessage, LiteChatHandoff, LiteWakeRotationUi: usunąć wielostronicowe teksty i osobne instrukcje następców. ETAP 1 SOURCE wykonany, wymaga testów LIVE.
2. LiteResumeSender, LiteSuccessorSeedDirect, LiteSuccessorFirstWakeProof: potwierdzenie wysyłki bez tekstowego GUID; osobna diagnostyka silnika. ETAP 1 SOURCE wykonany, weryfikacja WebView2 234 kontroli PASS; rzeczywista zalogowana rotacja jeszcze niezweryfikowana.
3. LiteChatWakeUi: zinwentaryzować wszystkie gałęzie DeferWakeWithoutSend, HoldUnconfirmedRecovery, waiting_*, LastAnswerFingerprint oraz warunki preflight. Usuwać blokady wynikające wyłącznie ze starej niepewnej próby; pozostawić rozpoznawanie właściwego czatu i niezajmowanie cudzego edytora. Czas odzyskiwania ograniczony, każda kolejna szczelina powinna ostatecznie powodować próbę.
4. LiteWakeStallUi: uprościć wielofazowe czekanie i dwukrotny STOP do jednej ograniczonej procedury przywrócenia, bez wyłączania harmonogramu.
5. LiteWakeRotationUi / LiteRotationRetryPolicy: zmniejszyć liczbę faz rotacji; nie utrzymywać send_uncertain bezterminowo; pierwszy normalny wake w nowym /c/, potwierdzenie niezależnym dowodem.
6. LiteWakeJournal i event logs: logować minimalne zdarzenia mechaniczne niezależnie od payloadu; logowanie nie może blokować wysyłki, a jednocześnie nie wolno fałszować ACK.
7. Checkpoint i Beads: obowiązki znajdują się w zasadach projektu. Handoff najlepiej tworzy silnik z istniejących plików, a prośba do czatu ma być jednym poleceniem.
8. DOM hard prune: zachować ograniczenie <=10 tur, ale usunąć nadmiar powtarzanych skanów i powtórnych reinstalacji po dowiedzionym 8h soak.

## Kolejność wykonania i bramki
A. Uprościć trzy komunikaty i pokryć testami statycznymi, headless oraz realnym WebView2. Następnie wdrożyć LIVE z jednym PID, SHA, aktywnym rollbackiem; nie restartować czatu, jeśli zmiana zniszczy trwającą pracę.
B. Potwierdzić 5 kolejnych naturalnych wybudzeń nowym krótkim tekstem, `visible_ack` i niezależnym `chat_started`. Monitorować powrót do pracy, a nie tylko ACK.
C. Przejrzeć kod wszystkich gałęzi odroczenia, przygotować tabelę mechanizm, przyczyna, decyzja KEEP/DELETE/MERGE, zmniejszać stan maszynowy i testować warianty awaryjne.
D. Zrealizować prawdziwy cykl handoff (numer z końcówką 9) oraz początek nowego czatu (0), potem awaria i odzyskanie bez udziału użytkownika.
E. Przeprowadzić 24h, a następnie 7 dni ciągłej pracy: dowody wybudzeń, faktycznych zmian w projekcie, rotacji, rollbacku i DOM <=10.

## Co nie jest celem
Dopisanie kolejnych zabezpieczeń, zwiększanie liczby testów dla samych testów, optymalizacja logów oderwana od dostarczania pracy, zatrzymywanie wybudzeń dla uniknięcia duplikatu.

## Stan na start
LIVE 1.73.75 PID2472, jedyny lead; w innej rozmowie wake207 przygotowano QUEUED 1.73.78 bez ostatnich zmian minimalnego payloadu. Pełna wersja źródłowa z minimalnym payloadem i testami z tego zadania wymaga NOWEGO numeru wydania (np. 1.73.79), niezależnego SHA i dokładnego testu; nie wdrażać niezweryfikowanego wcześniejszego pakietu 1.73.78. Wspólne repo może mieć równoległego lead207: zachować jednego autora wdrożenia.

## Audyt blokad, wynik wstepny 16:53 CET
- `LiteChatWakeUi.cs:1040 FlushNotes` teraz moze zablokowac cale wybudzenie, choc przy minimalnym komunikacie OBL notatki nie sa wysylane: DELETE z krytycznej sciezki dla OBL; zapis notatek osobno.
- `LiteChatWakeUi.cs:1039,1087,1102 HoldWakeOnBackendRead429`: czytalny HTTP 429 blokuje wysylke; MERGE w jednego ograniczonego retry albo inny sposob uzyskania poprawnego DOM, bez bezterminowego hold.
- `LiteChatWakeUi.cs:1059-1069 ShouldDeferWorkerProbe`: kumuluje opóznienia i moze stracic termin; MERGE z szybka naprawa worker i foreground fallback.
- `LiteChatWakeUi.cs:1124-1136 HoldUnconfirmedRecovery`: oddzielne 5-min odroczenie, mimo decyzji wlasciciela o pierwszenstwie dostarczenia; DELETE: zwykly dostepny composer ma pozwalac na nowy wake.
- `LiteChatWakeUi.cs:1141-1194 submitOwnDraft`: zaleznosc od GUID w tekscie jest niekompatybilna z minimalnym `[OB-WAKE-NNNNNN]`; FIX w etapie B, bez nadpisywania zwyklego szkicu.
- `LiteChatWakeUi.cs:1198-1221 ShouldRestoreChatView`: obecnie 5-min cooldown i 2-min defer; SIMPLIFY: jedno wymuszone otwarcie wl. /c/ i bounded retry.
- `LiteChatWakeUi.cs:1222-1237 preflight not ready`: co najmniej dwa osobne `DeferWakeWithoutSend`; MERGE w jedna obsluge stanu niedostepnosci.
- `LiteChatWakeUi.cs:1245 LastAnswerFingerprint`: dawny fence przed duplikatem. DELETE tylko po ustaleniu zaleznosci z preflight i restart/rotacja. Sam nowy numer wake dostatecznie rozroznia proby.
- `LiteWakeStallUi.cs:50-... StallPhase`, `wait_two_minutes`, `force`, `StallFirstDueUtc`: wiele faz STOP i awaryjnego komunikatu; MERGE w krotka, ograniczona procedurę przywracania i przejscia do nowego czatu.
- `LiteWakeRotationUi.cs` stare `send_uncertain`: nie przedluzac w nieskonczonosc. Dwufazowe: aktywny stary lead, nowy potwierdzony; stan techniczny proby przejscia trzymac osobno od harmonogramu.
Klasyfikacja KEEP: tozsamosc wlasciwego `/c/`, ochrona obcego edytora, unikanie dwoch jednoczesnych leadow, faktyczne rozroznienie click/visible_ack, niezniszczenie checkpointu. Nie sa to kontrolki antyduplikacyjne, tylko warunki poprawnosci.

## Postep wake212, 2026-10-31 17:39 CET
- SOURCE 1.73.83: LiteWakeStallUi usunieto dodatkowy komunikat awaryjny, wait_two_minutes i drugi STOP. Dwa normalne terminy, jeden STOP (jesli dostepny), bezposrednia proba normalnego wybudzenia. Testy: 4 headless pass, WebView2 pass (raportowany licznik 234 jest statyczny; dodano szesc asercji).
- Pakiet 1.73.83 SHA A67A3D790340964777A2AD7EBC1F164454509AE1E8F4736CE083962D0705AB7D jest QUEUED. LIVE 1.73.82 zachowany na czas generacji; nie przerywac aktywnej odpowiedzi.
- Nastepny mechanizm: LiteChatWakeUi.ShouldRestoreChatView pieciominutowy cooldown i dodatkowe dwuminutowe odroczenie po reloadzie. MERGE w jedna probe recovery; zachowac sciezke wlasciwego /c/.
- Kolejny: LiteWakeRotationUi.TryRearmUncertainRotation i 3-min timeout. Przeprojektowac na szybko weryfikowany URL /c/ i osobny journal zdarzen; bez podwojnego kliku i utraty leada.
- Nadal do wykonania: numeric own-draft recovery w LiteResumeSender.js. Poprzednia proba modyfikacji zostala zablokowana przed wykonaniem przez zabezpieczenia narzedzia, nie zmieniono pliku. Nie obchodzic odmowy.


## Naturalne wybudzenia 211 do 215, 2026-10-31 18:21 CET
- 5/5: pojedynczy click, silnikowy visible_ack i niezalezny chat_started, dopasowane do identycznych sekwencji oraz receipt. Bez przerw i brakujacych ACK. Wake215 etap aktywny, chat_finished jeszcze oczekiwany.
- 1.73.84 uruchomiona przez watchdog przy potwierdzonej bezczynnosci; kolejne wake215 15/15 PASS.
- 1.73.85 queued, nie przerywac generowania. Naprawiono raportowanie rzeczywistego deploy vs skipped, nie zuzywac 10min cooldown dla skipped.
- Etap 5 wake: potwierdzony. Etap 24h/7d oraz rotacja 219/220: otwarte.


## Wake216: przygotowanie rotacji 219/220
- LIVE1.73.85 zdrowe 15/15 na naturalnym216, jedyny lead. Dowod historyczny: prawdziwe przeslanie pierwszego numerowanego wake210 i chat_started/finished210; brak starszego eventu DOM visible_ack210.
- Luka czasowa: silnik zapisuje aktualny automatyczny HANDOFF_CURRENT przy wykonywaniu rotacji 219->220, natomiast wake219 wysyla tylko polecenie do starego czatu. Stary dokument209 pozostaje do czasu zapisu nowszego. Docelowo zapisz best-effort automatyczny snapshot przy wake219 oraz odswiez go przy220, bez blokowania wysylki gdy zapis sie nie powiedzie.
- Test izolowany pierwszego przekazania przygotowany, kompilacja 1.73.86 odrzucona przez zabezpieczenia narzedzia; przywrocono plik testowy z backupu, nie wykonano obejscia, brak wdrozenia.
- Atomic temp->rename i SHA readback w katalogu docs: PASS, usuniecie testowych plikow PASS. 24h obecny wycinek ma 90 rezerwacji, 81 jawnych ACK i nie dowodzi 100% ciaglosci.


## Wake217, 2026-10-31 18:52 CET
- LIVE1.73.85 15/15 PASS, natural wake217 visible_ack, Beads started. 7 sequential wakes211..217 have independent ACK and chat_started.
- SOURCE ONLY LiteChatWakeUi.cs: after actual click of numbered .9, best-effort snapshot Save and event rotation_handoff_prestaged_on_send; 220 path refreshes as before. One additive 1003-character fragment with backup in diagnostics, no other source deletion.
- Build of updated SOURCE not attempted after previous tool denial; old binary selftests cannot prove new branch. LIVE unchanged. Until new build and QA permitted, handoff219 depends on model instruction and snapshot220 remains the engine fallback.
- Next: actual handoff219 verified header Last wake219/Next220/current old lead URL; then real 220 promotion, avoid two leaders.


## Wybudzenie 218: awaryjne przejecie do 220, 19:10 CET
- Alarm chatgpt_app_problem w starym foreground, dla 218 click ale DOM ACK uncertain. Chat_started218 przekazal prawdziwy dowod: engine receipt_resolved confirmed_by_chat_started.
- 219 nie wyslane przez bledny generator special .9 -> handoff; powtarzane invalid_seed. Recovery pliku projects.json zmienila jedynie WakeSequence218 na219 (backup, readback), 219 jawnie zapisano jako skipped_unsent. Handoff snapshot 219->220 poprawny.
- 220 ma rzeczywisty first ordinary click, stable server route, DOM visible_ack, niezalezne chat_started220 i START220; jeden nowy lead. NEXT 19:21:36.
- SOURCE-only fix NumberedMessage(forceOrdinary:true) dla first successor, test boundary .9; foreground follow exact oldUrl. Bez kompilacji/wdrozenia, blokada publish z poprzedniego cyklu. LIVE1.73.85 pokazuje stara strone z bledem, chociaz nowy lead pracuje w worker.

