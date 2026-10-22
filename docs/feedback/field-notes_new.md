# Field Notes

Purpose:
Collects raw operational observations from the Operator, the model working inside OpenBrowser, and developer agents when they notice problems, missing capabilities, unclear instructions, failures, or useful improvement ideas.

Depends on:
- docs/templates/document-header-template_new.md
- docs/templates/field-note-template_new.md
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/documentation-update-checklist_new.md

Impacts:
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/documentation-update-checklist_new.md
- docs/agent/commit-rules_new.md
- docs/agent/current-state_new.md
- developer-agent triage and planning

Update rule:
Update this file whenever the Operator, the model working inside OpenBrowser, or a developer agent records a real observation from use, testing, development, or documentation work.

Latest:
- 2026-10-22 07:41 CEST: PBOX1 LIVE1.72.21 wykonał 22. foreground wake o07:27:37 receiptConfirmed=true. Kolejne fałszywe blokery w SOURCE<=1.72.44: global querySelector tool-running w LiteResumeProbe mógł użyć sidebar tool innego chatu, global error role alert i LiteResponseSignal document-wide assistant error mógł blokować bieżącą rozmowę. SOURCE/TEST v1.72.45: main-scoped tool running/assistant inline placeholders, z globalnych alertów usuwa aside/nav/sidebar, prawdziwe toasty pozostają. Node unit 20/20, dotnet Release publish, 8 published EXE tests, 146 real offline WebView2 checks oraz SHA manifest PASS. Pierwszy regression run ujawnił test harness niepełny i złą asercję null, poprawiono. Manifest final utworzony pod unikatową nazwą bez nadpisania starego. PBOX1 LIVE wciąż1.72.21, tool denial publish z przeszłości bez obejścia. Brak real logged-in background multi-chat, 8h post-release CPU/RAM/DOM soak, DIL QA. Checkpoint STATUS.
- 2026-10-22 07:17 CEST: LIVE PBOX1 v1.72.21 21. naturalny foreground wake 07:07:36 receiptConfirmed=true. Defekt SOURCE<=1.72.43: LiteResumeProbe na każdym preflight przeszukiwał cały document osobno legacy+DIL, sortował merged DOM nodes compareDocumentPosition (zbędny koszt dla długiego chatu), sidebar fake-assistant mógł stać się ostatnią wiadomością nawet przy bieżącym user turn. SOURCE/TEST v1.72.44: tylko main scoped `querySelectorAll('[data-message-author-role],[data-user-message-bubble],[data-chatgpt-selection-message-id]')`, bez global sort, no main => fail closed. Dwa nowe Node testy sidebar/noMain (Mixed11), 4 zaktualizowane harnessy, 20 Node suite PASS, dotnet Release publish, 8 published EXE flags, real isolated offline WebView2 141 checks PASS, SHA manifest PASS. Brak real live konto multi-chat E2E, PBOX1 nadal1.72.21 przez wcześniejszą odmowę publikacji bez obejścia, nie wnioskować o oszczędności RAM bez deployment i pomiaru. Główny checkpoint STATUS.
- 2026-10-22 06:55 CEST: PBOX1 LIVE v1.72.21 20. naturalny foreground wake o06:47:36 receiptConfirmed=true. Risk OBL-0.3/0.6: SOURCE<=1.72.42 preflight asystenta to dwa await JS rozdzielone1500ms, ale bez stabilności URI/route epoch na przestrzeni obu odczytów, przez co SPA A→B→A może pozornie zaakceptować stabilną odpowiedź innej sesji. SOURCE/TEST v1.72.43 teraz CaptureStablePreflightAsync z porównaniem exact ChatGPT Source+epoch w 4 punktach, osobny foreground epoch i shared-worker epoch. `navigation_changed` zwraca false i nie commit ani nie wysyła. 9 nowych deterministycznych asercji, isolated real offline WebView2 136/136, Node20 suites, .NET publish, 8 EXE flags, manifest SHA PASS. Nie dowodzi live zalogowanego background 5–10 chat E2E. PBOX1 LIVE dalej 1.72.21, wcześniejszego tool denial publish bez obejścia, RAM starej wersji 06:53 984.9MiB WebView2 total; 8h soak/DOM real-site QA wciąż brak. STATUS checkpoint.
- 2026-10-22 06:35 CEST: LIVE PBOX1 v1.72.21 otrzymał #19 naturalny foreground wake 06:27:36 receiptConfirmed=true. Nowe wykrycie OBL-0.3: SOURCE v1.72.41 miało 25s total history hydration limit w hidden WebView2, ale po composer-only shell `history_not_ready` wracało false bez MarkWorkerProbeFailed, więc ten sam koszt25s+do6.5s metadata JS ponawiał się bez narastającej przerwy. SOURCE/TEST v1.72.42 wywołuje MarkWorkerProbeFailed po takim terminalnym niepowodzeniu, metadata limit1.5s/timeout bez dalszego JS. Osiem dodatkowych deterministycznych backpressure/recovery checks w izolowanym real WebView2, 127/127 PASS; Node20/20, .NET publish, 8 published EXE flags, SHA manifest PASS. Nie zmienia wymogu historii/przygotowania exact chat; brak jeszcze live logged-in 5–10 background E2E. PBOX1 LIVE wciąż 1.72.21 bez obchodzenia wcześniejszej odmowy publish, nie przypisywać RAM do nowego SOURCE. Główny checkpoint STATUS.
- 2026-10-22 06:17 CEST: LIVE PBOX1 v1.72.21 18th natural foreground wake o 06:07:35 receiptConfirmed=true. Defekt SOURCE<=1.72.40: IsWorkerHydratedForChatAsync checkuje Source tylko przed awaited WebView2.ExecuteScriptAsync; NavigateWorkerAsync po found=true ustawia verified URL bez re-check Source. SPA może zmienić A→B lub ABA A→B→A zanim async wróci. SOURCE/TEST v1.72.41: post-probe Source+verified identity, monotonic routeEpoch from NavigationStarting,SourceChanged,ProcessFailed; reuse i fresh history akceptują wynik tylko bez żadnej zmiany epoch/target. 12 nowych WebView2 offline race checks, 119/119 PASS, 20 Node suites, dotnet publish, 8 published EXE selftests, test SHA manifest PASS. NIE jest real logged-in hidden-worker background E2E. PBOX1 LIVE nadal 1.72.21, odmowy wcześniejszej publikacji nie obchodzono, bez nowego LIVE soak i DIL QA. Checkpoint STATUS.
- 2026-10-22 05:57 CEST: Live PBOX1 v1.72.21 17th natural foreground wake 05:47:35 receiptConfirmed=true. Wykryto race OBL-0.3: C# po eligible preflight A wywoływał JS sendDirect(payload,receipt,true), a JS ufał swojej aktualnej ścieżce w momencie injekcji, nie wiedząc, że host wybrał A; SPA A→B przed send mogła wysłać wiadomość przeznaczoną do A do B. SOURCE/TEST v1.72.40 dodaje exact expectedPath JS param czwarty i porównanie zanim tekst/cokolwiek dotknie DOM; mismatch blocked_navigation i żadnego pending/click. C# no-click marker zwalnia hash i retry po2min. Unit JavaScript sender38 PASS, 20 Node suites, .NET publish, osiem EXE selftests, real offline WebView2 107 kontroli, local SHA manifest PASS. NIE zweryfikowano na real zalogowanych 5–10 czatach; PBOX1 LIVE nadal1.72.21, wcześniejszej odmowy publikacji nie obchodzono. RAM LIVE 05:55 998.1MiB private, do obserwacji po dozwolonym deploy. Główny checkpoint STATUS.
- 2026-10-22 05:33 CEST: 16. naturalny live foreground wake PBOX1 v1.72.21 05:27:35 receiptConfirmed=true. Discovered OBL-0.3 multi-chat starvation: source PollAllChatWakesAsync always chose due foreground before any older background due, so active foreground generating/busy could occupy every tick. SOURCE/TEST v1.72.39 adds SelectDueChat oldest-due sorting with foreground priority only if due within 60s of oldest, excluding disabled/future schedules; no new timers or heavy worker. Five fake chat entries, 10 offline checks in actual isolated WebView2: 102/102 PASS, Node 20/20, .NET publish, 8 EXE selftests, SHA manifest PASS. This does NOT validate real hidden ChatGPT WebView2 hydration nor live 5-10 schedules. PBOX1 unchanged LIVE v1.72.21 after previous blocked publish; no bypass. Resource 05:31 private WebView2 943.7MiB/7 processes, not new version. Further live E2E and 8h DOM soak pending. STATUS checkpoint.
- 2026-10-22 05:14 CEST: Potwierdzony 15. naturalny foreground wake PBOX1 LIVE v1.72.21 05:07:35 receiptConfirmed=true. W SOURCE ≤1.72.37 LiteDomOptimizer.js wykonywał własny setInterval(check,30s), host _domMonitor co30s wykonywał odrębny status poll JS getStatus, czyli dwa niezależne ticki i stale/niejednoczesne dane. SOURCE/TEST v1.72.38: JS utracił redundantny timer, start check zachowany; host _domMonitor wykonuje check+getStatus razem. Node focused optimizer8 PASS, 20 Node suites, dotnet publish, 8 published EXE flags, isolated offline real WebView2 92/92 controls, SHA256 manifest PASS. Hard prune i DIL soft default OFF, bez dodatkowych Scheduled Tasks. Oczekiwanie: mniej zbędnych odświeżeń UI, nie potwierdzony spadek RAM bez wdrożenia. PBOX1 nadal LIVE v1.72.21 PID8216, po wcześniejszej odmowie tool publish bez obejścia; 5–10 real background chat, >8h soak/DIL QA i UI pending. Główny checkpoint STATUS.
- 2026-10-22 04:56 CEST: LIVE 1.72.21 PBOX1 14. automatyczne foreground wake 04:47:35 potwierdzone, 04:47:51 response generation. Ryzyko w SOURCE ≤1.72.36: po host-managed click C# sprawdzał hasAck jeden raz natychmiast, choć React może opóźnić user bubble; pozornie brak ACK mimo prawdziwej wysyłki. SOURCE/TEST v1.72.37: readonly bounded 4200ms receipt polls (<=8, each JS<=1200ms), tylko po click_invoked/form_submitted, ten sam main-scoped sender.hasAck, nigdy drugi click ani automatyczny resend, timeout kończy próbę. Offline real WebView2 1.3s delayed fixture test PASS, bubble count exactly one; WebView2 87/87, Node 20/20, .NET publish, 8 EXE flags, SHA manifest PASS. PBOX1 LIVE nadal stary 1.72.21, nie obejścia wcześniejszej odmowy publikacji; zasobowe A/B i real 5–10 chat background E2E niezaliczone. Checkpoint STATUS.
- 2026-10-22 04:34 CEST: LIVE PBOX1 v1.72.21 dostarczył 13. foreground wake 04:27:33 receiptConfirmed=true. Zbadano OBL-1.2 duplicate/idle pollers: LiteDomHardPrune.js w default OFF nadal instalował setInterval(prune,120s)+setTimeout(prune,45s), choć callback po wywołaniu stwierdzał disabled; C# hard-prune report timer już robił early return. SOURCE v1.72.36 usuwa niepotrzebne timery gdy disabled i natychmiast kasuje oba przy opt-out; ręczne ON pozostaje idempotentne, inne bezpieczeństwo/busy guards i hard-prune default OFF. Node focused 10 PASS w tym stale callback after OFF, full 20 Node suite, dotnet publish, 8 EXE flags, isolated WebView2 83 controls, SHA manifest PASS. Brak pomiaru różnicy CPU/RAM na LIVE, PBOX1 nadal 1.72.21; niewdrożone ze względu na wcześniejszy tool denial. 8h soak, DIL QA i prawdziwy E2E 5–10 czatów tła nadal potrzebne. Checkpoint STATUS.
- 2026-10-22 04:13 CEST: Dwunasty naturalny LIVE foreground wake v1.72.21 PBOX1 o 04:07:34, receiptConfirmed=true. Znaleziona sprzeczność: JS sender hasAck main-scoped, lecz C# PollAllChatWakesAsync ponownie liczył receipt po całym document; naprawiono w SOURCE v1.72.35 przez wywołanie sender.hasAck. Drugie ryzyko: trwały hash i DueUtc+pełne Minutes mimo potwierdzonego no-click (`draft_button_missing` lub `blocked_*`) zatrzymywały czat; scheduler teraz clear fingerprint i retry2min tylko w tych stanach, NIE gdy wynik click bez ACK, timeout albo sender_missing. 8 nowych kontroli w real offline WebView2, 78/78 PASS, 20 Node suites, .NET publish, 8 EXE selftests, lokalny SHA PASS. Nie wdrożono do PBOX1 po wcześniejszym narzędziowym denial; LIVE nadal 1.72.21. 5–10 real background chat E2E, RAM/DOM soak i UI nadal oczekują. Główny checkpoint STATUS.
- 2026-10-22 03:53 CEST: LIVE PBOX1 v1.72.21 wysłało naturalny 11. foreground wake 03:47:33 receiptConfirmed=true. Defekt: LiteResumeSender.js hasAck poprzednio szukało po całym DOM i dowolnym substring receiptId, więc marker w sidebar/preview albo inny tekst mógł dać false-positive ACK. SOURCE/TEST v1.72.34 hasAck wymaga prawidłowego full [OB-receipt] i USER bubble tylko wewnątrz bieżącego main, brak fallback gdy main nieobecny. Dwa Node harnessy mają scoped main, sender focused36 PASS; 20 Node suite, .NET publish, 8 CLI EXE selftests, isolated WebView2 70 PASS, lokalny SHA manifest PASS. Produkcyjny PBOX1 nadal 1.72.21 (wcześniejszej odmowy narzędzia nie obchodzono); nie zaliczać real background 5-10 ani 8h soak. Szczegóły checkpoint STATUS.
- 2026-10-22 03:38 CEST: PBOX1 LIVE v1.72.21 10th natural wake 03:27:33 visible ACK true. Realny defekt SOURCE LiteResumeSender.js directPending bez delete po clicked/blocked, utrzymywał payload i referencje DOM przez całe życie SPA; attempted Set i phases Map bez limitu. SOURCE/TEST v1.72.33 usuwa wpisy terminalne, ma 128 metadata receipt history / max8 pending, 15min lazy expiry; brak tekstu/receipt w diagnose. Symulowane 192 sends i 12 pending test PASS; 20 Node suites, dotnet publish, 8 exe tests, isolated WebView2 68 checks, SHA manifest PASS. Jest to poprawka retencji, NIE udowodniona cała przyczyna wzrostu RSS. PBOX1 brak deployment po wcześniejszej odmowie narzędzia; LIVE v1.72.21 PID8216. 2.9h spójnych pomiarów PID: 561.9→901.5MiB; potrzebne >=8h i DOM count oraz real background online. Checkpoint STATUS.
- 2026-10-22 03:13 CEST: LIVE v1.72.21 PBOX1 auto-wake #9 03:07:32 receiptConfirmed=true, model generating 03:07:48. SOURCE/TEST v1.72.32 SATANAMD: dotychczasowy test dwóch czatów QA sprawdzał głównie dom/URL i tylko pierwszy chat miał sender ACK. Dodano actual host-managed direct send dla fikcyjnego chatu B: brak receiptu A, samo wstrzyknięcie to nie ACK, click_invoked i real user bubble B receipt, replay already_visible, zmiana SPA przed submit daje blocked_navigation i brak ACK, po nawigacji A B receipt nie przecieka. Izolowany WebView2 66/66 PASS, 20 Node lite suite, .NET publish, osiem testów EXE, SHA manifest PASS. To offline test bez realnego loginu, nie oznacza ukończenia OBL-0.3 live background. PBOX1 wdrożenie po wcześniejszej odmowie tool publish 1.72.22 nadal niedostępne. Checkpoint docs/OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md.
- 2026-10-22 około 02:54 CEST: Ósmy naturalny wake na PBOX1 LIVE 1.72.21 o 02:47:32 receiptConfirmed=true, 02:47:48 generating. Nowy problem: LiteRendererBackpressure był używany dla High probe, ale nie dla jednego background worker. SOURCE/TEST v1.72.31 dodaje per-worker cooldown po timeout/COM failure: 30–300s, bez kolejnych script probes w czasie cooldown, odroczenie due chatu bez wysyłki; foreground niezależny; historia zweryfikowana przywraca status zdrowy. 20/20 JS, dotnet publish, 6 EXE selftests, offline WebView2 56/56, local SHA manifest PASS. LIVE nadal 1.72.21, zabroniona próba publikacji 1.72.22 PBOX1 nieobchodzona. Rzeczywisty background E2E i resource soak nadal oczekują. Główny checkpoint docs/OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md.
- 2026-10-22 02:34: LIVE 1.72.21 PID8216 dostarczył 7. naturalny foreground wake 02:27:31, receiptConfirmed=true. Ryzyko: poprzedni background worker uznawał SPA chat B za gotowy, choć pod nowym URL DOM mógł nadal zawierać historię chat A. SOURCE v1.72.30 wprowadził per-worker verifiedChatUrl, reset SourceChanged/NavigationStarting/ProcessFailed i wyszukiwanie historii tylko w main. Offline real WebView2 49 tests PASS wraz z fake sidebar history, zmianą A->B->A i null verifier; 20 JS suites, .NET publish, sześć EXE tests i SHA manifest PASS. NIE wdrożono: PBOX1 nadal LIVE 1.72.21, zakaz obchodzenia wcześniejszej odmowy narzędzia. 56 próbek private WebView2 ~1.83h: min561.9, max882.3, avg784.7MiB; nie uprawnia to stwierdzenia wycieku. Dalsze kroki to prawdziwy background multi-chat E2E, dozwolony gate produkcyjny, real-page DIL QA i 8h resource soak.
- 2026-10-22 02:14: Szóste naturalne wake 02:07:31 [OB-b4d…] wysłane przez PBOX1 LIVE v1.72.21, receiptConfirmed=true, generowanie 02:07:47. W SOURCE v1.72.28 ukryty worker dla innego czatu miał 35 prób gotowości historii, każda mogła zużyć 6.5s+700ms, choć logował domWaitSeconds=25. SOURCE v1.72.29 wprowadził 25s łączne deadline po NavigationCompleted oraz brak dalszych skryptów po jednym timeout; loguje actual elapsed/attempts. 20 JS suite, .NET publish, 6 exe tests, isolated offline WebView2 44 checks PASS, lokalny SHA256 manifest PASS. PBOX1 nadal nie otrzymał nowego wydania po wcześniejszej odmowie publikacji v1.72.22. Wersja 1.72.29 jest wyłącznie SOURCE/TEST SATANAMD, brak 8h soak i real background E2E. Szczegóły checkpoint STATUS.
- 2026-10-22 01:55: PBOX1 LIVE v1.72.21 zachował piąte skuteczne naturalne wake 01:47:31 (visibleUserBubble i receiptConfirmed true). SOURCE SATANAMD v1.72.28 zmniejsza ryzyko trwałego zablokowania archiwizacji DPAPI po hung WebView2 script i pomyłki zapisów po zmianie czatu. Nowy captureEpoch i URI fence: zmiana URL pozwala nowemu dokumentowi odczytywać, a stary callback nie może nadpisać stanu nowej próby. Test offline dwóch osobnych historii A→B→A i 38 WebView2 checks PASS, 20 Node suites + .NET publish + six EXE selftests + lokalny manifest SHA pass. Wersja nie jest PBOX1 STAGED/LIVE; wcześniejsza odmowa publikacji PBOX1 1.72.22 nie została obchodzona. Następne: live E2E archive timeout, background multi-chat i 8h memory/DOM soak. Patrz docs/OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md.
- 2026-10-22 01:32: Czwarty naturalny wake przy v1.72.21 LIVE 01:27:30 ma receiptConfirmed=true. SOURCE v1.72.27: dla jednego background worker wydzielono guard prawidłowy chat URL + composer + pełna historia. Zamiast tylko testować predykat JS, offline isolated WebView2 sprawdza prawdziwy C# guard z histora valid UUID, inny URL i shell-only. Build, 6 exe selftests, WebView 26 checks, 20 Node suites PASS. Manifest skrypt na SATANAMD używał niedostępnego Get-FileHash; naprawiony stream SHA256 + test manipulacji hash. Lokalny kandydat v1.72.27 SHA 97247f7d6caa1ec6c815f865a2061bb8482396178f6de30849559ac6ca42cc28. Nie wdrożono PBOX1 ze względu na wcześniejszą odmowę narzędzia. Szczegóły w docs/OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md.
- 2026-10-22 01:18: OBL-0.3 background worker może bezcelowo przeładowywać już gotową rozmowę podczas kolejnej próby wake, co wiąże się z ciężkim rendererem i dawnym błędem shell-only. SOURCE 1.72.26 sprawdza dokładnie URL+composer+co najmniej jedną turę i reużywa zhydratowany WebView2 worker bez przeładowania, z istniejącym preflightem stabilności i odbioru. Isolated WebView2 21 checks PASS, całe 20 suite JS + .NET PASS. Nie jest LIVE, real background E2E PENDING, żadnych ręcznych push/commit ani obejść wcześniejszej odmowy narzędzia.
- 2026-10-22 01:15: Trzecie naturalne wybudzenie foreground projektu Open Browser Lite o 01:07:30 ma nowy unikalny [OB-de8…] i receiptConfirmed=true; renderer wzrósł w 30min LIVE 1.72.21, WebView2 private 561.9->882.3MiB. SOURCE 1.72.25 mierzy również totalDomElements co 2min poza generowaniem, loguje w dom_status; 7 tests nowoczesnego DIL pilota, 20 Node suites, .NET Release oraz izolowany WebView2 20 controls PASS. To telemetry-only, nie zmniejszenie RAM. Wcześniejsza odmowa publikacji PBOX1 1.72.22 nierozwiązana, SOURCE 1.72.25 nie LIVE. Patrz checkpoint docs/OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md.
- 2026-10-22 około 01:04: PBOX1 LIVE 1.72.21 PID8216 nadal Generowanie, 9 próbek private WebView2 z tej sesji: min 561.9, max 863.5, mean 674.7 MiB w 7 procesach. Trzy pętle monitorowania DOM/odpowiedzi w SOURCE miały ryzyko wiszącego ExecuteScriptAsync po timeout; poprawiono singleflight bounded task w SOURCE 1.72.24. Archiwum asystenta znało tylko legacy DOM; po modern DIL mogło stale nie zapisywać odpowiedzi. Nowe źródło DIL + brak częściowych snapshots przy Stop + 30s poll; 9 focused tests, 20 suites JS i .NET/WebView2 PASS. Wdrożenie NIE nastąpiło; odmowa publikacji 1.72.22 na PBOX1 nadal nierozwiązana. Szczegóły w checkpoint docs/OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md.
- 2026-10-22 00:48: DWA naturalne, odebrane wake czatu Open Browser Lite: 00:21:07 (recovered-own-draft, user message OB-b302...) i 00:41:28 (multi_wake_send phase=click_invoked, visibleUserBubble=true, receiptConfirmed=true, user message OB-9b7d...). Po tym Windows Pending Update automatycznie promował PBOX1 LIVE 1.72.21 o 00:41:44 z zachowaniem rollbacku. SOURCE 1.72.23: domyślnie OFF pilot DIL soft CSS (minTurns60, keepRecent40, tylko blokowe historyczne elementy), Low memory dla jednego ukrytego workera tylko podczas idle, Normal w czasie nawigacji. Testy SATANAMD 20 JS files i .NET/WebView2 exit0. Publikacja 1.72.22 na PBOX1 narzędziem RDC została odmówiona, nie próbowano omijać; więc 1.72.23 też nie jest LIVE. Zobacz docs/OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md oraz docs/OPENBROWSER_LITE_RESOURCE_BASELINE_2026-10-22.md.
- 2026-10-21 22:58: LIVE PBOX1 1.72.16 PID16600 po kontrolowanym wdrożeniu nadal nie wykazał skutecznego budzenia. Ustalono dodatkową przyczynę: identyfikator nowej rozmowy ChatGPT może zawierać kodowany dwukropek `%3A`, odrzucany przez dotychczasowy `LiteDevWakeState.IsChatUrl`. SOURCE v1.72.17 rozszerza walidację i testy, wybiera aktywny chat przy starcie i wykorzystuje widoczny CoreWebView2 do nadzoru tego czatu. Release build oraz samodzielne testy PASS na SATANAMD; PBOX1 LIVE E2E i aktywowanie nowego URL wymagają synchronizacji oraz wdrożenia 1.72.17. Windows Keepalive co 5 min zarejestrowany i wykonany exit0. Status szczegółowy `docs/OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md`.
- 2026-10-20 06:27: PBOX1 LIVE v1.72.15 PID24672 real book preflight observed no user/assistant DOM nodes though composer ready and target URL matched; blank app shell falsely considered navigation ready by previous versions. SOURCE v1.72.16 adds conversation-element readiness wait and metadata-only timeout diagnostics. Pending actual PBOX1 E2E; do not lower safety gates. See docs/openbrowser-checkpoint-2026-10-20-0619-live-v17214-preflight.md.
- 2026-10-20 06:19: PBOX1 v1.72.14 successfully promoted LIVE without hard kill by new scoped graceful script; main process PID18424 responsive and legacy separate 1.64 PID18064 preserved. First manual book wake under new version refused to send, reason assistant_not_complete, 2m defer; whether this is true missing assistant answer or DOM interpretation defect remains unknown. SOURCE v1.72.15 adds metadata-only preflight probes to diagnose without logging user content. See docs/openbrowser-checkpoint-2026-10-20-0619-live-v17214-preflight.md.
- 2026-10-20 05:53: PBOX1 had unintended concurrent old v1.64.0 window PID18064 from Explorer's old pinned taskbar .lnk. Root-cause shortcut corrected along with Start/Desktop targets to v1.72.14 --lite-open-safely. New v1.72.14 STAGED passed release identity and headless tests but is NOT LIVE because safe non-destructive upgrade of two active windows was not confirmed; legacy deploy helper would force-stop all. Background v1.72.12 second wake remained attempting at least a minute while UI heartbeats continued. Source v1.72.14 bounds script calls at 6.5 sec and fences ambiguous sends, pending live E2E. Details docs/openbrowser-checkpoint-2026-10-20-0535-wake-preflight.md.
- 2026-10-20 05:22: Owner's incoming Open Browser continuation contains TWO wake messages with distinct [OB-...] receipts, including the same note about self-configurable chat waking. Production log on PBOX1 20 Oct contains 17 multi_wake_send with only 4 visible unique ACK, plus 2 RenderProcessUnresponsive. Registry shows two enabled DIFFERENT targets (Open Browser and adventure book), so duplication in one displayed conversation is not yet causally proven; page readiness on navigation and disabled/no-ACK submit are plausible. In progress SOURCE v1.72.13 adds stable per-target assistant preflight, fingerprint once per answer, draft/generating protection and host-managed no-timer sending. Checkpoint docs/openbrowser-checkpoint-2026-10-20-0535-wake-preflight.md. Existing 1.64 process unexpectedly started concurrently at 01:32; cause remains to be diagnosed.
- 2026-10-19 ~12:45: Owner requires complete audit of hourly ChatGPT auto-run/other model, stage-by-stage checkpoints and deep file handoff. Both Open Browser native ChatGPT automations verified OFF; source v1.72.4 has no manifest/gate, PBOX1 LIVE still 1.72.0 PID5852. Most serious unmet goal: zero proven actual `dev_wake_ack` despite earlier attempts (busy/answer_missing/uncertain). Retry policy contradicts 02:24 owner decision, and project roadmap/latest current state was stale. Detailed evidence and next plan: `docs/OPENBROWSER_DEEP_HANDOFF_2026-10-19_1245_ChatGPT.md`, incremental log `docs/OPENBROWSER_AUDIT_RUNLOG_2026-10-19_1220_ChatGPT.md`. New tool safety denial on read-only PBOX1 log aggregation + independent registry append denial recorded in runlog. No code/live changes, no new reminders.
- 2026-10-19 08:45: Owner wants old OpenBrowser multi-message sending examined and app-local 25-minute wake operational. PRE-LITE MainForm used immediate editor injection, 500ms delayed click and broad SVG fallback with no ACK. LIVE PBOX1 v1.71 attempted 58 wakes: 56 confirmed busy/not sent, 2 uncertain, zero confirmed; at 08:35 another no_visible_ack. SOURCE v1.72 adds form-scoped exact PL/EN submit-button lookup, metadata diagnostics and no-ACK send phase, without clicking voice button. 15 JS suites PASS, actual offline WebView2 form-without-data-testid PASS. LIVE delivery still needs verification, no ChatGPT native tasks. Audit docs/openbrowser-old-sender-audit-2026-10-19-0845.md.
- 2026-10-19 02:46: Owner: stalled wake is worse than an occasional extra continuation message. Real PBOX1 1.68 had DPAPI Enabled=false, uncertain_send_busy and never resumed. Promoted v1.70 PID8704 with DPAPI Enabled=true, interval25, bound one chat. Direct GUI read shows Autopraca WŁ. 25m and High; while assistant is composing, status answer_missing. Added next-interval uncertain retry, previous ACK recovery, rearm command, tests PASS; first natural live wake not yet verified. No ChatGPT native Tasks. See docs/openbrowser-wake-uncertain-retry-2026-10-19.md.
- 2026-10-19 01:36: Operator clarifies: own Open Browser wake only, about every 25 minutes, without assigning tasks. The chat itself must use durable plan, choose unfinished steps, continue until application finished. Explicit prohibition on all ChatGPT native Tasks and reminders remains. Own project list on left is NOT genuine ChatGPT Projects; migration into true ChatGPT Projects is nonurgent but mandatory in full backlog. SOURCE v1.68 has DPAPI-bound single-chat periodic wake; pending staged and production E2E. Do not claim overnight completion without actual verification.
- 2026-10-18 23:07: 1.67 staged on PBOX1 with 16/16 selftests + 12/12 JS + real offline WebView2 PASS; one-time helper PID8444 waits for natural v1.64 close, no automatic replacement while operator working. Durable quarantine per-chat for delivery errors and manual review before resuming, not auto-resubmit. See docs/openbrowser-p0-checkpoint-2026-10-18-2305.md.
- 2026-10-18 23:05: Operator's repeated 22:38 delivery_timeout displayed for 20s, LIVE PBOX1 v1.64.0 remains responsive but logged 4 RenderProcessUnresponsive incidents today. Restarts/cleared UI cannot justify auto retry. SOURCE v1.67 DPAPI durable transport quarantine per chat + explicit manual reconciliation button before resuming, and protected even at ledger receipt registration/sending. Isolated WebView2 DIL error test PASS, ledger selftest PASS, next staged verification PBOX1. Separate OpenFill book test avoided duplicating existing chapters 8/9. Checkpoint docs/openbrowser-p0-checkpoint-2026-10-18-2305.md.
- 2026-10-18 21:54: Operator screenshot confirms a fresh 20-second delivery_timeout at 21:26:50–21:27:10 CEST in 1.64 live; no automatic retry. Real OpenFill book test failed closed due visible 'Nie udało się wczytać tej odpowiedzi', so no 8th chapter was submitted. SOURCE 1.66 includes High probe exponential backoff, DIL user acknowledgment, targeted response failure detection and supervisor fail-closed on known error. New offline real WebView2 end-to-end harness PASS only in interactive Windows session; production real-book E2E remains blocked/pending. Checkpoint docs/openbrowser-p0-checkpoint-2026-10-18-2154.md.
- 2026-10-18 20:55: Operator needs autonomous High+Luna resume. WebView2 LIVE v1.64 logs effort UNKNOWN, but real ChatGPT GPT-6 High control is accessible via `button[aria-label="Wybierz model ChatGPT"][data-selected-reasoning-effort="high"]`; actual chat has DIL DOM wrapper `[data-chatgpt-selection-message-id]` and user bubble `[data-user-message-bubble]`, making legacy role selector 0. SOURCE v1.65 adds High detection, keyboard slider request, DIL-safe role extraction and preflight answer-hash validation. Node suites PASS, actual Lite UI E2E remains unresolved until controlled deployment; no ChatGPT automations.
- 2026-10-18 20:22: Claude Code audit (docs/claude-audit-2026-10-18-1955.md) is current: source autosync SHA e49d063 and pinned icon blob 50318466 (one exception only); PBOX1 owner-launched LIVE v1.64.0 PID18412 with EXE SHA256 F82A3F7D...EF4DA7 verified locally. Earlier field note SOURCE ONLY / blocked autosync is obsolete, resolved now; historic scanner denial still recorded. 10 JS suites and 16 selftests passed per Claude audit. Still open visual QA 16px icon/taskbar status, stale scheduled task OB-10, real DOM counter 0/0, Luna/High/E2E. Audit follow-up docs/openbrowser-post-claude-audit-2026-10-18-2022.md; no COMMIT or ChatGPT reminders.
- 2026-10-18 19:17: Operator screenshot confirms delivery_timeout banner and unreadable taskbar icon/absence of activity indication, right-side note saved locally but not sent to model. Incident log confirms delivery_timeout at 17:44 CEST and cleared 18:57, HTTP/renderer metadata insufficient to prove server delivery. Source v1.64 fixes stale status and false positives from quoted errors, embeds high contrast 8-frame ICO and titlebar Gotowe/Generowanie/Błąd, offline tests PASS. DOM display 0/0 in 136 samples, selector audit 4 tests PASS, hard prune not promoted. WARNING: binary .ico flagged by Autosync secret scan, and equivalent PBOX publish 1.63 denied by safety; do not bypass. Current source-only and no ChatGPT automations.
- 2026-10-18 18:28: Operator forbids all ChatGPT reminders until further notice; all 32 disabled. Development manual chat only. PBOX1 v1.61 pending helper canceled, active v1.60 retained. Recovery inventory: docs/openbrowser-audit-recovery-2026-10-18-1828.md. Source repairs: delayed auto-send race and hard prune default mismatch corrected, 9 JS suites and 13 .NET selftests PASS, staged 1.62 still pending.
- 2026-10-18 15:51: Autosync SATANAMD pushed cef3a31221d6 (task result 0), PBOX1 fast-forward to cef3a31, Git clean, Debug build PBOX1 0 errors, 5 JS suites PASS. LIVE PBOX1 PID 15860 nadal telemetry, bez restartu. Nie publikowano nowego EXE.
- 2026-10-18 15:49: Open Browser Lite Debug 0 errors; 8 headless .NET selftests exit=0, 5 JS suites PASS. Polityka kart sprawdzona na 10 000 syntetycznych czatów; core-v06 mapping test PASS, ale NIE aktywowano w Lite. Otwarty problem: niespójna flaga WinForms hard prune po odmowie edycji, nie wdrażać jako LIVE.
- 2026-05-21: Test sources show unresolved PS stdout mojibake while PY UTF-8 works correctly.
- This file is a working intake queue, not polished technical documentation.
- Entries are written close to the moment of observation.
- A developer agent triages entries later into bugs, tasks, documentation updates, architecture questions, done items, or rejected items.
- Newest entries stay near the top.

## Entries

## 2026-10-18 | operator | problem | in progress

Observation: ChatGPT Lite on PBOX1 showed WebView2 RenderProcessUnresponsive and blocked chat switching. In parallel ChatGPT Desktop showed a missing response after 6m46s. PBOX1 had ~85 MiB free physical RAM and ~6.4 GiB pagefile occupied; a Get-WinEvent query timed out and LC MultiHost's PowerShell crashed. Unknown whether the missing server response was transmitted.

Expected: persistent WebView2 failure logs and independent CPU/RAM metrics without recording account secrets or conversation content. Debug instrumentation builds; not yet deployed to PBOX1.

Evidence: docs/openbrowser-lite-incident-2026-10-18.md.

## 2026-05-21 09:25 | developer-agent | problem | new

Context:
Documentation migration reviewed linkedin-response.md and LinkedIn tool notes.

Observation:
LinkedIn notes indicate both primary and secondary client secrets were active, and local config may need cleanup to ensure the currently active secret is used. The notes also point to a clean OAuth retry with exact 127.0.0.1 redirect URI.

Impact:
LinkedIn posting may fail with invalid_client if local config uses a stale or wrong secret, hidden whitespace, or mismatched redirect host.

Expected:
Treat this as an operational LinkedIn tool cleanup item, not architecture. Before further LinkedIn posting tests, verify active secret choice, update config/local/linkedin/, avoid printing secrets, and retry OAuth cleanly.

Links or files:
- linkedin-response.md
- tools/linkedin/README.md
- tools/linkedin/LINKEDIN_WORKING_GUIDE.md

## 2026-05-21 09:20 | developer-agent | idea | new

Context:
Documentation migration reviewed ideas.md and tool-proposals.md.

Observation:
The source files contain future ideas and proposed integrations, including Gmail, Google Calendar, Google Drive, Microsoft 365, Slack, Discord, Notion, Obsidian vault, Apify, browser form automation, Supabase, and GitHub Issues/Releases.

Impact:
These items are useful backlog material, but they are not accepted current architecture and must not be added to current-state or modules-index as active tools unless the owner/architect accepts them.

Expected:
Keep these proposals as source/backlog material. Promote individual items only after explicit architect decision, then update architecture-decisions, modules-index, current-state, and relevant tool docs.

Links or files:
- ideas.md
- tool-proposals.md

## 2026-05-21 09:00 | developer-agent | problem | new

Context:
Development documentation migration reviewed test-plan and test-results source files.

Observation:
Multiple test result files report that PS stdout can corrupt Polish characters or return mojibake, while PY output and PY file UTF-8 tests pass.

Impact:
Documentation must not claim that all PowerShell output encoding problems are fully solved. The invariant remains valid for generated files and explicit UTF-8 writes, but PS stdout display may still require separate handling.

Expected:
Clarify current state: explicit UTF-8 is mandatory, PY UTF-8 works, PS generated file writes should use explicit UTF-8, and PS stdout mojibake remains an open issue unless verified fixed by a later test.

Links or files:
- test-results-v3.md
- test-results-v4.md
- tools-test-results.md

## 2026-05-21 09:00 | developer-agent | problem | triaged

Context:
Test result sources include a historical failed sandbox download pipeline in test-results-v4.md, while later live OpenBrowser checks verified automatic ChatGPT sandbox file downloads.

Observation:
Older v4 test results reported that sandbox file was not downloaded and HST_STATUS did not list it. Later current-state documentation and live checks verified openbrowser_real_sandbox_test.txt download handling.

Impact:
Old test-results-v4 failure should be treated as historical evidence, not current truth, unless the current pipeline fails again.

Expected:
Keep current-state based on latest verified live behavior, but retain this note as a warning that download behavior has had a prior failure mode.

Links or files:
- test-results-v4.md
- docs/agent/current-state_new.md
- docs/modules/Downloads/index_new.md

## Entry format

```text
## YYYY-MM-DD HH:mm | <source> | <type> | <status>

Context:
<what the Operator or model was trying to do>

Observation:
<what happened or what was noticed>

Impact:
<why it matters, what work was blocked, slowed down, or made unclear>

Expected:
<what should happen instead, if known>

Links or files:
- <optional paths, logs, downloads, screenshots, commands>
```

## Allowed values

Source:
- operator
- openbrowser-model
- developer-agent

Type:
- problem
- failure
- missing-capability
- confusing-instruction
- manual-workaround
- idea
- question

Status:
- new
- triaged
- converted-to-task
- needs-architect-decision
- done
- rejected

<!-- COMPLETE_DOC_UPDATE_FIELD_NOTES_20260521 -->
## 2026-05-21 | Model help, feedback, and text transport observations

Observed issues to keep visible:
- model operators need explicit command frame examples, not only command names;
- HST_HELP must explain OpenBridge, HST commands, tool help commands, and RAW/Base64 framing;
- tool help commands should display tools/<tool>/README.md and state the displayed path;
- executor-specific anti-patterns belong in executor help files;
- old README files and legacy documents can mislead models if not routed through help;
- text transport and encoding must be treated as an architecture issue in both directions: model to executor and executor to model;
- models need one factual place for operational observations after tasks.
<!-- /COMPLETE_DOC_UPDATE_FIELD_NOTES_20260521 -->

## 2026-10-18 15:47 | developer-agent | Open Browser Lite | partial
Observation: testy 8 bezinterfejsowych modułów .NET zakończone exit=0; 26 scenariuszy JavaScript PASS. Nowy planista rezydencji kart i klasyfikator dowodów dostarczenia są w SOURCE, lecz nie sterują jeszcze rzeczywistym WebView2. Domyślne usuwanie DOM w JavaScript ustawiono na opt-in, ale flaga WinForms nadal wymaga uzgodnienia, ponieważ edycja pola została odrzucona przez zabezpieczenia. Nie wdrażać nowego EXE do LIVE bez sprawdzenia zgodności UI i testu React. Wystąpiły równoległe edycje kolejki notatek, chwilowo psujące kompilację; ostatni build Debug PASS. Pełny rejestr blokad: docs/Rejestr nieudanych komend.md.
