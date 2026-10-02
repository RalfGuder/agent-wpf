# ADR 0003 – Daemon pro Session über Named Pipe

**Status:** angenommen (2026-10-02)

**Kontext:** Refs, UIA-Verbindung und Event-Abos müssen zwischen CLI-Aufrufen erhalten bleiben; neu Anhängen kostet 200–500 ms pro Aufruf.

**Entscheidung:** Ein Daemon pro Session, gestartet vom ersten Befehl (`UseShellExecute`, damit er keine Konsolen-Handles erbt), beendet nach 1 h Leerlauf. Transport: Named Pipe mit `CurrentUserOnly`, eine Anfrage pro Verbindung als JSON-Line. `FirstPipeInstance` verhindert zwei Daemons pro Session; eine zweite Instanz lauscht schon während eine Anfrage läuft.

**Konsequenzen:** Anfragen werden seriell bearbeitet; ein langes `wait` blockiert andere Befehle derselben Session (Client wartet bis 10 min).
