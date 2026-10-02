# ADR 0004 – UIA-Patterns statt simulierter Eingabe

**Status:** angenommen (2026-10-02)

**Kontext:** Der Agent arbeitet oft auf demselben Desktop wie der Mensch. SendInput stiehlt Fokus und Maus.

**Entscheidung:** `click`, `fill`, `check`, `select`, `expand` nutzen ausschließlich Patterns. Fehlt ein Pattern, gibt es Exit 1 mit Hinweis auf `--input`. Echte Eingabe nur explizit (`--input`, `press`, `type`, `dblclick`).

**Konsequenzen:** Vorhersehbar und störungsfrei; Controls ohne Pattern brauchen den bewussten Umweg über `--input`.
