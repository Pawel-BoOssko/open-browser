# docs/runtime/

**Po co ten folder:** Dokumenty dla modelu LLM, który działa WEWNĄTRZ Open Browser. Model nie zna historii projektu ani kodu — potrzebuje tylko wiedzieć: kim jest, co może robić, jak wysyłać komendy.

**Stworzony przez:** Claude Code, 2026-05-18.

**Zasady:**
1. Wszystko w jednym pliku lub minimalnej liczbie plików — model nie ma kontekstu na wielostronicową dokumentację.
2. Tylko informacje potrzebne w runtime. Zero historii, zero architektury, zero kodu C#.
3. Format kopert i lista komend muszą być zawsze aktualne — to jest kontrakt między systemem a modelem.
