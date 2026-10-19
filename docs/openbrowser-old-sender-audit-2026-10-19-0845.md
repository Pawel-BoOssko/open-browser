# Open Browser: audyt starej wysyłki i naprawa Autopracy (2026-10-19 08:45 CEST)

## Źródła i stan
- Kanoniczny kod SATANAMD `D:\projects\open-browser`, historyczne niezacommitowane zmiany. PBOX1 LIVE v1.71.0 PID 21560 (07:44:50), `Responding=True`. 25-minutowy nadzór był już lokalnie włączony; nie jest przypominajką ChatGPT.
- W logu PBOX1 z 19.10: 58 `dev_wake_send_attempt`, 56 `dev_wake_deferred_busy`, 2 `dev_wake_submission_uncertain`, bez potwierdzonego `dev_wake_ack` w odczytanych danych. Próba 08:35 CEST zwróciła `no_visible_ack`. To oznacza brak potwierdzenia, NIE pewność, że wiadomość nie została dostarczona. Nie ponawiać tego samego identyfikatora.
- OpenFill task `OF-20261019-0818-3826` (odczyt nowej, pustej sesji ChatGPT, bez wysyłki) potwierdził, że dzisiejszy przycisk ma `button[type=submit][aria-label=Wyślij]` wewnątrz `form[data-chatgpt-composer]`, bez `data-testid=send-button`. Sąsiaduje z przyciskiem fioletowego trybu głosowego.

## Inwentaryzacja PRE-LITE
| Moduł | Funkcja w starym Open Browser | Ryzyko / wnioski |
|---|---|---|
| `MainForm.cs` `SendTextToChatAsync` | Humanizer.Wrap, opóźnienia człowieka `ComputeHumanDelayMs`, `SessionRhythm`, potem DOM #prompt-textarea/.ProseMirror/contenteditable + `textContent` + `input` i po 500 ms klik w `data-testid=send-button`, lub przycisk SVG. | Historycznie wysyłał wiele wiadomości, ale bez identyfikatora receipt, bez ACK; fallback `button svg` może dziś trafić w nieodpowiednią ikonę. NIE kopiować go do Lite. |
| `MainForm.cs` `InjectImmediatelyAsync` | Bez oczekiwania wrzuca wynik OpenBridge (w tym komunikaty timeout). | Przy niepewnej wysyłce mógł duplikować. Historyczny brak blokad nie dowodzi poprawności transportu. |
| `MainForm.cs` `ExecuteAndInjectResultAsync` | Pobiera stdout/stderr z `OpenBridgeRuntimeApproval.ExecutePendingAsync`, wkleja rezultat do rozmowy. | Stare duże wolumeny to w dużej mierze odpowiedzi z narzędzi, nie autonomiczne budzenie modelu. |
| `PageTap.js` i `WebViewMessageHandler.cs` | Instrumentuje `fetch`/XHR/WebSocket, klonuje/rozdziela SSE, wysyła fragmenty do `ResponseExtractor`, zapisuje trace. | Pozwala analizować streaming, ale jest ciężki, może powiększać RAM i ma szerszy dostęp do treści. Nie włączać pełnego PageTap w Lite; korzystać z metadanych CDP i małych snapshotów. |
| `LogWriter/ResponseExtractor` | Przechowywanie zrzutów odpowiedzi i wyników narzędzi. | Cenne do diagnozy awarii, lecz prywatne dane wymagają redakcji/retencji/szyfrowania. |

## Co zachować i co poprawiono
- Zachować sprawdzony wzorzec prostego wpisu do bieżącego edytora i kliknięcia dokładnego przycisku po krótkim czasie; utrzymać sprawdzanie braku aktywnego generowania i braku prywatnego szkicu.
- Nowa implementacja Lite (`LiteResumeSender.js`) ma `receipt`, `phases`, ponowną weryfikację URI, edytora i treści tuż przed kliknięciem; `LiteDevWakeState.cs` przechowuje DPAPI identyfikator i harmonogram; `LiteDevWakeUi` sprawdza High, brak błędu, stabilność, potwierdzenie dymku. W odróżnieniu od starego OpenBridge nie ponawia wysyłek o niepewnym skutku.
- Wersja SOURCE v1.72.0: sender wybiera `button[type=submit]` o dokładnej etykiecie PL/EN *we właściwym formularzu powiązanym z edytorem*, z kontrolowanym fallbackem `data-testid` tylko gdy należy do formularza. Nigdy nie wybiera `button svg` i nie klika przycisku głosowego. Diagnostyka metadanych `diagnose()` podaje `sendButtonFound`, `sendButtonDisabled`, długość szkicu i reason (bez treści). `LiteDevWakeUi` zapisuje `dev_wake_delivery_phase` po braku ACK, razem z informacją czy kliknięcie w ogóle wywołano.
- `lite-resume-send-race.test.cjs`: 14 PASS w tym prawdziwy typ przycisku bez data-testid i brak kliknięcia w przycisk głosowy. Inne testy Node: 15/15 suites PASS; build Debug .NET 0 błędów, jedno znane MSB3277; GUI integracyjne SATANAMD w interaktywnej sesji Windows 1 z nowym lokalnym formularzem (bez ChatGPT sieci/profilu): PASS exit 0.
- NIE ZALICZAĆ rzeczywistego wznowienia do czasu `dev_wake_ack` albo odpowiadającego śladu widocznego w DIL. `submission_requested` oznacza tylko zainicjowanie, a `click_invoked` tylko kliknięcie. Odpowiedź modelu i trwały ACK pozostają do zbadania.

## Kolejność wdrożeniowa
1. Autosync SATANAMD, clean pull `autosync/SATANAMD` do PBOX1, side-by-side wydanie 1.72, manifest, bramka JS i 16+ selftestów. Nie zmieniać aktywnego EXE podczas pracy bieżącej sesji.
2. Weryfikacja `dev_wake_enabled` z DPAPI tylko poprzez odczyt metadanych. Po kontrolowanym uruchomieniu 1.72 na tej samej zalogowanej sesji (bez równoległego WebView2 na jednym profilu) obserwować `dev_wake_delivery_phase`, `dev_wake_ack` i przycisk form submit.
3. Rozwiązać przypadek nadal niepotwierdzonego wysłania BEZ powtarzania identycznego receipt. Po ewentualnym restart/upgrade wyświetlić użytkownikowi potwierdzony status oraz zależność od aktywnej karty.
4. W przyszłości niezależny watchdog aplikacji uruchamiający ją po awarii można dodać poprzez Windows Task Scheduler, ale nie zastępuje to rozpoznania tego, czy wiadomość rzeczywiście wysłano. Nie tworzyć ChatGPT Task/reminder.

## Zasady
Brak ręcznych git commit/push. Autosync bez poszerzania wyjątku skanera. Nie używać polecenia odzyskiwania przez zignorowanie błędów transportu. Wszelkie odmowy bezpieczeństwa opisywać w `docs/Rejestr nieudanych komend.md` z konkretnym narzędziem i zakresem.
