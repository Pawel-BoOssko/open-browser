# Open Browser Lite: plan wykonawczy po przejęciu, 2026-10-21

**Status 22.10.2026 10:55:** ACTIVE. 33 naturalne foreground wakes tej jednej rozmowy, ostatni 10:47:39 receiptConfirmed=true, generowanie od10:47:56, bez potwierdzonego końca odpowiedzi; PBOX1 LIVE1.72.21 PID8216. SOURCE/TEST v1.72.54 SATANAMD: OBL-0.3 NavigationStarting wiąże NavigationId tylko z dokładnym docelowym chat URI i po wydaniu Navigate; spóźnione inne chat/login/foreign nawigacje ignorowane, wcześniejsze completion-id/URL/history guardy zachowane. Node22/22, .NET Release publish, 8 published EXE, isolated real offline WebView2 204/204, SHA PASS, STAGED tylko SatanAMD. Real authenticated 5–10 background E2E, dozwolony PBOX1 deploy, >=8h nowego LIVE RAM/DOM/CPU soak, real DIL QA i końcowy UI nadal otwarte.  
**Punkt wznowienia:** `docs/OPENBROWSER_LITE_HANDOFF_ODTWORZENIE_2026-10-21_ChatGPT.md`, aktualny checkpoint `docs/OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md`.  
**Dokument historyczny:** `docs/design/openbrowser-master-roadmap-v3-2026-10-19.md` ma 86 zadań. Nie traktować ich jako 86 równoległych priorytetów.  
**Źródła aktualne 22.10 10:55:** SOURCE/TEST v1.72.54 SATANAMD, test manifest SHA256 5ea2d9edf44cdc2a320c739540ded9cfd336be64bb11202dcd8946e1b89b5c9d; PBOX1 LIVE nadal v1.72.21 PID8216, 33 foreground wakes, finalna odpowiedź bieżąca niepotwierdzona. STAGED tylko SATANAMD; wcześniejsza odmowa PBOX1 publish v1.72.22 bez obejścia. Checkpoint `docs/OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md`, baseline `docs/OPENBROWSER_LITE_RESOURCE_BASELINE_2026-10-22.md`.  
**Reguły:** bez ChatGPT Tasks, ręcznego commit/push, resetu dirty repo, masowego zamykania okien. Autosync dostarcza wersje źródłowe. Checkpoint po każdym etapie.

## Zasady produktu

Tylko jeden właściciel, maksymalnie kilkanaście rozmów (typowo 5–10), lokalnie zaszyfrowane już istniejące ustawienia i jeden nadzorca. Nie dodawać usług backendowych, wielowarstwowych kolejek, nowych managerów i rozbudowanego uwierzytelniania. Dwa wyniki mierzalne: **praca faktycznie się wznawia** i **RAM/CPU/DOM nie rosną bez końca**.

## ETAP 0: krytyczny tor samokontynuacji

- [x] **OBL-0.1: Autostart/keepalive**. Jeden zewnętrzny, prosty Windows Scheduled Task uruchamiany w zalogowanej sesji użytkownika co 5 minut. Uruchamia Start Menu `Open Browser - ChatGPT Lite.lnk` tylko jeśli nie działa żadna instancja `BridgeBrowserAlpha0`. Bez restartów działającego procesu i bez automatycznej instalacji STAGED. Status wykonywania do pliku. Test na PBOX1: brak drugiej instancji; po naturalnym zamknięciu uruchomiona prawidłowa binarka.
- [x] **OBL-0.2: Własny czat projektu**. Wyraźnie identyfikować URL aktualnego czatu projektowego (nie zgadywać na podstawie suffixu poprzedniego czatu); do rejestru przypisać `Enabled=true, Minutes=25`, tekst poniżej. Inne czaty zachowują ustawienia.
- [ ] **OBL-0.3: Naprawa worker WebView2**. Obserwowany 21.10 defekt: 111 preflight + 118 skips, `composer=true`, `targetMatches=true`, 0 user/assistant/turn nodes. Wersja SOURCE 1.72.16 wymaga DOM historii i zapisuje liczby, ale nie przywraca gwarantowanie historii. Odróżnić niezhydratowaną powłokę od ukrytej/zalogowanej historii, skontrolować NavigationCompleted, gotowość rozpoznania DIL, dopiero realny test. Nie znosić sprawdzenia celu i aktywnego generowania.
- [x] **OBL-0.4: Gate wydania**. Dla kandydata 1.72.16: pełne JS, .NET headless, izolowany WebView2, manifest SHA, przygotowanie backupu DPAPI, graceful switch z LIVE 1.72.15 przy idle, zachowanie fallbacku. Nie twierdzić, że folder kandydata = wdrożenie.
- [x] **OBL-0.5: Realne wybudzenie**. Właściwy czat projektu otrzymuje jeden unikalny `[OB-receipt]`, widać nową wiadomość użytkownika, następuje nowa odpowiedź modelu i powstaje następny checkpoint. Potem potwierdzić drugi naturalny cykl. Jeden czat w foreground, drugi w tle; osobna walidacja bez pomylenia rozmów.
- [ ] **OBL-0.6: Recovery przerwań**. Jeśli odpowiedź modelu została przerwana, powtórny start czyta ten dokument i plik statusu. Raz na etap krótki, jawny zapis wyniku, PID, wersji, testów, blokad i następnego kroku. Powtarzanie niepewnych wysyłek: użytkownik w historii 19.10 preferuje rzadki dubel nad trwałe zatrzymanie, ale trzeba rozstrzygnąć dopiero po naprawieniu faktycznej wysyłki i zachować licznik prób.
- [x] **OBL-0.7: Stwierdzenie autonomii tej rozmowy (wstępne)**. Dwa naturalne wybudzenia bez ręcznej interwencji użytkownika + checkpoints + brak wzrostu liczby okien. Dopiero wtedy przejście do priorytetu zasobowego i deklaracja, że autopraca działa.

### Tekst wybudzenia czatu projektu

> Kontynuuj rozwój Open Browser Lite w ramach dwóch priorytetów: niezawodna autopraca oraz małe RAM/CPU/DOM. Odczytaj D:\projects\open-browser\docs\OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md i plan wykonawczy; zweryfikuj stan SOURCE, PBOX1 i logów, wybierz najwyższe niedokończone zadanie, wykonaj je bez pytania o zgodę przy zwykłych zmianach, zrób testy i dopisz krótki checkpoint. Nie twórz ChatGPT Tasks ani ręcznych commit/pushy. Gdy napotkasz blokadę, zapisz ją i przejdź do niezależnego zadania. Po ukończeniu wszystkiego nie budź więcej.

Nie dodawać niepotrzebnych szczegółów do samej wiadomości. Powyższy tekst jest domyślną propozycją dla rozmowy projektowej, docelowo edytowalną w UI.

## ETAP 1: odchudzanie pamięci i DOM

- [ ] **OBL-1.1: Stan wyjściowy**. Pomiar na jednej długiej rozmowie: liczba wiadomości/elementów DOM, renderer/WebView2 private bytes, RAM hosta, CPU, czas reakcji na zmianę rozmowy, historia 5m/1h/24h. Nie sugerować z samego licznika RAM źródła problemu.
- [ ] **OBL-1.2: Reużycie kodu**. Przejrzeć istniejące `LiteDomOptimizer.js`, `LiteDomHardPrune.js`, `LiteRendererBackpressure.cs`, `LiteTabResidencyPlanner.cs` i sprawdzone fragmenty starszej architektury Open Browser. Usunąć dublujące się odświeżacze/probe.
- [ ] **OBL-1.3: Łagodne odchudzanie domyślnie ON**. `content-visibility` i ograniczony wpływ CSS tylko na zakończone nieaktywne tury; aktywna odpowiedź, composer i narzędzia bez zmian. Test DIL na aktualnej stronie.
- [ ] **OBL-1.4: Twarde cięcie po testach**. Przy wzroście DOM powyżej zmierzonego progu zachowywać najnowsze wiadomości, ewentualnie archiwizować starsze. DOM React może odtworzyć, więc najpierw sprawdzić stabilność i możliwość przywracania; do czasu rzeczywistego QA flaga OFF.
- [ ] **OBL-1.5: Jeden worker**. Maksymalnie foreground + jeden okresowo używany WebView2 worker dla 5–10 czatów. Nie uruchamiać osobnego ciężkiego renderera na każdy harmonogram. W spoczynku ograniczać intensywność prób. Nie zabijać rendererów w trakcie pracy modelu.
- [ ] **OBL-1.6: 8-godzinny soak**. Dwa rzeczywiste długie czaty + 3–5 kolejnych harmonogramów, obserwacja szczytów i stabilności, bez wzrostu RAM/DOM bez ograniczeń.

## ETAP 2: uproszczenie interfejsu i konfiguracji

- [ ] Jedna wąska belka: nazwa czatu, `Autopraca WŁ/WYŁ`, czas następnego wybudzenia, `RAM/DOM`, przycisk `Wybudzenia`, dostęp do `Diagnostyka` bez osobnego centrum administracyjnego.
- [ ] W `Wybudzenia`: lista 5–10 rozmów, proste ON/OFF, interwał, ostatni sukces, ostatnia blokada; edycja tekstu i wymuszenie pojedynczego wybudzenia wyłącznie po otwarciu szczegółów.
- [ ] Domyślnie nowe czaty: autopraca WYŁ, uruchomienie ręczne jednym kliknięciem. Po włączeniu 25 minut, zero obowiązkowej konfiguracji promptu. Czat projektu: jawnie uprzywilejowany, zawsze włączony, dopóki praca nie jest zakończona.
- [ ] Kontrola RAM/DOM pracuje automatycznie; zaawansowane limity i wykresy pod `Diagnostyka`. Brak osobnego ustawienia na każdy czat bez realnej potrzeby.
- [ ] Zależne funkcje z 86-punktowego starego backlogu połączyć lub odłożyć; nie tworzyć modułów MCP/UI niezwiązanych z dwiema głównymi funkcjami przed zakończeniem etapów 0–1.

## Raportowanie i kryterium ukończenia

Każdy checkpoint: data/godzina Warszawa, zadanie, aktualne SOURCE/TEST/STAGED/LIVE, wykonana czynność, wynik/test, blokada i precyzyjny next step. W razie przerwania nie twierdzić, że implementacja lub wysyłka się udała. Pojedynczym źródłem statusu jest `docs/OPENBROWSER_LITE_WORK_STATUS_2026-10-21.md`. Wersje i Git tylko przez Autosync.  
**Ukończenie:** co najmniej dwa naturalne udane wznowienia + jeden stabilny, efektywny mechanizm odchudzania DOM, zaakceptowany UI i test resource soak.