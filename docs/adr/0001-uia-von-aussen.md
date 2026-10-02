# ADR 0001 – UI Automation von außen statt Injektion

**Status:** angenommen (2026-10-02)

**Kontext:** agent-wpf soll beliebige fremde WPF-Apps bedienen, ohne sie zu verändern.

**Entscheidung:** Zugriff über UI Automation (UIA3) aus einem eigenen Prozess. Eine Snoop-artige Injektion (Visual Tree, Bindings, DataContext) kommt später als optionaler Diagnosemodus.

**Konsequenzen:** Funktioniert ohne Rechte und Bitness-Abhängigkeit, auch für WinForms/Win32. Sichtbar ist nur, was AutomationPeers liefern; eigene Controls ohne Peer bleiben `custom`/`pane`.
