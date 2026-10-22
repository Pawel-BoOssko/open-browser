# Current State

Purpose:
Defines the current repository-level truth that developer agents must use before making changes or recommendations.

Depends on:
- docs/templates/document-header-template_new.md
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/development-rules_new.md
- docs/agent/coding-invariants_new.md
- docs/runtime/modules-index_new.md
- docs/agent/architecture-decisions_new.md
- docs/development/rules.md
- docs/development/decisions.md
- docs/development/journal.md
- docs/development/README.md
- docs/runtime/environment.md
- config/start-prompt.txt
- docs/design/session-rhythm.md
- docs/design/help-system.md

Impacts:
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/development-rules_new.md
- docs/agent/documentation-update-checklist_new.md
- docs/agent/commit-rules_new.md
- docs/runtime/start-prompt_new.md
- docs/runtime/hst-help_new.md
- docs/runtime/hst-status_new.md
- docs/runtime/modules-index_new.md
- docs/executors/*/index_new.md
- docs/modules/*/index_new.md

Update rule:
Update this file when implemented behavior, active architecture, available commands, active modules, active executors, runtime paths, build/test commands, RAW payload behavior, or verified system capabilities change.

Latest:
- 2026-10-22 07:41 CEST: PBOX1 LIVE v1.72.21 PID8216 Responding=True; 22. naturalny foreground wake 07:27:37 [OB-e06f0a88…] receiptConfirmed=true, 07:27:53 generation. SOURCE/TEST SatanAMD v1.72.45: preflight LiteResumeProbe main-scoped toolBusy oraz globalne alerty z odfiltrowanym aside/nav/sidebar; LiteResponseSignal odczytuje assistant/turns tylko z bieżącego main, sidebar false alert ignorowany, realne globalne toasty i chat running nadal blokują. 20 Node tests PASS; focused sidebar busy4, response signals13; .NET Release publish exit0 1 znane MSB3277, 8 published EXE flags EXIT0, offline real WebView2 146/146 PASS, lokalny SHA RELEASE_IDENTITY_OK b8bf5462d034a010ce9e06442dd8488ee94ac90783af35a57f8dfc1b2026383c (nowy test manifest po poprzedniej kolizji). STAGED lokalny SatanAMD TEMP. PBOX1 LIVE nadal1.72.21, wcześniejszego tool denial publish v1.72.22 nie obchodzono. PBOX1 07:39 WebView2 private aggregate1063.8MiB 7proc max renderer839.1 (stara wersja). Wciąż do zrobienia rzeczywisty logged-in 5–10 background E2E, prod deployment, >=8h soak/DIL CSS QA/UI. Checkpoint STATUS/PLAN; bez ChatGPT Tasks/ręcznych git commit/push.
- 2026-10-22 07:17 CEST: PBOX1 LIVE v1.72.21 PID8216 Responding=True; 21. naturalny foreground wake 07:07:36 [OB-4d78f9ff…] receiptConfirmed=true, 07:07:53 generation. SOURCE/TEST SatanAMD v1.72.44: LiteResumeProbe.observe wybiera tylko wiadomości w bieżącym main/[role=main] jednym zbiorczym selektorem (DOM order bez sortowania + bez dokumentowych 2 querySelectorAll); sidebar preview ani obce menu nie mogą nadpisać ostatniej odpowiedzi. Brak main zwraca role=unknown/0 turns (fail-closed), dotychczasowe stop/draft/generation safeguards bez zmian. 4 JS harnessy zaktualizowane; focused Mixed11/Sidebar3/DilDom4/Agent10 PASS, full 20 Node testów PASS, .NET publish 0 errors (znane MSB3277), 8 CLI EXE selftestów EXIT0, isolated real offline WebView2 141/141 PASS (+5 nowo dodanych przypadków sidebar i usuniętego main), manifest RELEASE_IDENTITY_OK v1.72.44 SHA 8e826290581f8cbec24e3cad735e3f3743aec021445031a7cf4a2d6c063200f1. STAGED lokalny TEMP SatanAMD, PBOX1 LIVE nadal1.72.21 po wcześniejszej tool denial, nie obchodzono. PBOX1 07:15 7 WebView2 private 1019.9MiB/top renderer806.6 (stara wersja). Brak real logged-in background 5–10, dozwolonego prod release, >=8h soak nowego kodu, DIL QA i UI. Checkpoint STATUS/PLAN. Bez ChatGPT Tasks i ręcznych git commit/push.
- 2026-10-22 06:55 CEST: PBOX1 LIVE v1.72.21 PID8216 Responding=True, 20. naturalny foreground wake 06:47:36 [OB-319adac…] receiptConfirmed=true, 06:47:52 generation. SOURCE/TEST SATANAMD v1.72.43: preflight two snapshots DOM oddzielone 1.5s ma stabilną ścieżkę i monotonic epoch, osobny foreground+shared worker (NavigationStarting/SourceChanged/ProcessFailed). Każda zmiana target podczas pierwszej/drugiej JS lub 1.5s przerwy, w tym A→B→A, skutkuje navigation_changed bez fingerprintu/kliku i z retry after2min. 9 nowych asercji w isolated real offline WebView2, 136/136 PASS; 20/20 Node JS suite, .NET Release publish 0 errors/znany MSB3277, 8/8 published EXE CLI selftests PASS, local test manifest RELEASE_IDENTITY_OK v1.72.43 SHA 067f7790c64f43435a4f18ed804bd47fe8944db119645decb6c6a2c41b29912c. SATANAMD STAGED wyłącznie lokalny TEMP. PBOX1 LIVE 1.72.21 bez instalacji lub obejścia wcześniejszej odmowy publish v1.72.22, 06:53 WebView2 private984.9MiB 7proc/top771.6. Real logged-in 5–10 background chat E2E, bezpieczny deployment, >=8h new-release soak, real DIL CSS QA/UI nadal nieukończone. Główny checkpoint STATUS/PLAN, brak ChatGPT Tasks/ręcznych git commit/push.
- 2026-10-22 06:35 CEST: PBOX1 LIVE v1.72.21 PID8216 Responding=True; naturalny foreground wake #19 o 06:27:36 [OB-418cd5…] receiptConfirmed=true, generowanie 06:27:52. SOURCE/TEST SATANAMD v1.72.42: niezhydratowana powłoka shared WebView2 worker (composer bez historii) po 25s teraz MarkWorkerProbeFailed(history_not_ready), co aktywuje istniejący cooldown 30/60/120/240/300s dla background; readonly metadata script limit1500ms zamiast6500ms, timeout bez dalszych skryptów, prawidłowa historia resetuje gate, foreground nie blokowany. Offline real WebView2 127/127 PASS (8 nowych testów escalated failure+recovery), Node JS20/20, .NET Release publish0 errors 1 known MSB3277, 8 published EXE selftests exit0, manifest RELEASE_IDENTITY_OK v1.72.42 SHA 5a5a6b1d22b34660a535a82f9562f83e65caf6e600f79cc0ccb1eb5a815a1f63. STAGED lokalny SATANAMD TEMP tylko, PBOX1 LIVE wciąż 1.72.21 po wcześniejszym tool denial v1.72.22 (nie obchodzony). PBOX1 resource 06:33 seven WebView2 process private total990.5MiB/top769.7. Nadal potrzebne real logged-in multi-chat background5–10, dozwolone wdrożenie, ≥8h new-release soak DOM/RAM/CPU oraz DIL real-page QA i UI. Checkpoint STATUS/PLAN. Bez ChatGPT Tasks i ręcznych commit/push.
- 2026-10-22 06:17 CEST: PBOX1 LIVE v1.72.21 PID8216 Responding=True; 18. naturalny foreground wake 06:07:35 [OB-3e6b51…] receiptConfirmed=true, 06:07:51 generowanie. SOURCE/TEST SATANAMD v1.72.41: OBL-0.3 C# shared worker ponownie sprawdza Source i stale verified identity po awaited history probe; worker NavigationStarting/SourceChanged/ProcessFailed zwiększają routeEpoch, co uniemożliwia ABA A→B→A w trakcie odczytu i zaakceptowanie starego DOM przy tej samej końcowej ścieżce. Nowa nawigacja akceptuje history found=true wyłącznie z tym samym source/epoch po await. +12 offline WebView2 checks, 119/119 PASS, 20/20 Node JS tests, .NET Release publish exit0/1 known MSB3277 warning, 8/8 published EXE selftests exit0, local test manifest SHA RELEASE_IDENTITY_OK v1.72.41 8a647b9a6f2555b93a5794b342497439fb823b6767521e3ed86e55a0f72b5308. STAGED tylko SATANAMD TEMP. PBOX1 nadal LIVE v1.72.21 i żadnego obejścia wcześniejszej tool denial PBOX1 publish v1.72.22. RAM PBOX1 06:15 WebView2 7 proc 968.1MiB private, max renderer753.4MiB. Real 5–10 logged-in background chat E2E, prod deploy, 8h RAM/DOM soak i real DIL CSS QA/UI nadal otwarte. Checkpoint STATUS i PLAN. Bez ChatGPT Tasks/manual git commit/push.
- 2026-10-22 05:57 CEST: PBOX1 LIVE v1.72.21 PID8216 Responding=True; 17. naturalny foreground wake 05:47:35 [OB-31c7e64…] receiptConfirmed=true; 05:47:51 generowanie. SOURCE/TEST SATANAMD v1.72.40: host passuje exact expectedPath jako czwarty argument do JS LiteResumeSender.sendDirect w tym samym JS call co inject. Po SPA A→B przed wysyłką zwraca blocked_navigation bez edycji cudzego kompozytora, pending/attempt untouched, bez kliknięcia. C# IsDefinitelyUnsentWake dla blocked_navigation pozwala clear fingerprint i retry2min; click bez ACK/timeout nadal chronione przed dublem. JS focused sender38 PASS i Node20/20 suites, .NET Release publish0 errors(znany MSB3277), osiem published EXE selftests EXIT0, isolated real offline WebView2 107/107 PASS, local test SHA256 RELEASE_IDENTITY_OK v1.72.40 7abe192219b0d923980dc0ebcd1837e785cf6dbbe2f6c9588e08b5b71af1f275. SATANAMD STAGED wyłącznie lokalny TEMP. PBOX1 LIVE nadal1.72.21, brak próby obejścia wcześniejszej odmowy PBOX1 publish v1.72.22. PBOX1 05:55 7 WebView2 processes aggregate private998.1MiB/top785.5. Real online multi-chat background 5–10 E2E, >=8h new-release soak, DIL CSS QA i UI nadal otwarte. Checkpoint STATUS/PLAN. Bez ChatGPT Tasks/ręcznego commit/push.
- 2026-10-22 05:33 CEST: PBOX1 LIVE v1.72.21 PID8216 Responding=True; naturalny foreground wake #16 05:27:35 [OB-ceba562…] receiptConfirmed=true, 05:27:51 generating. SOURCE/TEST SATANAMD 1.72.39: OBL-0.3 fair selection `SelectDueChat` prioritizes oldest due background over much less overdue foreground; foreground retains 60s due-time tolerance, stable sort and disabled/future exclusion. Five simulated schedule entries: 10 deterministic checks PASS, real isolated offline WebView2 total 102/102 PASS, 20 Node suites PASS, .NET Release publish 0 errors/1 known MSB3277, 8 EXE selftests PASS, SHA256 test manifest RELEASE_IDENTITY_OK 55b5a5f5d1340b0bfec7c877bfbf29b55f18b6cbf90943556d0199ad944e10db. Local STAGED only SATANAMD. PBOX1 LIVE1.72.21 unchanged, prior tool denial for PBOX1 publish not bypassed. PBOX1 WebView2 private 943.7MiB 7 processes at05:31, no new LIVE DOM census. Still missing REAL 5-10 chat background, PBOX1 deployment, ≥8h new-release resource soak and DIL QA/UI. Checkpoint STATUS/PLAN; no ChatGPT Tasks or manual git commit/push.
- 2026-10-22 05:14 CEST: LIVE PBOX1 v1.72.21 PID8216 Responding=True; 15. naturalny foreground wake 05:07:35 [OB-c2eedd…] receiptConfirmed=true, generowanie 05:07:51. SOURCE/TEST SATANAMD v1.72.38: OBL-1.2 zlikwidowano redundantny JS LiteDomOptimizer.setInterval(check,30000). Zamiast oddzielnego pollera DOM wewnątrz renderer + status-poll hosta, jeden WinForms _domMonitor (30s) w PollDomAsync wywołuje check() i loguje aktualny status. Start-up check nadal występuje, totalDomElements census co >=120s i idle only, modern DIL soft OFF, hard prune OFF. Testy: focused optimizer 8 PASS z host contract, 20/20 Node suites PASS, .NET publish 0 errors (1 znane MSB3277), 8/8 EXE selftests PASS, real offline WebView2 92 checks PASS (dodatkowe 5 testów świeżości DOM bez timera), lokalny manifest RELEASE_IDENTITY_OK v1.72.38 SHA 86ab9271b13b72b94d6f8009ca95a146886a8a974567f34d2a2f822dd2f50471. STAGED tylko lokalny TEMP SatanAMD, PBOX1 LIVE nadal 1.72.21, bez obejścia wcześniejszej odmowy publish. PBOX1 05:13 private WebView2 934.9MiB 7 processes, top renderer727.0. Bez porównania A/B, real background E2E, 8h soak i DIL QA. Checkpoint STATUS i PLAN. Bez ChatGPT Tasks i ręcznych commit/push.
- 2026-10-22 04:56 CEST: PBOX1 LIVE 1.72.21 PID8216 Responding=True; 14. naturalny foreground wake o 04:47:35 [OB-d77add…] receiptConfirmed=true, generating 04:47:51. SOURCE/TEST SATANAMD 1.72.37: po jednym `click_invoked/form_submitted` host bounded read-only waits max 4200ms/8 prób co400ms/1200ms JS timeout na USER bubble ACK przez sender.hasAck, nigdy re-click; przy timeout renderer natychmiast stop probes i log bez danych prywatnych. Real offline WebView2 fixture: chat B USER bubble celowo opóźniony1300ms, początkowy ACK false, host detects true, count3 bez dubla, invalid receipt fail; WebView2 87/87 PASS. Node 20/20 suites, dotnet Release publish exit0 (znane MSB3277), 8 published EXE selftests exit0, manifest RELEASE_IDENTITY_OK SHA b8709210c649a6d316157fb0c96b3ecc3ee3d10f121fe6ce6e19d69fa79244c3. SATANAMD lokalny STAGED test; PBOX1 LIVE nadal1.72.21 po wcześniejszej odmowie publish v1.72.22 bez obejścia. RAM LIVE 04:55 7 WebView2 processes 918.2MiB private, top717.6. Real online multi-background chat, PBOX1 release, ≥8h soak i DIL QA/UI nadal otwarte. Checkpoint STATUS + PLAN. Bez ChatGPT Tasks/ręcznych commit/push.
- 2026-10-22 04:34 CEST: PBOX1 LIVE v1.72.21 PID8216 odpowiada, 13. naturalny foreground wake 04:27:33 [OB-0bebc4f9…] receiptConfirmed=true, 04:27:50 generowanie. SOURCE/TEST SATANAMD v1.72.36: LiteDomHardPrune.js nie instaluje interval 120s ani initial timeout45s gdy hard prune default OFF. Jawne ON właściciela zakłada max jeden timer każdego rodzaju, OFF usuwa oba, stary zakolejkowany callback nie wycina DOM. Hard prune nadal OFF w UI i JS; DIL soft CSS default OFF. Node 20/20 suites PASS, focused hard-prune 10 PASS, dotnet Release publish 0 errors (MSB3277 warning), 8 published EXE CLI selftests exit0, isolated REAL offline WebView2 83 checks PASS, local SHA256 RELEASE_IDENTITY_OK b1cd625bce36b821e1cf2ba3aafe2665ed86555768121e41cd68cd769be335cc. Lokalny STAGED tylko SatanAMD, PBOX1 LIVE 1.72.21 bez obejścia wcześniejszej odmowy PBOX1 publish. PBOX1 04:33 WebView2 7 procesów aggregate private924.5MiB top renderer705.5. Real multi-chat background, PBOX1 deployment, 8h soak, real DIL QA i final UI wciąż otwarte. Checkpoint STATUS/PLAN. Bez ChatGPT Tasks i ręcznych git commit/push.
- 2026-10-22 04:13 CEST: LIVE PBOX1 v1.72.21 PID8216 Responding=true, 12. naturalny foreground wake o 04:07:34 marker [OB-9730c…], visibleUserBubble/receiptConfirmed=true, generating 04:07:49. SOURCE/TEST SATANAMD v1.72.35: C# multi-wake scheduler używa sender.hasAck ze scoped MAIN USER bubbles zamiast globalnego DOM, a definitely-unsent result+phase (`draft_button_missing`, `blocked_*`, busy/no-composer) resetuje trwały answer fingerprint i planuje retry po 2min. Kliknięty bez ACK, timeout i sender_missing nadal niepewne bez automatycznego dubla. 20 Node suite PASS, .NET Release publish zero errors 1 znane MSB3277, 8 CLI published EXE selftests exit0, isolated offline WebView2 78/78 checks PASS, lokalny SHA manifest RELEASE_IDENTITY_OK f71a2ad68450ce728100d19beb74c791af494dddb892bff4cb6f18b59e1c47ce. SATANAMD STAGED tylko lokalne. PBOX1 LIVE nadal 1.72.21 bez obejścia wcześniejszej odmowy publikacji. PBOX1 04:11 aggregate private WebView2 902.6MiB 7 procesów. Nadal brak real background 5-10 chat E2E, 8h soak i DIL CSS QA/UI. Główny checkpoint STATUS. Bez ChatGPT Tasks i ręcznych commit/push.
- 2026-10-22 03:53 CEST: PBOX1 LIVE v1.72.21 PID8216 Responding=true; 11. naturalny foreground wake 03:47:33 [OB-df2d8b…] receiptConfirmed=true, generating03:47:49. SOURCE/TEST SATANAMD v1.72.34: potwierdzenie wysyłki w LiteResumeSender.js tylko z aktywnego main/[role=main] i jego USER messages zawierających pełny marker [OB-<32hex>]. Żaden sidebar, assistant quote, aplikacyjny shell bez main ani goły 32hex nie daje ACK. 20 Node suites PASS (sender focused36), .NET publish Release 0 errors znane MSB3277, 8 CLI published EXE selftests PASS, isolated WebView2 70/70 PASS, SHA256 RELEASE_IDENTITY_OK 5929a31a3c99637380db2a768ce2c2fe84370b4606f400d194d13a034b17cef6. STAGED tylko SatanAMD local TEMP. PBOX1 LIVE bez aktualizacji przez wcześniejszą odmowę narzędzia przy v1.72.22; nie była obchodzona. WebView2 03:51 private aggregate881.6MiB 7 procesów; real background i 8h soak/DOM real page QA oczekują. Checkpoint STATUS i PLAN zaktualizowane; bez ChatGPT Tasks i ręcznych commit/push.
- 2026-10-22 03:38 CEST: PBOX1 LIVE v1.72.21 PID8216 responsive, dziesiąte naturalne foreground wake 03:27:33 marker [OB-7d579d…] receiptConfirmed=true, generating 03:27:49. SOURCE/TEST SATANAMD v1.72.33: usunięto retencję pełnego payload+referencji do starego DOM z LiteResumeSender.js directPending po terminalnym click/blocked. 128 ostatnich prób/faz, <=8 niewysłanych pending, lazy expiry15min; do diagnostyki tylko liczniki, bez prywatnej treści. JS focused simulated 192 sequential sends pending=0 po kliknięciu oraz 12 pending bounded=8; 20/20 Node suites, dotnet publish, 8/8 EXE selftests, real isolated WebView2 68/68 checks, local SHA256 RELEASE_IDENTITY_OK v1.72.33 c9ffaf4d76ac650407fcdc18561e236d8ea8f8b8717187d32511e9b78b942754. Testowa STAGED tylko SatanAMD, PBOX1 nadal LIVE1.72.21; wcześniejsza odmowa PBOX1 publish1.72.22 nieobchodzona. Obecne 88 resource samples 00:43–03:37 (2.9h) private WebView2 561.9→901.5MiB, avg807.9; brak 8h soak i real background E2E. Checkpoint docs/OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md. Bez ChatGPT Tasks i ręcznych commit/push.
- 2026-10-22 03:13 CEST: PBOX1 LIVE v1.72.21 PID8216 Responding=True; 9. naturalny foreground wake 03:07:32 [OB-6d8a917…] receiptConfirmed=true, 03:07:48 generating. SOURCE/TEST SATANAMD v1.72.32: izolowany WebView2 fikcyjny chat A→B→A, w B sprawdzono prawdziwy host-managed sendDirect / submitDirect, unique user bubble ACK, brak ACK samego draftu, replay blokujący duplicate; podczas pending B zmiana adresu SPA do A zwraca blocked_navigation bez wysłania, brak B receipt po przeładowaniu A. WebView2 66/66 kontroli PASS, 20 JS suites PASS, .NET publish 0 errors (MSB3277 warning), osiem EXE selftests EXIT0, SHA manifest RELEASE_IDENTITY_OK v1.72.32 hash 8a474ee69e59a50e5eeb73728a235a94fe4bc4a1845d0a50cc786e399b52d78d. Lokalny STAGED tylko SatanAMD. PBOX1 LIVE nadal 1.72.21 bez próby obejścia wcześniejszej odmowy PBOX1 publish. WebView2 03:11 aggregate private 877.3MiB 7 processes. Real online background 5-10 chats, 8h soak, modern DIL CSS, UI pending. Checkpoint docs/OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md. Bez ChatGPT Tasks i ręcznych commit/push.
- 2026-10-22 około 02:54 CEST: PBOX1 LIVE 1.72.21 PID8216 Responding=True. Ósmy naturalny foreground wake 02:47:32 [OB-3e807c6…] receiptConfirmed=true; generowanie 02:47:48. SOURCE/TEST SATANAMD v1.72.31: Jeden background WebView2 worker używa istniejącej LiteRendererBackpressure po reuse/history/navigation timeout i background TimeoutException/COMException (30s->max300s), do czasu cooldown nie sonduje hung renderer, DeferWakeWithoutSend 2min, foreground niezależny od tego limitu. Regresja: 20 Node suites PASS, .NET Release publish 0 errors (1 warning MSB3277), 6 EXE selftests PASS, isolated WebView2 56 checks PASS, lokalny manifest SHA256 RELEASE_IDENTITY_OK 4905522e47f41fea3f53d9e928697e11c094c37726a21d03e7c1c999706ed233. SATANAMD lokalny STAGED test, PBOX1 LIVE nadal 1.72.21 bez wdrożenia po wcześniejszej odmowie narzędzia 1.72.22. PBOX1 resource 02:53 WebView2 private 855MiB, 7 procesów. Brak real background multi-chat, brak 8h soak i DIL QA. Checkpoint docs/OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md. Bez ChatGPT Tasks i ręcznych commit/push.
- 2026-10-22 02:34 CEST: 7. autonomiczne foreground wake na PBOX1 LIVE v1.72.21 PID8216, o 02:27:31 [OB-2d42186b…] z visibleUserBubble=true receiptConfirmed=true; generowanie rozpoczęło się 02:27:48. SOURCE/TEST SATANAMD v1.72.30: jeden background WebView2 worker musi mieć _workerVerifiedChatUrl z rzeczywistej zakończonej nawigacji z historią. Zmiana SourceChanged na inny chat, NavigationStarting albo ProcessFailed unieważnia tę tożsamość; sam URL i obecność pozostałej historii React nie uprawnia reuse. Testy: 20 JS files PASS, .NET Release publish 0 errors (znane MSB3277), 6 published EXE selftests PASS, isolated WebView2 49 controls PASS, lokalny SHA256 manifest RELEASE_IDENTITY_OK 7b4b9437f84b8c1b10717adb1bcd60a13b74983479e548a10e8e0b6254e92609. Tylko SATANAMD STAGED, PBOX1 1.72.30 nie wdrożono; poprzednia odmowa PBOX1 publikacji v1.72.22 nieobchodzona. 56 próbek v1.72.21 w 1.83h: private WebView2 561.9–882.3MiB, mean784.7, ostatnio858.0, brak po-DOM porównania i 8h soak. Dokumentacja STATUS/PLAN. Brak ChatGPT Tasks i ręcznych commit/push.
- 2026-10-22 02:14 CEST: PBOX1 LIVE v1.72.21 PID8216 Responding=True; szóste naturalne wybudzenie 02:07:31 [OB-b4d…] z visibleUserBubble=true i receiptConfirmed=true, 02:07:47 generating=true. SOURCE/TEST SATANAMD v1.72.29: OBL-0.3 ukryty worker po NavigationCompleted ma 25s łączny limit hydracji historii; pojedyncze ExecuteScriptAsync max 6.5s, limit skraca każdy timeout i opóźnienie, po JS timeout bez kolejnych skryptów do tego renderera w próbie. Build .NET publish 0 errors, 1 warning MSB3277; 20 Node JS test files, 6 published EXE selftests, isolated real offline WebView2 44 checks exit0. Lokalny manifest SHA256 RELEASE_IDENTITY_OK hash 41468dbf535276dfe94ea06c261824d89cc3ba0d02f6f5a81d05577d027d5c8f. Produkcja PBOX1 w próbie 02:13 829.1 MiB private 7 WebView2 processów. Wcześniejsza odmowa PBOX1 publish 1.72.22 nie obchodzona; v1.72.29 nie jest STAGED ani LIVE na PBOX1. Nadal real multi-chat background i 8h DOM/RAM soak. Dokumentacja STATUS i PLAN na bieżąco; bez ChatGPT Tasks i ręcznych git commit/push.
- 2026-10-22 01:55 CEST: PBOX1 v1.72.21 PID8216 responsive, piąte automatyczne foreground wake 01:47:31 [OB-8d07…] receiptConfirmed=true; generowanie rozpoczęte o 01:47:47. SATANAMD SOURCE/TEST v1.72.28: realny izolowany WebView2 38/38 PASS, w tym nawigacja dwóch fikcyjnych czatów A→B→A, test strażnika archiwum odrzucającego nieaktualne epoki i adresy. W LiteConversationCaptureUi naprawione wiszące snapshot probes: singleflight per dokument, reset na NavigationStarting/SourceChanged, stary task nie może skasować stanu nowej próby ani zapisać wyniku do innego czatu. 20 Node suites, .NET publish, 6 opublikowanych EXE selftestów PASS; lokalny manifest SHA256 RELEASE_IDENTITY_OK v1.72.28 hash caa5b09b6ae17833e986f3acbf4939db4a0d69d2175fb5bc5d0c35e8c5477d02. STAGED wyłącznie SatanAMD TEMP. PBOX1 brak instalacji, wcześniejsza odmowa narzędzia przy 1.72.22 nadal blokadą. Windows Autosync ostatni 01:50:57, bez ręcznych push/commit/ChatGPT Tasks. PBOX1 RAM WebView2 01:53 ~819.9MiB, siedem procesów. Najnowszy checkpoint docs/OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md.
- 2026-10-22 01:32 CEST: SOURCE/TEST v1.72.27 SATANAMD. Czwarty naturalny foreground wake o 01:27:30 [OB-b661…] potwierdzony w PBOX1 multi_wake_send visibleUserBubble=true receiptConfirmed=true, kolejna odpowiedź generowana 01:27:47. LIVE PBOX1 1.72.21 PID8216 responsive. Background reuse guard wyodrębniony do IsWorkerHydratedForChatAsync i przetestowany na rzeczywistym izolowanym WebView2 z valid UUID, innym URL, pustą historią i odtworzeniem historii. 20 testów Node, dotnet publish, 6 testów opublikowanego EXE, offline WebView2 26 checks PASS. Na SATANAMD PowerShell 5.1 brak Get-FileHash, więc scripts/new-/verify-lite-release-manifest.ps1 korzystają z SHA256 stream; manifest utworzony, RELEASE_IDENTITY_OK, celowo podmieniona suma odrzucona, następnie poprawny manifest przywrócony. Testowy pakiet tylko w %TEMP% SATANAMD, nie PBOX1. PBOX1 wcześniejsza odmowa publish 1.72.22 nierozwiązana, brak workaround. Bez ChatGPT Tasks i ręcznych commit/push. Checkpoint docs/OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md.
- 2026-10-22 01:18 CEST: SOURCE 1.72.26 SATANAMD, 20 JS test files, .NET Release 0 errors + known MSB3277, isolated WebView2 21 checks PASS. Jeden ukryty worker: jeśli due URL już otwarty i ma composer + historię, ponownie wykorzystuje jego DOM zamiast reload; w innym przypadku stary bezpieczny tor nawigacji. Wcześniejsza SOURCE 1.72.25 dodała próbkowanie totalDomElements co 120s tylko poza generowaniem. LIVE PBOX1 1.72.21 PID8216, 3 naturalne foreground wake potwierdzone do 01:07. Brak E2E dla background multi-chat i 8h soak, pamięć WebView2 w 30min 561.9 -> 882.3 MiB. Publikacja 1.72.22 na PBOX1 poprzednio zablokowana przez narzędzie, bez obejścia, 1.72.26 nie LIVE. Checkpoint docs/OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md.
- 2026-10-22 01:15 CEST: PBOX1 LIVE 1.72.21 PID8216, 3. naturalne wybudzenie o 01:07:30 [OB-de8…] z logowym visibleUserBubble=true receiptConfirmed=true i nową odpowiedzią 01:07:46; wcześniejsze 00:21 i 00:41. SOURCE/TEST SATANAMD 1.72.25: ograniczone WebView2 probes, DIL snapshot po zakończeniu odpowiedzi, eksperymentalny DIL CSS pilot default OFF, jeden Low/Normal worker, nowy metadata-only licznik elementów DOM co 120s poza generowaniem w event dom_status. 20/20 Node tests PASS, Release .NET 0 errors (1 znane MSB3277), realny izolowany WebView2 20 checks PASS (pierwszy test miał błąd parsowania znacznika czasu w QA, poprawiony). PBOX1 LIVE RAM w 30min obserwacji 561.9->882.3MiB private WebView2; 15 kompletnych próbek CPU średnia 5.25% maszyny. Wersja 1.72.25 NIE została opublikowana ani zainstalowana po wcześniejszej odmowie narzędzia przy publikacji 1.72.22. Bez ChatGPT Tasks i ręcznych commit/push. Aktualny status docs/OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md.
- 2026-10-22 około 01:04 CEST: PBOX1 LIVE 1.72.21 PID8216 responsive, długie generowanie; Windows Keepalive działa. Na SatanAMD SOURCE/TEST 1.72.24: poprawiono trzy cykliczne odczyty WebView2 (Response/DOM/Hard prune), które po timeout mogły nadal awaitować wiszący task; odtąd bounded wait i pojedynczy in-flight task per odczyt, po nawigacji reset śledzenia; hard prune OFF nie odpytuje renderer. LiteConversationSnapshot wspiera nowe DIL, zapisuje tylko zakończone odpowiedzi, poll 30s zamiast 12s. Isolated WebView2 sprawdza także MemoryUsageTargetLevel Low/Normal; 20/20 JS test files, dotnet Release 0 errors, isolated WebView2 exit0. PBOX1 publikacja 1.72.22 była odmówiona przez zabezpieczenia narzędzia, bez obejścia; 1.72.24 NIE jest STAGED ani LIVE. Dwa wake 00:21 i 00:41 potwierdzone (drugi receiptConfirmed=true). Raport docs/OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md, pomiary docs/OPENBROWSER_LITE_RESOURCE_BASELINE_2026-10-22.md. Bez native ChatGPT Tasks i ręcznych commit/push.
- 2026-10-22 00:48 CEST: PBOX1 LIVE Open Browser Lite **v1.72.21 PID8216** deployed automatically after previous response completed (Windows Pending Update status new_live 00:41:44); predecessor 1.72.20 retained for rollback. Two actual app-local autonomous wake cycles for the development chat: first 00:21:07 [OB-b302…] by own-draft recovery; second 00:41:28 [OB-9b7d…] with log `multi_wake_send` phase=click_invoked, visibleUserBubble=true, receiptConfirmed=true. These are delivered user turns and model continuation, not mere test clicks. Windows Keepalive Task checks every 5min, hardening for two consecutive unresponsive snapshots deployed; no native ChatGPT Tasks or manual Git push/commit. SOURCE **v1.72.23** on SATANAMD, local .NET Release 0 errors + one known MSB3277 warning, 20 JS suites PASS, six app selftests including isolated WebView2 exit0. Changes since live: hidden background worker memory target Low only idle / Normal during wake; DIL CSS soft optimization experimental, disabled by default until real-site QA and resource trends, no hard DOM delete. PBOX1 attempt to publish 1.72.22 was denied by tool safety, no bypass; therefore source changes remain NOT STAGED or LIVE. Current checkpoint docs/OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md, resource report docs/OPENBROWSER_LITE_RESOURCE_BASELINE_2026-10-22.md.
- 2026-10-20 06:27 CEST: PBOX1 LIVE **v1.72.15 PID24672**, promoted with scoped graceful restart at 06:22:57; previous 1.72.14 closed, fallback guarded, dev v1.64 PID18064 preserved. Actual 1.72.15 book preflight metadata at 06:25:06: correct URL and composer but **0 conversation DOM nodes** (assistantNodes/userNodes/turns all zero), no draft/generating/error; so not evidence of incomplete model response. SOURCE v1.72.16 now waits ~25s for composer AND actual chat message nodes, logs only DOM counts on timeout, then defers without sending. SATANAMD Release publish exit0 and JS sender 26 PASS. Latest checkpoint docs/openbrowser-checkpoint-2026-10-20-0619-live-v17214-preflight.md. Await Autosync and PBOX1 deployment/test of 1.72.16. No ChatGPT Tasks, manual commits or pushes.
- 2026-10-20 06:19 CEST: PBOX1 **LIVE v1.72.14 PID18424** after safe graceful-only promotion from v1.72.12 PID20736 at 06:14:33. Fresh release v1.72.14 build 092ce98 confirmed by window_constructed; recovery script in Documents/deploy-openbrowser-graceful-20261020.ps1 checked SHA256 and preserved DPAPI config backup. Separate old development v1.64 PID18064 untouched. Both per-chat DPAPI schedules enabled, project due ~06:36 and book due ~06:20 after manual test defer. Book wake test at 06:16:08 was definitely NOT sent, reason assistant_not_complete; expected guard behaved safely but cause still unknown. New SOURCE v1.72.15 adds content-free DOM preflight metadata logging; SATANAMD publish exit0 and 16 JS suites PASS. Latest checkpoint docs/openbrowser-checkpoint-2026-10-20-0619-live-v17214-preflight.md. No native ChatGPT Tasks, no manual Git commit/push.
- 2026-10-20 05:53 CEST: PBOX1 STAGED v1.72.14, source Autosync 092ce98, Release publish exit0, manifest identity+SHA256 verified D32C1CF5AC55C989AAC44D81F0DE9338A6FB6B94B4198F2142AD23BD8C88A5A4, PBOX1 four headless selftests exit0, SATANAMD 16 JS suites pass. Pinned taskbar, Start, desktop shortcuts all point to v1.72.14 with --lite-open-safely. LIVE remains v1.72.12 PID20736 alongside separately launched legacy v1.64 PID18064. No forced shutdown because old deploy script force-kills all processes. v1.72.14 background E2E remains pending. Full checkpoint docs/openbrowser-checkpoint-2026-10-20-0535-wake-preflight.md.
- 2026-10-20 05:49 CEST: SOURCE 1.72.14 fixes possible infinite pending `CoreWebView2.ExecuteScriptAsync` in chat wake worker with 6.5s timeout for all navigation/preflight/insertion/click/ACK probes, preserving correct pre/post-commit retry semantics. SATANAMD Release publish exit0 and sender JS 26 PASS. Evidence: PBOX1 LIVE v1.72.12 emitted book-timed unique ACK at 05:45:16, but second target persisted `attempting` from 05:46:07 with host heartbeats still alive at 05:47:07; actual await cause not directly known. Latest checkpoint docs/openbrowser-checkpoint-2026-10-20-0535-wake-preflight.md. SOURCE 1.72.14 not yet PBOX1 LIVE, previous PBOX1 STAGED v1.72.13 remains. No manual push or force stop.
- 2026-10-20 05:44 CEST: **PBOX1 STAGED v1.72.13, NOT LIVE**. Autosync naturally synced source b1f2ccc at 05:40:57. PBOX1 Release publish exit0, staged manifest SHA256 0AE12CDCA3C5C27FF8435CCDC60A6F7416010A49CF0A50B671AA4E425BC49E52 verified, 6 PBOX headless selftests all exit0, 16 JS suites pass. Pinned taskbar, desktop and Start shortcuts updated to 1.72.13 with --lite-open-safely. Running PBOX1 remained v1.72.12 PID20736 plus legacy v1.64 PID18064; neither stopped. v1.64 came from obsolete pinned taskbar .lnk (now repaired). Old force-stop deploy helper intentionally NOT used; 1.72.13 E2E new wake remains untested. Full evidence: docs/openbrowser-checkpoint-2026-10-20-0535-wake-preflight.md.
- 2026-10-20 05:35 CEST: PBOX1 LIVE is still v1.72.12 PID20736 (not yet v1.72.13), plus separate unexplained v1.64 PID18064 started at 01:32. 20 Oct event log: 17 background wake attempts, only 4 unique-receipt visible ACK, 2 renderer unresponsive events. DPAPI registry exactly two different chat schedules, each 25 min; incoming owner turn contained two distinct OB receipts. SOURCE v1.72.13 fixes missing eligibility and duplicate fences: separate SHA256 last-answer fingerprint per chat; stable 1.5s two-sample target chat/assistant role/no draft/generation/error guard via injected LiteResumeProbe; explicit complete stops per chat; defer without send if not ready; persist fingerprint before uncertain sending; explicit host-managed Send excludes throttled JS timers; remove unverified form.requestSubmit/CDP synthetic Enter fallback and add refusal on changed editor/draft/busy. Source JS sender 26 PASS and .NET Release build 0 errors at 05:33. New checkpoint docs/openbrowser-checkpoint-2026-10-20-0535-wake-preflight.md. Staging/real PBOX1 tests still pending. No new ChatGPT Tasks, no manual push, no reset.
- 2026-10-19 17:51 CEST: PBOX1 LIVE Open Browser Lite v1.72.12.0 PID20736, safe deployment exit0, fallback 1.72.11. Root cause failed 15:35 was `dev_wake_skipped other_chat` while foreground viewed different conversation; fixed with independent per-chat encrypted DPAPI schedules and background WebView2 worker. Chat-specific notes are appended to outgoing payload, CLI lets each chat set/off/test its own wake. Initial repeated book attempts v1.72.8–11 inconclusive/failed, then v1.72.12 17:47:53 real book chat unique receipt `[OB-<32hex>]` visible in DIL user bubble, `phase=click_invoked,receiptConfirmed=true,includesNotes=true`; separate DPAPI state `visible_ack`. Host C# explicitly flushes Send, avoiding background JS timer throttling. Unit sender 20 PASS, HighGuard 18 PASS, HighSlider 5 PASS, PBOX1 multi selftest PASS. Main project due 18:02:33, book due 18:15:01, 25m each; changed test book prompt to sensible book continuation. Checkpoint docs/openbrowser-checkpoint-2026-10-19-1717-multichat-wake.md and commands docs/openbrowser-multichat-wake-commands-2026-10-19.md. Follow-ups: confirm independent natural cycles and actual assistant response, monitor renderer unresponsive. No ChatGPT Tasks, no manual commit/push.
- 2026-10-19 15:33 CEST: OB3-029 auto-refresh independently LIVE VERIFIED. Existing PBOX1 Windows Resource Monitor wrote 15:33:01 JSONL sample with hostCount=1 and refreshed local resources-dashboard.html at 15:33:08 (4044 bytes). No new scheduled task, no EXE restart. Checkpoint docs/openbrowser-checkpoint-2026-10-19-1512-second-wake.md. Main app GUI integration remains TODO.
- 2026-10-19 15:31 CEST: OB3-029 local diagnostics dashboard now includes bounded event timeline (only utc, eventName, pid, no details) and is auto-regenerated from the EXISTING Windows Resource Monitor Task after each successful sample. No new scheduled task or browser EXE restart. PBOX1 scripts updated from Autosync 60fb43e with backup, manual PBOX sample 15:31:08 generated 4060-byte HTML with dev_wake events, no active JavaScript. PowerShell regressions 18 PASS, AST 0 errors. Detailed checkpoint docs/openbrowser-checkpoint-2026-10-19-1512-second-wake.md. Dashboard integration inside main app still partial.
- 2026-10-19 15:26 CEST: OB3-023 and 029 partially advanced and deployed as independent PBOX1 diagnostics scripts (no browser EXE restart). Resource analyzer provides 5m/1h/24h windows with actual coverage and CPU/RAM peaks, stable vs raw delta across PID changes. 16 regression tests PASS. Synced exporter scripts produce verified 1993-byte static no-JS dashboard: %LOCALAPPDATA%\OpenBrowser\ChatGPT-Lite\diagnostics\resources-dashboard.html. PBOX1 404 sample real report: 5m 3 samples/4min, 1h 33/58min, 24h 404/800min, peak 24h WebView private 1103.7 MiB; 24h window incomplete. All results: docs/openbrowser-checkpoint-2026-10-19-1512-second-wake.md. No manual commit/push or new ChatGPT Tasks.
- 2026-10-19 15:20 CEST: OB3-018 core LIVE_VERIFIED by two correct chat wake events 14:45:23 and 15:10:55, 25m32s apart; current chat resumed actual project work and checkpoints. Simplified direct sender still lacks server ACK (OB3-009/025 OPEN). OB3-021 v1.72.7 production PBOX1 PID20956 LIVE since 15:15:28, fallback 1.72.6 via recovery script, manifest channel live + source SHA 671ed94dac64183307bd73cc99bed4ed5c0aec8e + EXE SHA256 validated, toolbar Techniczne UIA present, dialog body unverified. Desktop and Start shortcuts now v1.72.7; next wake 15:35:55 preserved. OB3-023 memory trend analyzer added stable-PID window and raw-vs-stable RAM deltas, 9 regression checks PASS, syntax 0 errors; production sample verification still pending. Checkpoint docs/openbrowser-checkpoint-2026-10-19-1512-second-wake.md. No manual commit/push, no ChatGPT Tasks, blocked OB3-081 Windows task untouched.
- 2026-10-19 15:07 CEST: OB3-024 partial LIVE resource monitor on PBOX1 extended: 7 scoped WebView2 processes classified browser=1, crashpad-handler=1, gpu-process=1, utility=2, renderer=2. Pure diagnostic orphan candidates computed from parentPid not present in host+WebView2 PID set, no auto-kill. 15:07:17 actual sampled host=1, webview=7, orphan candidates=0, CPU sample 11.5s, PS AST parse 0 errors, no browser restart. Details appended to docs/openbrowser-checkpoint-2026-10-19-1504-resources.md. OB3-024 still partial, error/exit simulation not run.
- 2026-10-19 15:04 CEST: OB3-022/023 source and PBOX1 runtime resource monitor hotpatched, without restarting LIVE v1.72.6 PID21332. Monitor now matches --lite-open-safely as well as --chatgpt-lite, fixing false hostCount=0 while WebView2 count was 7. Real PBOX1 sample 15:00 hostCount=1; existing Windows resource monitor task 15:01 sample also hostCount=1. Added adjacent-sample CPU percentages normalized per core and whole machine: 15:03:55 sample window 51.9s, 4 logical CPUs, host CPU 0.1% of one core / 0.02% overall, WebView2 CPU rates available. Original script backed up, previous inaccurate hostCount history kept as-is. Detailed checkpoint docs/openbrowser-checkpoint-2026-10-19-1504-resources.md. OB3-022/023 PARTIAL, no full trend dashboard. OB3-018 second wake still due 15:10:23. No manual commits/push, no new ChatGPT Tasks.
- 2026-10-19 14:56 CEST: OB3-012/061 handoff UI v1.72.6 deployed LIVE on PBOX1 PID21332 (from autosync 4e218b2), process Responding=True; Desktop/Start links updated. User-click 'Przypnij ten czat' + confirmation + persisted DPAPI rebind (new conversation resets receipt ledger, 25m), UI shows pinned suffix and due time. At 14:53:20 LIVE UIAutomation confirmed new button + 'Czat b74f0fc1f389 | Budzenie 15:10', PBOX1 selftest 33 checks exit0, JS promotion 9/9 PASS, SAT/PBOX publish PASS, fallback 1.72.5 retained and safe-deploy script exit0. First correctly routed real DevWake at 14:45:23 confirmed by received user message; second due 15:10:23 still pending, OB3-018 PARTIAL. Existing Windows scheduled task OB3-081 action update DENIED 0x80070005 and remains old telemetry; not retried; recorded in failed commands log. See docs/openbrowser-checkpoint-2026-10-19-1451-handoff-ui.md. No manual commit/push, no ChatGPT Tasks.
- 2026-10-19 14:21 CEST: production Autopraca on PBOX1 v1.72.5 RETARGETED after user-identified wrong recipient. Current pinned chat is 'Przejęcie projektu Open Browser' (ID suffix b74f0fc1f389), old 'OLD Open Browser — rozwój aplikacji' (suffix 92bfb725f054) unpinned. Exact URLs verified against live WebView2 SQLite History and DPAPI. Re-armed with existing --lite-dev-wake-arm exit0, controlled restart with fallback script exit0, new LIVE PID13856 healthy and WebView2 visited correct conversation at 14:20:23. Next due 14:45:05 CEST, without immediate duplicate on restart. Checkpoint docs/openbrowser-checkpoint-2026-10-19-1421-chat-handoff-rebind.md. No code changes, no manual commit/push or ChatGPT scheduled automations. Delivery of next wake not yet verified.
- 2026-10-19 14:14 CEST: Open Browser Lite v1.72.5 is DEPLOYED and RUNNING on PBOX1, PID6840 since 14:09:56, Responding=True; original 1.72.0 retained as fallback. Owner explicitly prioritized direct send over waiting for ACK/idle/High checks. LiteResumeSender.sendDirect uses old-style editor insertion and clicks Send after 500ms; default DevWake bypasses historical gating but still requires bound chat URL and due interval. 18/18 sender Node tests PASS, SAT and PBOX publish exit0. Deployment recovery script C:\Users\pboru\Documents\deploy-openbrowser-safe-20261019.ps1 exit0 with healthy new PID and tested fallback executable presence. DPAPI metadata shows first actual direct submission requested 14:11:30 and next due 14:36:30. UIAutomation sees one visible wake text node of ControlType.Text and none in ControlType.Edit; server-side assistant reply not independently confirmed. Desktop and Start shortcuts now v1.72.5. Full details: docs/openbrowser-checkpoint-2026-10-19-1414-direct-send-live.md. Autosync, no manual commit/push. Native ChatGPT hourly Tasks remain OFF; old Windows OB-10 task not modified.
- 2026-10-19 13:16 CEST: handoff verified and independent SOURCE R07 fix. PBOX1 LIVE still v1.72.0, now PID17840 started 13:09:41 (cause of PID change unknown; no agent-triggered restart). SATANAMD 1.72.4 EXE verified version/SHA, still NO MANIFEST and NO GO for release. Git local master HEAD374ed298 dirty (38 index, 19 unstaged, 133 untracked); origin autosync/SATANAMD SHA d915235a. LiteHighGuard.js now scopes aria-busy to latest conversation turn, preserves visible Stop and tool-running guards; isolated Node tests 18/18 HighGuard and 5/5 HighSlider PASS, 3 syntax checks PASS. SOURCE/TEST only, NOT STAGED or LIVE; first test fixture TypeError corrected and logged. Checkpoint: docs/openbrowser-checkpoint-2026-10-19-1316-highguard-scoped-busy.md. OB3-018 real DevWake ACK, 1.72.4 promotion and R04 retry decision remain OPEN. Native Open Browser ChatGPT Tasks remain OFF; no manual commit/push.
- 2026-10-19 ~12:45: AUDIT/HANDOFF is current. READ FIRST: `docs/OPENBROWSER_DEEP_HANDOFF_2026-10-19_1245_ChatGPT.md` + `docs/OPENBROWSER_AUDIT_RUNLOG_2026-10-19_1220_ChatGPT.md` (progress checkpoint after each audit stage). ChatGPT hourly 'Open Browser — ciągły rozwój' OFF (last run 12:10 CEST, disabled 12:18 CEST); older 'Open Browser — rozwój aplikacji' OFF. PBOX1 verified LIVE v1.72.0 PID5852, buildWarsaw 08:47:33/sourceCommit 7bb4525, not v1.72.4. SATANAMD/GitHub SOURCE v1.72.4, staged SAT with EXE but NO manifest; gate+headless denied. GitHub autosync ahead 6 snapshots/18 changed files from PBOX 7bb4525. Source DevWake UI/state/probe/sender reviewed; live `dev_wake_ack` NOT independently confirmed; historical checkpoints report busy/answer_missing/uncertain. Retry policy conflict (architecture 02:24 permits bounded uncertain retry vs newer same-answer hash guard) requires explicit resolution, not silent code change. Legacy Windows Task `OpenBrowser ChatGPT Lite` still points telemetry (OB-10). New attempted PBOX read-only log aggregation and append to failure registry denied by tool safety, recorded in RUNLOG; no bypass. Only docs changed, no code changes, no manual commit/push, no native ChatGPT reminders, no restart of LIVE.
- 2026-10-19 08:57: SOURCE/STAGED Open Browser Lite v1.72.0. PBOX1 clean pull from technical autosync 7bb452587048; side-by-side Release EXE SHA256 DA6C2DD7B13A1B312EF2F459E7C38948F52ADE845A1A9103FC1A62C33B05159D, embedded build 2026-10-19T06:47:33Z (08:47:33 Warsaw), manifest/hash and JS promotion gate PASS, 17/17 .NET headless tests PASS. PBOX1 interactive GUI WebView2 isolation test with real ChatGPT-like form submit but no data-testid PASS, exit0 (about 66s). Desktop/Start shortcuts now v1.72 `--lite-open-safely`. One-shot no-GUI upgrade helper v1.72 PID19936 waits on LIVE 1.71 PID21560 to exit naturally; confirmed ONLY ONE live WebView browser and no force restarts. PBOX1 DPAPI wake enabled 25m, one bound chat, lastState uncertain_no_ack with next due 09:00:56, no confirmed ACK. Current v1.71 still LIVE until exit, source/stage not live. Audit docs/openbrowser-old-sender-audit-2026-10-19-0845.md; checkpoint docs/openbrowser-checkpoint-2026-10-19-0857-v172.md. No ChatGPT native reminders, no manual commit/push.
- 2026-10-19 08:45: User requested to recover PRE-LITE multi-message delivery design and enable reliable 25-minute Open Browser app-local development wake; no native ChatGPT reminders. PBOX1 LIVE v1.71 PID21560 from 07:44; 58 wake attempts observed, 56 confirmed busy/not-sent and 2 uncertain, 0 confirmed ACK. Original MainForm SendTextToChatAsync/InjectImmediatelyAsync injected contenteditable text then clicked 500ms later, but lacked receipt and used unsafe `button svg` fallback. MainForm + PageTap + WebViewMessageHandler also injected OpenBridge results and streamed raw SSE into ResponseExtractor; full tap is too expensive/invasive to enable in Lite by default. SOURCE v1.72: form-scoped PL/EN `button[type=submit]` recognition, safe legacy fallback, no voice button; diagnostics of available/disabled send button and phase after no ACK. JS 15 suites PASS (sender race 14 checks), Debug .NET 0 errors, isolated GUI WebView2 test with no data-testid and local form PASS in session1. Live delivery remains unconfirmed until `dev_wake_ack` or equivalent. Detailed audit docs/openbrowser-old-sender-audit-2026-10-19-0845.md. No manual commit/push.
- 2026-10-19 07:31: App-local periodic wake on PBOX1 LIVE v1.70 PID8704 did NOT deliver any confirmed wake: >=18 pairs dev_wake_send_attempt/dev_wake_deferred_busy. SOURCE v1.71 scoped sender busy() from generic page-wide aria-busy to visible Stop control, still blocks active generation and draft; distinct return codes busy_missing_composer/busy_generating/busy_draft_present, metadata-only diagnose(); DevWakeUi now logs specific reason and treats confirmed not sent as 2-minute deferred, without throwing send-uncertain. JS 15 suites PASS, gate PASS, .NET Debug build 0 errors, known MSB3277. STAGED/LIVE proof pending, no ChatGPT reminders, no commit/manual push. Checkpoint docs/openbrowser-wake-busy-fix-2026-10-19-0731.md.
- 2026-10-19 02:46: Owner authorizes occasional duplicate development wake; uncertain sending must not permanently disable app-local supervisor. LIVE PBOX1 now v1.70.0 PID8704, upgraded gracefully from 1.68 PID1980. DPAPI configuration Enabled=true, bound one chat, 25m; UIAutomation verifies `Autopraca: WŁ. 25m`, `Tryb HIGH`, current in-progress answer gives `answer_missing`; first actual periodic submission still needs confirmation. Source changes: repeat ambiguous wakes after 25 minutes, inspect previous ACK first, recover after restart, --lite-dev-wake-rearm without exposing URL; 13/13 JS suites, C# build 0 errors, wake selftest 22 PASS, PBOX1 offline WebView2 PASS, release manifest sha256 9c714692e77d3ad0c17f9a29dc64703cce34e74a06c357413fc474ba3cf8e676. No native ChatGPT Tasks, no commit/push. Details docs/openbrowser-wake-uncertain-retry-2026-10-19.md.
- 2026-10-19 01:36: Owner explicitly requested a **self-contained Open Browser Lite** wake every 25 minutes, for this one ongoing app-development conversation, and long autonomous work; strictly no OpenAI/ChatGPT reminders. Read URL from PBOX1 local WebView2 History (not stored in repo), planned 86-item roadmap docs/design/openbrowser-master-roadmap-v3-2026-10-19.md. SOURCE v1.68.0: LiteDevWakeState.cs DPAPI encrypted URL/receipt/timing, 25m interval, per-answer once-only, daily cap and completion marker, wake message not assigning tasks; LiteDevWakeUi.cs app-side polling, High/idle/draft/error checks, revalidate before send, ACK, no retry; flags --lite-dev-wake-arm and --lite-dev-wake-selftest, restore pinned conversation after natural restart. C# Debug built 0 errors and wake selftest PASS. LIVE PBOX1 was v1.67.0 PID13032 at 01:26; staging/deployment pending. Before LIVE must run all gates and verify safe restart, no parallel browser sessions. No commit or manual push.
- 2026-10-18 23:07: v1.67.0 STAGED on PBOX1, source Autosync 22db028, clean pull --ff-only, publish Release PASS, manifest SHA256 FDDA657AEFACC62A182DA593B8F31FAEFF94E4B2394774076740C3A9650D6C58 valid, 16/16 .NET headless tests, 12/12 JS suites, isolated real WebView2 GUI test in Windows session1 PASS including DIL error detection. LIVE remains v1.64.0 PID18412 responsive; technical one-time helper v1.67 PID8444 awaits natural old-process exit, no forcible restart and no second WebView2. OpenFill confirmed chapters 8 and 9 already exist; test declined to duplicate prompts. Real user-account Lite supervisor E2E pending. Docs/checkpoint openbrowser-p0-checkpoint-2026-10-18-2305.md. No ChatGPT reminders, no commits or manual pushes.
- 2026-10-18 23:05: P0 SOURCE v1.67.0: PBOX1 LIVE v1.64.0 PID18412 untouched. New 22:38:50→22:39:10 CEST delivery_timeout (20s); total 4 confirmed RenderProcessUnresponsive today and 3 delivery_timeout appearances. Analyzer schema2 critical timeline + counts validated on PBOX1 real logs. Luna gpt-6-luna API PASS. Book chat already has chapters 8 and 9, so independent OpenFill safely declined to duplicate them; no messages sent. Added durable per-chat DPAPI transport quarantine for delivery_timeout/response_load_failed, persistent across reboot and cleared ONLY after explicit user manual reconciliation; fail-closed Register and MarkSendingUncertain, observer per chat even when same error appears across navigation. Offline real WebView2 QA includes DIL error signal + ResumeProbe fail closed PASS. Source build Debug 0 errors, known MSB3277; ledger selftest 20 checks PASS, JS 12 suites PASS. Old 1.66 upgrade helper withdrawn pending v1.67 full stage. Checkpoint docs/openbrowser-p0-checkpoint-2026-10-18-2305.md. No ChatGPT reminders, no local commits/pushes.
- 2026-10-18 21:54: Open Browser P0 SOURCE v1.66.0: PBOX1 LIVE 1.64.0 unchanged PID18412, screenshot delivery_timeout 21:26:50 CEST→cleared 21:27:10 (20s), not evidence of server delivery; no retry. Luna real API gpt-6-luna credential shared PASS and synthetic book plan 'continue/high' PASS. High effort observed on actual UI via data-selected-reasoning-effort=high; DIL selectors modern; sender ACK now checks data-user-message-bubble with regression 7 PASS. HighGuard uses renderer backpressure after repeated timeout. Response signal checks short DIL failure placeholders; supervisor blocks if known failure even before revalidation. Added real offline isolated WebView2 UI E2E selftest (separate temp profile and local virtual host), SATANAMD Interactive Session1 PASS, after debugging session0 HWND 0x80070578 and test harness JSON boolean mismatch. Independent OpenFill test of real book chat blocked from sending by transient 'Nie udało się wczytać tej odpowiedzi' (no message sent). Node 12 suites PASS, Debug build 0 errors; FULL LIVE book auto-resume E2E not yet proven. Checkpoint docs/openbrowser-p0-checkpoint-2026-10-18-2154.md. No automations, no commit or manual push.
- 2026-10-18 20:55: P0 work resumed only in interactive chat, no ChatGPT reminders. PBOX1 LIVE v1.64.0 PID18412 not disturbed. Production diagnostics: Luna gpt-6-luna and shared Credential Manager key real API PASS, test conversation High 3/3 and idle in independent OpenFill read-only QA; live HighGuard was UNKNOWN because current ChatGPT effort is exposed by button `data-selected-reasoning-effort=high`, not button text. Real DIL messages use `[data-chatgpt-selection-message-id]` for assistant and `[data-user-message-bubble]` for user, old role selectors return 0. SOURCE v1.65.0 changes LiteHighGuard.js, LiteResumeProbe.js, adds LiteResumePreflight.cs SHA/High/idle recheck before send, Node 12 suites PASS, Debug C# 0 errors, 1 known warning MSB3277, promotion test gate PASS. Needs STAGED PBOX1 and actual WebView2 auto-resume E2E, no auto-send on unknown. Ref docs/openbrowser-p0-checkpoint-2026-10-18-2055.md.
- 2026-10-18 20:22: Read Claude audit docs/claude-audit-2026-10-18-1955.md from GitHub autosync/SATANAMD and verified source/PBOX1 independently. Owner/Claude approved ONE scanner exception for exact src/BridgeBrowserAlpha0/OpenBrowserLite.ico Git blob 50318466b3657f2df44428a9ab864208ee18992f; all other changed binaries remain prohibited. Autosync OK, remote e49d063. PBOX1 LIVE v1.64.0 PID18412 started 20:10:37, Responding=True, EXE SHA256 F82A3F7D646AB67E24F484D04C9652A8900164784E6A1CBE10FC09E782EF4DA7; release manifest is named lite-release-manifest.json (not openbrowser-release-manifest.json), hash matches, channel metadata remains staged and needs correction at a future controlled release. Historical OB-12..15 duplicate identifiers uniquely renamed with original aliases; resolved hard-DOM gate, Autosync scanner and prior NO-GO marked closed while preserving original refusal history. 10/10 JS and 16/16 .NET headless PASS per Claude audit/owner (not rerun in this doc-only turn). LIVE visual 16px/taskbar QA, Luna High E2E, task scheduler OB-10 (points obsolete telemetry), release channel label, and real DOM audit remain TODO. No COMMIT, no new ChatGPT automations, live window untouched. See docs/openbrowser-post-claude-audit-2026-10-18-2022.md.
- 2026-10-18 19:17: Operator screenshot: taskbar icon unreadable, no visible work status, ChatGPT delivery_timeout. PBOX1 real event at 17:44:51 CEST, cleared at 18:57:12 CEST; around first incident free RAM 7565MiB, no nearby HTTP >=400 and no nearby local answer snapshot, server delivery unknown. SOURCE-only v1.64: changed response_error_transition to clear stale UI status after error disappears, log error duration and display UI/transport-unknown distinction; taskbar title now Gotowe/Generowanie/Błąd; redesigned 8-size native EXE ICO, icon selftest PASS; corrected False Positive when user quotes an error, JS test 9 PASS. The older production 1.60 not updated. DOM counters remain zero in 136 recent samples despite CSS support, so added offline read-only LiteDomSelectorAudit.js 4 PASS to guide a future real DOM QA, not injected into live. SOURCE/TEST only. CRITICAL: Autosync blocked changed binary .ico with secret scanner and PBOX1 v1.63 publication was separately denied by tool safety; DO NOT bypass or claim staged/live. See docs/openbrowser-checkpoint-2026-10-18-1913-v164.md and docs/Rejestr nieudanych komend.md. No ChatGPT automations.
- 2026-10-18 19:08: v1.63 SOURCE/DEBUG TEST only. Standalone LiteRendererBackpressure selftest PASS but UI integration refused by tool safety; on PBOX1 Release publish refused too, no workaround, staging unverified. Aggregate regression command hung >209s and was cancelled by replacing only the test shell, so full matrix is NOT completed. Individual HighGuard (14), hard DOM (10+4), delivery/resume/backpressure selftests PASS and Debug build 0 errors. PBOX1 metadata-only renderer incident analyzer found 3 RenderProcessUnresponsive at 14:50, 16:31, 17:02 CEST and visible delivery_timeout 17:44; no server answer delivery proof. Canonical checkpoint docs/openbrowser-checkpoint-2026-10-18-1854-v163.md, failures docs/Rejestr nieudanych komend.md. All ChatGPT automations remain disabled by user instruction.
- 2026-10-18 19:03: v1.63 remains SOURCE/DEBUG TEST ONLY, PBOX1 release publish blocked by tool safety, no retry. Renderer backpressure pure class plus 10-check selftest PASS but integration in ChatGptLiteForm blocked by tool safety and NOT DONE. Full details docs/Rejestr nieudanych komend.md and docs/openbrowser-checkpoint-2026-10-18-1854-v163.md. PBOX1 read-only analyzer found 3 historical RenderProcessUnresponsive, 20 JS-delay events, 1 delivery_timeout; sampled free RAM 5386MiB at 17:02 failure, so not exclusively global RAM exhaustion. High Guard tests now 14 PASS and DOM hard prune includes synthetic 10k turns, 10 PASS. No automations ChatGPT were created/enabled. LIVE v1.60.0 must not be interrupted.
- 2026-10-18 18:53: SOURCE v1.63.0, not yet LIVE: High effort auto-switch is restricted to explicitly registered active project chats and will not click Stop during ongoing generation, change chat while pending, or change user draft. 14 HighGuard JS regression cases PASS, Debug build 0 errors. LiteDeliveryEvidence now prioritizes a current renderer/JS failure over earlier archived visible text; offline .NET test PASS. scripts/analyze-lite-renderer-incidents.ps1 is a new read-only, bounded event/resource correlation report; test and stage on PBOX1 pending. No ChatGPT automations created/re-enabled. PBOX1 LIVE still v1.60.0 as of latest inspection.
- 2026-10-18 18:28: All ChatGPT automations are disabled (32 existing tasks checked), and the owner explicitly forbids new reminder-driven runs until further notice. Current work must be performed only in this interactive chat. Full recovery/inventory: docs/openbrowser-audit-recovery-2026-10-18-1828.md. PBOX1 LIVE remains v1.60.0 PID21532; unsafe staged v1.61.0 auto-promotion watcher cancelled without touching LIVE. New SATANAMD source backup (SHA recorded in audit), PBOX1 projects/notes backup (no live cookies). Found and fixed resume-send asynchronous race plus hard-prune default mismatch; 9 JS suites and 13 .NET selftests PASS. SOURCE version now v1.62.0, STAGED/LIVE PBOX1 not yet updated. Maintain manual-only rollout gates, do not conflate source with live.
- 2026-10-18 16:50: Open Browser Lite SOURCE v1.60.0 and PBOX1 STAGED are separate from PBOX1 LIVE. Embedded assembly version includes exact UTC build second, rendered on title and toolbar as local Europe/Warsaw time. Code includes experimental opt-in resume supervisor for test chat only: DOM observations must be stable >=40s and >=3 samples, require verified High effort, clean composer and no tools/error; separate Luna Responses API requires OPENBROWSER_LUNA_MODEL and OPENAI_API_KEY configured in user's Windows environment, currently both absent on PBOX1. DPAPI resume ledger prevents uncertain-send retries. See docs/design/openbrowser-full-delivery-plan-v2-2026-10-18-1626.md and docs/openbrowser-checkpoint-2026-10-18-1643-v160.md. PBOX1 live former notes_dom_voice process should not be killed. Actual auto-resume E2E with a user-created 'testowy' chat still TODO; do not claim live supervision.
- 2026-10-18 15:52: Open Browser Lite iteration SOURCE/BUILD only (not LIVE): encrypted, crash-safe per-chat notes outbox (`LiteNotesOutbox.cs`, selftest PASS), safe navigation guard that refuses to leave chat while generating/draft/notes unsaved (`LiteTabSwitchGuard.cs`), headless adaptive warm-tab residency planner (0-2 warm per available memory, selftest PASS), metadata-only passive CDP network correlation (`LiteTransportDiagnostics.cs`, selftest PASS). UI buttons for outbox and initialization of network CDP NOT connected because tool safety blocked exact edits; see docs/Rejestr nieudanych komend.md entries OB-11..14. Full checkpoint docs/openbrowser-checkpoint-2026-10-18-1552.md. SATANAMD Debug build PASS, 0 errors, known MSB3277. PBOX1 live earlier release not interrupted.
- 2026-10-18 14:03: PBOX1 was fully unresponsive and rebooted at 13:53:23; post-reboot monitoring survived and resumed successfully at 14:01. Pre-reboot resource samples show severe *system-wide* memory pressure (274 MiB free at 13:45; >6 GiB pagefile occupied), with MS Edge, ChatGPT Desktop, Claude and WebView2 among consumers. Lite UI heartbeat continued through 13:52:54; this does not prove renderer/desktop responsiveness. No post-instrumentation WebView2 ProcessFailed recorded, CPU deltas of Lite renderer did not indicate a saturated single core. Scheduler transient 0x800710E0 around 13:59 resolved to success; on-battery task is currently disallowed. See docs/openbrowser-lite-incident-2026-10-18.md. Root cause not proven.
- 2026-10-18 13:44: Resource monitor is now installed on PBOX1 after the operator used schtasks.exe. Verified by LC MultiHost: Task Scheduler `OpenBrowser Lite Resource Monitor`, LastTaskResult=0, 2-minute cadence, latest JSONL samples present (3 lines). Runs while Windows user is logged in due to /IT. Post-reboot sample execution has not yet been verified. This supersedes the earlier failed task-registration attempt; see docs/openbrowser-lite-incident-2026-10-18.md.
- 2026-10-18: Added `scripts/monitor-lite-resources.ps1` plus hidden launcher `scripts/monitor-lite-resources-hidden.vbs` for independent read-only per-process CPU/memory snapshots. Manual execution on PBOX1 PASS, JSONL `resources-*.jsonl` exists. The initial Task Scheduler registration attempt timed out and was not installed at that point; this was resolved by the operator's later schtasks.exe registration (see latest entry above). Record in `docs/openbrowser-lite-incident-2026-10-18.md`.
- 2026-10-18: Diagnosed PBOX1 Lite `RenderProcessUnresponsive` under severe system-wide RAM pressure (16 GiB RAM, measured 85 MiB free, ~6.4 GiB pagefile in use). The prior Lite build had no persisted process error telemetry. Added `LiteEventLog.cs` and Lite UI instrumentation for WebView2 lifecycle, render failures, navigation outcomes and UI heartbeat; event metadata only, no chat contents/tokens. Debug build PASS and PBOX1 Release publish PASS; the new PBOX1 task and shortcuts launch `%LOCALAPPDATA%/Programs/OpenBrowserChatGPTLite_20261018_telemetry/BridgeBrowserAlpha0.exe`. Profile preserved; lifecycle/navigation/UI-heartbeat JSONL confirmed with WebView2 155.0.4283.60. SATANAMD Release output remains locked by its running PID 13384. See `docs/openbrowser-lite-incident-2026-10-18.md`.
- 2026-10-18: ChatGPT Lite preview is implemented in src/BridgeBrowserAlpha0/ChatGptLiteForm.cs. Launch via --chatgpt-lite, or scripts/launch-chatgpt-lite.ps1 after building Release. It is a single-window WebView2 with a dedicated user profile under %LOCALAPPDATA%/OpenBrowser/ChatGPT-Lite/Profile. It does not instantiate MainForm, OpenBridgeHost, PageTap, response interception, OpenBridge diagnostics or ConversationTrimmer; Lite has separate metadata-only event logs but does not capture or preserve conversations. SATANAMD interactive task 'OpenBrowser ChatGPT Lite' and a desktop shortcut were created for testing; ChatGPT login/navigation requires user verification.
- 2026-10-18: The relocated src/BridgeBrowserAlpha0/BridgeBrowserAlpha0.csproj and three smoke test csproj references were repaired. Debug and Release builds pass, 0 errors with known WindowsBase warning; Protocol, Host and ResponseExtractor smoke tests pass after re-running dotnet restore.
- 2026-10-18: Local SATANAMD working tree has an unfinished, staged relocation of 38 source files from releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0 to src/BridgeBrowserAlpha0. The old .csproj path no longer exists in this worktree. The moved .csproj has unresolved relative paths to assets. No build verification or runtime fixes are part of this housekeeping pass.
- 2026-10-18: Maintenance checkpoint and privacy exclusions are recorded in docs/maintenance-open-browser-2026-10-18.md. Runtime downloads remain on disk but are ignored by Git.
- Source migration from docs/development/*, docs/runtime/environment.md, config/start-prompt.txt, and design docs is reflected here.
- OpenBrowser project working directory is D:\projects\open-browser.
- Developer agents must begin technical work with git status --short.
- Current developer-agent documentation entrypoint is docs/agent/READ_BEFORE_WORK_new.md.
- Current module and executor registry is docs/runtime/modules-index_new.md.
- SessionRhythm is documented as planned/pending, not confirmed active implementation.
- RAW payload mechanism exists for long or quoting-sensitive executor payloads.
- Current Open Questions include cross-tab communication and watchdog/recovery.

## Current repository root

OpenBrowser/OpenBridge work is performed in:
- D:\projects\open-browser

Developer agents must not work outside this path unless explicitly instructed.

## Current active command model

Current OpenBridge command envelope version:
- version 001

Current Host commands:
- HST_HELP
- HST_STATUS

Current executors:
- PS
- PY

Related help commands:
- PS_HELP
- PY_HELP

Current tools reported by OpenBridge status may include:
- claude
- github
- linkedin
- local
- n8n
- web

## Current RAW payload model

Runtime environment documentation describes a RAW payload mechanism for commands that are long or fragile under JSON escaping.

Use RAW payload for:
- long scripts;
- quote-heavy commands;
- backslashes;
- JSON;
- markdown;
- OpenBridge-sensitive text.

Operational rule:
- RAW belongs inside the payload JSON string;
- RAW outside the JSON object makes the envelope invalid;
- RAW is preferred over manual JSON escaping for complex payloads.

## Current build and validation model

After every change to .cs files:
- run dotnet build.

Historical build command (no longer valid after the pending local source relocation):
- dotnet build D:\projects\open-browser\releases\BridgeBrowserAlpha0\src\BridgeBrowserAlpha0\BridgeBrowserAlpha0.csproj

Current local project file is src/BridgeBrowserAlpha0/BridgeBrowserAlpha0.csproj, but its asset references need correction before this path can be used as a validated build command.

After changes to OpenBridgeProtocol:
- dotnet run --project tests/OpenBridgeProtocolSmoke/OpenBridgeProtocolSmoke.csproj

After changes to OpenBridgeHost or executors:
- dotnet run --project tests/OpenBridgeHostSmoke/OpenBridgeHostSmoke.csproj

Known warning:
- MSB3277 WindowsBase can be ignored only if there are 0 errors.

## Current versioning model

Build identity uses:
- build number: git rev-list --count HEAD
- hash: git rev-parse --short HEAD
- date format: dot-separated date

Source evidence:
- docs/development/decisions.md
- docs/development/journal.md

## Current active documentation model

Developer agents must use repository documentation as current truth.

Key files:
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/development-rules_new.md
- docs/agent/coding-invariants_new.md
- docs/agent/documentation-update-checklist_new.md
- docs/agent/commit-rules_new.md
- docs/runtime/modules-index_new.md
- docs/feedback/field-notes_new.md

## Current module and executor status

Active:
- HST
- PS
- PY
- Downloads
- Humanizer
- PageTap

Existing documented area:
- ConversationTrimmer

Planned/pending design area:
- SessionRhythm

## Current verified capability: ChatGPT sandbox downloads

OpenBrowser currently has verified live ChatGPT sandbox file download handling.

Current flow:
- ChatGPT exposes a generated file in the conversation UI.
- PageTap detects the file control.
- PageTap triggers guarded activation.
- WebView2 DownloadStarting handles the browser download.
- The file is saved to the OpenBrowser downloads directory.
- HST_STATUS reports recent downloads.

Verified file:
- openbrowser_real_sandbox_test.txt

Verified content:
- Hello from live test

Verified events:
- page_sandbox_download_candidate
- page_sandbox_download_click
- download_starting
- download_completed

## Current active architecture constraints

Do not restore:
- old BridgeBrowserHelper
- old Cloud Code connector

Do not introduce without explicit approval or explicit task scope:
- new layers
- services
- managers
- registries
- factories
- dispatchers
- queues
- watchdog
- cross-tab
- multi-tab registry

Do not commit:
- config/local files
- secrets
- unrelated generated downloads

## Current open questions

Needs owner/architect decision:
- cross-tab communication;
- watchdog/recovery.

## Current security and folder evidence

Security and environment sources state:
- Open Browser is designed for local use and must not be exposed to the Internet.
- There is no authentication or network security layer for public exposure.
- Secrets, API keys, tokens, and credentials belong in config/local/.
- config/local/ is git-ignored and must not be committed.
- The previous application path was releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/; local sources are currently staged for relocation to src/BridgeBrowserAlpha0/.
- Local OpenBridgeHost code is currently under src/BridgeBrowserAlpha0/OpenBridgeHost/.
- Local OpenBridgeProtocol code is currently under src/BridgeBrowserAlpha0/OpenBridgeProtocol/.
- General command executor lives under OpenBridgeHost/GeneralCommand/.
- diagnostics/pipeline-output/ is used for pipeline diagnostic dumps.

Release/source evidence:
- releases/BridgeBrowserAlpha0 contains alpha release notes and changelog material.
- Historical release docs include ConversationTrimmer and older alpha context; active truth still comes from current *_new.md docs and current code.

## Current active tool integrations

Tool source migration identifies these active tool integration folders:
- tools/claude/: Claude Code launcher through DeepSeek Anthropic-compatible API.
- tools/github/: GitHub file publishing through Contents API, without local git.
- tools/linkedin/: LinkedIn posting through OAuth 2.0 and REST API.
- tools/n8n/: n8n workflow management through API and Supabase-related scripts.
- tools/web/: browser/web automation utilities for fetch, forms, and browser-related tasks.

Tool rules:
- Each tool folder owns its own README and working guide.
- Secrets belong in config/local/, never in tools/.
- Tool documentation should not be duplicated into Host or runtime help.
- HST_STATUS may report available tools, but detailed usage belongs in tools/* documentation.

## Current implementation evidence from handoff

Latest handoff sources identify these active implementation areas:
- PageTap.js detects ChatGPT download buttons, including button.behavior-btn, and dispatches the click sequence.
- BrowserTabRuntime.cs manages WebView lifecycle, PageTap, module loading, and WebView2 DownloadStarting handling.
- AppPaths.cs defines the Downloads path and EnsureAll creates it.
- OpenBridgeRuntimeApproval.cs participates in HST_STATUS recent downloads reporting.
- OpenBridgeHost.cs is a shared singleton with a concurrency lock.
- GeneralCommandExecutor.cs runs local processes, captures stdout/stderr, and truncates large output.
- IOpenBridgeCommandExecutor.cs is the hot-swap interface for executors.

Current downloads path evidence:
- AppPaths.Downloads currently resolves to releases/BridgeBrowserAlpha0/downloads/ according to handoff.md.

Historical do-not-restore evidence:
- Old markers are dead.
- ClaudeCodeExecutor and old CommandExecutor were removed and replaced by GeneralCommandExecutor.
- BridgeBrowserHelper must not be restored.
- Human approval panel was removed from runtime flow.

## Current verified test evidence

From test source migration:
- HST_STATUS has verified output areas: ID, build, working directory, Git status, tools, recent downloads section.
- PY executor works for basic Python execution and UTF-8 output according to later test results.
- RAW block inside payload is verified for PY, and invalid RAW placement outside JSON correctly fails.
- n8n workflow listing has passed in test sources.
- GitHub authentication check has passed in test sources.
- web_fetch.py was initially missing dependencies in older results, but later test results show it working.
- PS stdout may still corrupt Polish characters. Treat PS stdout UTF-8 as an open issue unless a later verification proves it fixed.
- Older sandbox download test failed in test-results-v4.md, but later live OpenBrowser tests verified ChatGPT sandbox downloads. Treat the old failure as historical unless reproduced.
## Current handoff for AntiGravity

Current handoff file: handoff.md.

The handoff is not a replacement for docs/agent/READ_BEFORE_WORK_new.md. It is a task handover for AntiGravity after the documentation cleanup.

Near-term AntiGravity work should be inventory and planning only:
- inspect dirty working tree, especially PortalScanner.js and runtime downloads;
- propose how to separate OpenBrowser from the portal-document-download direction, possibly under the user-facing idea name Otwarty Portal Sądowy;
- propose a procedure for creating a clearly distinguishable user-runnable build for Paweł on explicit owner request;
- do not implement the split or build procedure until accepted.


## Current repository caution
## Current folder policy

Folder governance is defined in docs/agent/folder-policy_new.md and local FOLDER.md files. New root folders require policy update or architect approval.


There are existing older docs and new *_new.md docs during the documentation-governance migration.
Developer agents must not assume old files are current if a *_new.md replacement exists and is part of the current documentation-governance work.

Historical or replaced content must not be treated as current truth unless migrated into active documentation.



<!-- COMPLETE_DOC_UPDATE_CURRENT_STATE_20260521 -->
## Current help and transport documentation state

Active model-facing help files:
- docs/runtime/hst-help_new.md: OpenBridge/HST help index, command frame, HST commands, tool help commands, RAW/Base64 framing;
- docs/executors/PS/help_new.md: PowerShell executor help;
- docs/executors/PY/help_new.md: Python executor help;
- docs/agent/help-command-routing_new.md: maps help commands to documentation files.

Active operational policy files:
- docs/agent/model-feedback-policy_new.md: where and when models record operational observations;
- docs/agent/external-tool-policy_new.md: how models treat external tools differently from local filesystem work;
- docs/feedback/antigravity-text-transport-task_new.md: architecture task for text transport and encoding.

Node.js may be available through the local Windows environment, but NODE is not an OpenBridge executor unless listed by implemented commands. Models must not infer a NODE executor from Node being installed.
<!-- /COMPLETE_DOC_UPDATE_CURRENT_STATE_20260521 -->
