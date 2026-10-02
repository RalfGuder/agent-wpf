# ADR 0002 – C# .NET 10 mit FlaUI

**Status:** angenommen (2026-10-02)

**Kontext:** agent-browser ist in Rust geschrieben. Für Windows-UIA ist das .NET-Ökosystem deutlich reifer.

**Entscheidung:** C# auf .NET 10 mit FlaUI 5 (UIA3). Auslieferung als self-contained Single-File mit Kompression (~50 MB), kein NativeAOT wegen COM-Interop.

**Konsequenzen:** Muster für DataGrid, ComboBox, Tree und Events stehen fertig zur Verfügung; die spätere Injektion bleibt in derselben Sprache. Das Binary ist größer als bei Rust.
