# ADR 0005 – Stabile Refs, nie wiederverwendet

**Status:** angenommen (2026-10-02)

**Kontext:** Ein Ref, der nach einer UI-Änderung still ein anderes Element trifft, ist gefährlicher als ein Fehler.

**Entscheidung:** Refs hängen an der UIA-RuntimeId und bleiben über Snapshots gleich. Nummern werden nie wiederverwendet. Vor jeder Aktion prüft der Driver, ob das Element noch an einem lebenden Fenster hängt; sonst Exit 3 `stale ref`.

**Konsequenzen:** Ein zusätzlicher Elternketten-Durchlauf pro Aktion (~ms). Kein automatisches Wiederfinden über AutomationId.
