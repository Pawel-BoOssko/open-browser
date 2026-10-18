# Open Browser — P0 diagnostyka, Luna, High, testowy czat — checkpoint 2026-10-18 20:55 CEST

## Status źródło/test/staged/live
- SATANAMD SOURCE: `D:\projects\open-browser`, branch master z 150 zastanymi dirty changes; brak commita i ręcznego push. Wszystkie zapisy kodu wyłącznie przez RDC, LC MultiHost tylko system/tests. Nie uruchamiać żadnych automatyzacji ChatGPT.
- PBOX1 LIVE podczas tej pracy v1.64.0 PID18412 (start 20:10:37), WinForms Responding, MainWindowHandle obecny. Nie restartowano. Poprzedni `delivery_timeout` nie stanowi podstawy do automatycznego ponawiania.
- API Luna: uruchomiony na prawdziwej zainstalowanej binarce PBOX1 `--lite-luna-connectivity-test`: EXIT0, Windows Credential Manager FileScannerAI/OpenAIAPIKey dostępny, model `gpt-6-luna`, wynik `continue` high-confidence na syntetycznym tekście książki. NIE odczytano ani nie ujawniono klucza.
- PBOX1 monitor 18.10.2026 20:45 CEST: 8 procesów WebView2, najcięższy renderer PID17664 pamięć robocza 1001.8→794.1 MiB w odstępie 123s, zużycie CPU ~40.1% jednego rdzenia w tym interwale, RAM wolna 4265 MiB. Zmienna wysoka aktywność, brak dowodu wycieku pamięci.
- OpenFill READ ONLY QA prawdziwego ChatGPT, testowy URL tylko https://chatgpt.com/c/6ad4d68a-a304-83eb-950a-bb4284f58c79: High/Wysoki '3 z 3' potwierdzone, brak generating/tool/draft/visible error, książka 7 rozdziałów i późniejsza odpowiedź asystenta. To przeglądarka OpenFill, NIE produkcyjne WebView2 Lite.

## Źródłowe poprawki v1.65.0
- HighGuard: rzeczywisty przycisk `button[aria-label="Wybierz model ChatGPT"]` ma bez otwierania menu atrybut `data-selected-reasoning-effort="high"`. Stary kod patrzył na tekst przycisku i produkcja logowała `high_guard_mode_changed unknown`; poprawiono. Oddzielna pozycja menu `div[data-reasoning-slider]` ma span role=slider value 2, min0 max2; po wyborze niskiego trybu guard używa kontenera dostępnego przez ArrowRight, weryfikuje później realny atrybut. Nigdy nie klika Stop, przy busy/draft odmowa.
- LiteResumeProbe.js: klasyczne role/tury zwracają 0 w prawdziwym DOM. Zweryfikowano semantyczny DIL: `[data-chatgpt-selection-message-id]` 2 ostatnie odpowiedzi, `[data-user-message-bubble]` 2 wiadomości użytkownika, union daje 4 w kolejności DOM; `[data-turn-key]` 2. Fallback odczytu ostatniego role i tekstu dodano bez modyfikowania DOM. Hard-prune pozostaje OFF / nie ruszano.
- LiteResumePreflight.cs: po decyzji Luny tuż przed ewentualnym wysłaniem ponowne sprawdzenie tej samej rozmowy, High, żadnego generating/tool/draft/error i IDENTYCZNEGO SHA256 ostatniej odpowiedzi. Jeśli zmiana stanu: nie wysyłać.
- SOURCE version 1.65.0. Node: 12/12 suites PASS, w tym nowe lite-high-slider 4 PASS, lite-resume-dil-dom 4 PASS, dotychczasowe high-guard 14 PASS i resume-agent 7 PASS. C# Debug build zero błędów, znane MSB3277. Gate check-lite-p0-promotion.ps1 dodał oba nowe JS suites, PASS.

## Rozróżnienia i ograniczenia
- Luna API PBOX1 PASS; testowy czat realny ma High; SOURCE guard/DOM PASS w testach. **Nie stwierdzono jeszcze, że v1.65.0 STAGED/LIVE WebView2 wykonał prawdziwy cykl automatycznego wznowienia**, gdyż aktywne okno użytkownika jest v1.64.0; nie zamykać podczas pracy.
- Weryfikacja suwaka medium→high była syntetyczna na DOM fixture; nie obniżano na siłę istniejącego High w prywatnym czacie, aby testować.
- Na razie brak dowodu, że backend zrealizował utracone odpowiedzi ChatGPT, nie ponawiać automatycznie.
- Wydanie side-by-side PBOX1 v1.65.0 po Autosync + testy + manifest, bez promocji LIVE podczas pracy bieżącego czatu. Rzeczywisty E2E Lite po bezpiecznej promocji.

## Następny krok
1. Sprawdzić gate dokumentacji, Autosync techniczne, PBOX1 staged v1.65.0 i selftesty.
2. Utrzymywać zgłoszony test High+Luna+książka bez nowych zadań ChatGPT Reminder. Dopuszczalne pojedyncze zadanie OpenFill do realnego interfejsu, jasno odróżnić je od E2E Lite.
3. Do czasu LIVE wprowadzania zmian, dokumentować wszystkie braki jako SOURCE/STAGED i nie zaliczać punktu 4 jako ukończonego.