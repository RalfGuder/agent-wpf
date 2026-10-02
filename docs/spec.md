# agent-wpf – Spezifikation

Stand: 2026-10-02 (MVP 0.1.0). Ergebnis einer Grill-Session (Q1–Q27).

## Ziel

Ein KI-Agent bedient beliebige Windows-Desktop-Anwendungen (WPF zuerst, WinForms/Win32 über UIA gratis)
mit demselben Bedienmodell wie agent-browser: Snapshot → Ref → Aktion → Snapshot.

## Entscheidungen

| # | Thema | Entscheidung |
|---|---|---|
| Q1 | Nutzer | KI-Agent; tokensparende Ausgabe |
| Q2 | Ziel-Apps | beliebige fremde Apps, keine Eingriffe |
| Q3 | Zugriff | UIA3 von außen; Snoop-Injektion später optional ([ADR 0001](adr/0001-uia-von-aussen.md)) |
| Q4 | Ort | eigenes Repo |
| Q5 | MVP | open/attach, snapshot+Refs, click/fill/select/check/keys, screenshot, wait, Werte/DataGrid, Mehrfenster |
| Q6 | Syntax | agent-browser-Verben + Desktop-Verben (`attach`, `windows`, `window`, `expand`, `invoke`) |
| Q7 | Stack | C# .NET 10 + FlaUI 5 (UIA3), self-contained Single-File ([ADR 0002](adr/0002-csharp-flaui.md)) |
| Q8 | Daemon | pro Session, Auto-Start, 1 h Idle ([ADR 0003](adr/0003-daemon-named-pipe.md)) |
| Q9 | KI-Anbindung | SKILL.md-Stub + `skills get core`; MCP nach MVP |
| Q10 | Interaktion | nur UIA-Patterns; echte Eingabe nur mit `--input` bzw. `press`/`type`/`dblclick` ([ADR 0004](adr/0004-patterns-statt-sendinput.md)) |
| Q11 | Sicherheit | `open` vertraut dem gestarteten Prozess; `attach` nur mit Allowlist beim Session-Start |
| Q12 | Transport | Named Pipe `agent-wpf-<user>-<session>`, `CurrentUserOnly`, JSON-Lines |
| Q13 | Prozess | ein Exe, `daemon`-Rolle, x64 |
| Q14 | Refs | stabil über Snapshots, Nummern nie wiederverwendet, toter Ref → Exit 3 ([ADR 0005](adr/0005-stabile-refs.md)) |
| Q15 | Snapshot | `- role "name" [ref=eN] [id=…] value="…" [state]`, Zustände nur bei Abweichung |
| Q16 | Virtualisierung | Hinweis im Snapshot; `table @eN --rows a-b` |
| Q17 | Regeln | TDD, Clean Architecture, XML-Doku, NASA-Asserts, NUnit/Moq/Shouldly, CPM; keine Base64-Strings, kein resx |
| Q18 | Tests | Unit-Tests gegen gemockte Ports; E2E gegen `AgentWpf.TestApp` (`[Category("E2E")]`) |
| Q19 | Distribution | GitHub, Apache-2.0, Release-Zip; winget später |
| Q20 | Schichten | Domain → Application (Ports) → Infrastructure.FlaUI; Protocol; Exe als Composition Root |
| Q21 | Screenshot | `PrintWindow(PW_RENDERFULLCONTENT)`, Crop über Bounds; `--annotate` später |
| Q22 | Lebenszyklus | `close` schließt nur Eigenes (`--kill` optional), Attachte werden nur abgehängt |
| Q23 | wait | `@eN [--state]`, `--text`, `--window`, `--idle`, `<ms>`; Default 10 s |
| Q24 | Fehler | Exit 0/1/2/3/4/5/6, `hint:`-Zeile, `--json`-Hülle |
| Q25 | Elevation | erkennen → Exit 6, keine Auto-Elevation |
| Q26 | Roadmap | MCP → `--annotate` → `diff snapshot` → Datei-Dialoge → `batch` → Injektion → winget |
| Q27 | Doku | `docs/` im Repo |

## Präzisierungen während der Umsetzung

- **Daemon parst die Befehlszeile.** Die CLI schickt argv + Arbeitsverzeichnis; nur `--session`, `--allow-process`, `skills`, `session` und `--help` bleiben im Client.
- **Allowlist:** `--allow-process` und `AGENT_WPF_ALLOW_PROCESSES` gelten nur beim Start einer Session; danach ignoriert (Warnung). So kann sich der Agent nicht nachträglich selbst freischalten.
- **Ein Zielprozess pro Session**, beliebig viele Fenster.
- **Popups** (WPF-Menüs, Drop-downs) sind eigene Top-Level-Fenster; der Snapshot hängt sie unter `# popup` an.
- **Fenstertitel** kommen aus `GetWindowText`, weil WPF den UIA-Namen eines Fensters aus dem Inhalt ableitet.
- **Lebendprüfung:** WPF hält Peers geschlossener Fenster am Leben; vor jeder Aktion wird die Elternkette bis zum Desktop samt `IsWindow` geprüft, sonst Exit 3.
- **Cache-Requests** mit `AutomationElementMode.Full`, damit gecachte Elemente später ungecachte Patterns bedienen.

## Architektur

```
agent-wpf.exe (Client)  ──argv, cwd──▶  Named Pipe  ──▶  agent-wpf.exe daemon
                                                         CommandLineApp (System.CommandLine)
                                                         └─ SessionServices (Use Cases)
                                                            └─ Ports ◀─ Infrastructure.FlaUI (UIA3, PrintWindow, Process)
```

| Projekt | Inhalt |
|---|---|
| `AgentWpf.Domain` | RefId, Role, ElementState, ElementSnapshot, AllowlistPolicy, ErrorCode, AgentWpfException |
| `AgentWpf.Application` | Use Cases, Ports, SnapshotRenderer, RefRegistry, AgentSession |
| `AgentWpf.Infrastructure.FlaUI` | FlaUiAutomationDriver, PrintWindowCapture, PngEncoder, ProcessLauncher, KeyChord, IntegrityLevel |
| `AgentWpf.Protocol` | DaemonRequest/Response, JSON-Lines-Codec, Pipe-Namen |
| `AgentWpf` | CLI, Daemon-Host/-Client, SkillCatalog, Composition Root |

## Offene Punkte nach dem MVP

- `snapshot -i` zeigt in DataGrids jede realisierte Zeile samt Row-Header – für große Grids zu laut; Zeilen eventuell standardmäßig zusammenfassen.
- Datei-Dialoge (Common Item Dialog) und MessageBox-Spezialfälle sind nicht gesondert behandelt.
- Siehe Roadmap Q26.
