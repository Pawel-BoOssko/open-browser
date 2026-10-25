# Open Browser Lite — operacyjny kontrakt agentów
Obowiązuje dokument docs/OPENBROWSER_LITE_OPERATING_RULES_2026-10-25.md.
Priorytet: **najpierw pracująca, sprawdzona wersja LIVE, potem następna iteracja**.
Cykl: POPRAWKA -> TESTY -> STAGED -> WDROŻENIE SATANAMD -> WERYFIKACJA -> DOGFOOD nowego LIVE.
OBL ma działać WYŁĄCZNIE na SATANAMD. PBOX1 całkowicie poza zakresem; w LC MultiHost ZAWSZE target=satamad i sprawdzenie COMPUTERNAME.
P0: niezawodne wybudzenie/wznowienie, jeden aktywny czat wykonawczy, brak duplikacji wysyłek, właściwy dziennik i ACK tylko na podstawie dowodów silnika.
Nie używać ChatGPT Tasks; bez ręcznego git commit/push/reset. Poprawki muszą trafiać do produkcyjnego OBL z zachowaniem odwracalnego rollbacku, aby kolejna praca korzystała z nowego narzędzia.
W razie NO-GO zapisać blokadę i konkretny krok odblokowania; testy offline nie są wdrożeniem.
