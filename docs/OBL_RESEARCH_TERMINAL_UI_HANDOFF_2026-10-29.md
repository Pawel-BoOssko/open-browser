# OBL — research: utrata kontekstu czatu po terminalnym błędzie UI (29.10.2026)

Status: RESEARCH/DO DYSKUSJI. Bez implementacji, bez zmiany harmonogramu. Główny cel projektu: niezawodne wybudzanie P0.

## Fakty od właściciela — traktować jako potwierdzone zachowanie produkcyjne OBL

Dokładna obserwowana sekwencja:
1. Czerwony komunikat o maksymalnej długości konwersacji pozostaje na ekranie z widoczną odpowiedzią.
2. Nowe polecenie może zostać wykonane, ale jego odpowiedź może nie pojawić się poprawnie; pojawia się pełnoekranowe „Coś poszło nie tak”.
3. Po odświeżeniu interfejs może cofnąć widoczną historię o 3–5 wymian i pokazać stan „Połączenie zostało przerwane. Oczekiwanie na pełną odpowiedź”.
W OBL traktować etap 1 jako wyzwalacz awaryjnego handoffu. Nie odświeżać automatycznie po czerwonym komunikacie.

## Oryginalne zrzuty odnalezione na SATANAMD

Folder: C:\Users\pboru\OneDrive\Pictures\Screenshots\
- Zrzut ekranu 2026-10-27 051332.png — czerwony baner: „Osiągnięto maksymalną długość dla tej konwersacji, możesz jednak dalej rozmawiać, rozpoczynając nowy czat”. Widać także „Rozpocznij nowy czat”.
- Zrzut ekranu 2026-10-27 190935.png — pełnoekranowy „ChatGPT napotkał problem”; „Coś poszło nie tak. Aby kontynuować, spróbuj ponownie”; przycisk „Spróbuj ponownie”.
- Zrzut ekranu 2026-10-28 130541.png, 2026-10-28 141822.png, 2026-10-28 174632.png — ten sam pełnoekranowy wariant awarii.
- Zrzut ekranu 2026-10-28 174404.png, 2026-10-28 175237.png — czerwony terminalny baner z widoczną rozmową.
- Zrzut ekranu 2026-10-28 174738.png — czat z aktywnym generowaniem oraz komunikatem „Połączenie zostało przerwane. Oczekiwanie na pełną odpowiedź”.
- Zrzut ekranu 2026-10-28 175237.png zawiera także opis właściciela: czerwony komunikat, potem „coś poszło nie tak”, następnie „połączenie zostało przerwane” i utrata części kontekstu.

W wytypowanych standardowych folderach lokalnych Pictures, Desktop, Documents, Downloads i pakiecie ScreenSketch nie znaleziono innych ostatnich zapisanych PNG/JPG. Inne narzędzie zrzutów może zapisywać do schowka zamiast tworzyć plik. W tym folderze OneDrive znaleziono 17 plików obrazów z 25–28.10.

## Publiczne potwierdzenia rodzaju problemów (nie dowód całej sekwencji w innych kontach)

1. OpenAI Help: https://help.openai.com/en/articles/7996703-troubleshooting-chatgpt-error-messages — „Something went wrong” to ogólny błąd; długie rozmowy i utrudnione ładowanie; w zwykłym rozwiązywaniu problemów zalecany nowy czat/refresh. OBL ma osobny wymóg ratowania stanu PRZED odświeżeniem.
2. OpenAI Community (16.04.2026): https://community.openai.com/t/regression-long-conversations-that-previously-loaded-are-now-completely-inaccessible-for-many-users/1379132 — opisy niedostępnych długich/maksymalnych rozmów.
3. OpenAI Community (02.04.2026): https://community.openai.com/t/critical-bug-persistent-chat-loops-failure-to-save-history-and-unable-to-load-conversation-errors/1378372 — relacja o nieutrwalonych najnowszych wiadomościach i zniknięciu ostatnich wymian po refresh; analogia, nie dowód przyczyny OBL.
4. OpenAI Help: https://help.openai.com/en/articles/8809935-deleting-and-archiving-chats-in-chatgpt — w ogólnych przypadkach brakujących rozmów dostępne są mechanizmy wyszukiwania i eksportu; nie są częścią automatycznego odzyskiwania OBL.

## Audyt obecnego SOURCE v1.73.61

- src/BridgeBrowserAlpha0/LiteResponseSignal.js: czerwony komunikat wykrywany po frazach PL/EN w terminalnym banerze/kontrolce nowego czatu; pełnoekranowy błąd po „ChatGPT napotkał problem” + „Spróbuj ponownie”. Nie jest to otwarty katalog; dokładny tekst i struktura mogą się zmieniać.
- LiteChatWakePreflight.cs wymaga dwóch obserwacji failureKind=conversation_length_exceeded.
- LiteResponseDiagnostics.cs: po utrzymywaniu się terminalnego błędu 18 sekund wywołuje QuarantineTerminalConversation; zapisuje lokalny snapshot przez LiteTerminalHandoffSnapshot.Save, następnie WYŁĄCZA dotychczasowy harmonogram i czeka na sukcesję. Zapis snapshotu przez silnik nie jest tym samym, co pełny handoff sporządzony przez czat.
- LiteChatWakeUi.cs przy terminalnym preflight również przechodzi do QuarantineTerminalConversation i NIE wysyła specjalnego polecenia handoffu do czatu. Obecna logika nie odpowiada propozycji właściciela.
- LiteWakeRotationUi.cs: obecnie uruchamia tworzenie następcy przy próbie wake kończącego się na 0, ZANIM wykona zwykły preflight oczekiwania, co koliduje z najnowszą decyzją: rotacja ma nastąpić dopiero gdy normalny silnik uzna wysyłkę kolejnego wake za dozwoloną. Jest również potencjalna usterka: po promocji nowego leada przy starej sekwencji ...9 zerowana jest RotationPhase, więc następny tick ponownie widzi granicę 9->0. Wymaga rozwiązania w uzgodnionym planie; NIE poprawiać bez dyskusji.
- LiteChatHandoff.cs: ścieżka bieżąca docs/OPENBROWSER_LITE_AUTO_HANDOFF_CURRENT.md, zwykły handoff tylko przy wybudzeniach kończących się na 9. Tryb terminalnego wyjątku P0-08-06 jest otwarty w Beads.
- Trzeci ekran „Połączenie zostało przerwane” nie ma równorzędnego, bezpośredniego pełnego testu E2E; nie wolno uznawać ogólnego no_composer/response_error za dokładną identyfikację tego ekranu.

## Propozycja procedury awaryjnej właściciela — do doprecyzowania

1. Wykryj pierwszy terminalny czerwony baner. Zapisz datę/godzinę, URL starego leada i numer ostatniego realnego wybudzenia, identyfikator incydentu; nie odświeżaj strony.
2. Wyślij dokładnie jedno specjalne polecenie utworzenia pełnego HANDOFF NA DYSKU. Nie jest to numerowane wybudzenie; nie resetuje ciągłej numeracji projektu i nie znosi obowiązujących zasad Beads/STOP.
3. Mierz czas od zlecenia handoffu i stan pliku przez maksymalnie 30 minut. Nie opieraj się na widoczności odpowiedzi w chat UI.
4. Aby nie pomylić starego pliku z nowym: każdorazowo wpisz do handoffu projekt, URL starego czatu, numer ostatniego wake, identyfikator incydentu, rzeczywistą datę ukończenia, LIVE/SOURCE, zadania Beads, ostatnią wykonaną pracę, blokady i następny krok. Zapisz atomowo i odczytaj ponownie. Wystarczy jeden aktualny plik plus nazwane historyczne snapshoty bez indeksu/JSON/ZIP; bieżący dokument jest jedynym polecanym do odczytu.
5. Po upływie 30 minut, niezależnie od wyniku pliku, silnik przechodzi do normalnej rotacji: nowy czat jako jedyny lead, nie dawny. Nowemu chatowi przekazuje JAWNY komunikat „HANDOFF ZWERYFIKOWANY” + dokładna ścieżka / „HANDOFF NIE POWSTAŁ” + recovery z Beads/aktywnego checkpointu. Nowy chat NIE czyta dawnych handoffów automatycznie.
6. Zwykłe wake ...9/...0 utrzymują osobną uzgodnioną zasadę: 9 tworzy handoff, 0 nowy czat w momencie, w którym istniejący scheduler podjąłby decyzję o wysyłce następnego wake. Tryb terminalny jest wcześniejszym wyjątkiem.

Otwarte pytanie dla właściciela: jeśli nowy, prawidłowy handoff pojawi się po 3–5 minutach, czy czekamy pełne 30 minut przed rotacją, czy przechodzimy przy najbliższym kwalifikującym się terminie? Odpowiedź zmienia implementację. Bez akceptacji nie wdrażać.

## Granice

Nie zmieniono SOURCE, LIVE ani harmonogramu w ramach tego researchu. Harmonogram właściciela był zatrzymany: Enabled=false, WakeSequence=49. Priorytet: do dyskusji przed implementacją.

## KOREKTA: ZATWIERDZONE USTALENIA WŁAŚCICIELA 2026-10-29
Powyższe propozycje nienumerowanego komunikatu i oczekiwania zawsze pełne 30 minut są NIEAKTUALNE. Właściwe: awaryjne zlecenie handoffu posiada N.1 z pełnym ID i timestampem, rotacja N+1 NATYCHMIAST po nowym pasującym pliku (LastWriteTimeUtc >= czas polecenia), inaczej maksymalnie 30 min. Pełnoekranowy problem i zerwane połączenie -> natychmiast N+1 bez handoffu. Pierwsza wiadomość nowego leada JEST normalnym numerowanym N+1, żadnego wcześniejszego seed. Cała obowiązująca specyfikacja: docs/OBL_PLAN_WDROZENIA_WAKE_ZATWIERDZONY_2026-10-29.md.
