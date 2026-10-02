# Plan: agent-wpf — UIA-CLI für KI-Agenten (analog agent-browser)

## Context

`D:\source\repos\agent-browser` (Klon vercel-labs, Rust, Apache-2.0) lässt KI-Agenten Websites über Accessibility-Snapshot + `@eN`-Refs bedienen. Ziel: gleiches Bedienmodell für **Windows-Desktop-Apps (WPF zuerst, WinForms/Win32 gratis über UIA)** — beliebige fremde Apps, ohne Eingriff. Ergebnis der Grill-Session (Q1–Q27, alle wie empfohlen) unten festgehalten. Repo existiert noch nicht; .NET SDK 10.0.401 ist installiert, NuGet global-packages auf `D:\packages`.

## Entscheidungen (Grill-Session 2026-10-02)

| # | Thema | Entscheidung |
|---|---|---|
| Q1 | Nutzer | KI-Agent; tokensparende Ausgabe |
| Q2 | Ziel-Apps | beliebige fremde Apps, keine Eingriffe |
| Q3 | Zugriff | UIA3 von außen; Snoop-Injektion später optional |
| Q4 | Ort | eigenes Repo `D:\source\repos\agent-wpf` |
| Q5 | MVP | open/attach, snapshot+Refs, click/fill/select/check/keys, screenshot, wait, Werte/DataGrid lesen, Mehrfenster |
| Q6 | Syntax | agent-browser-Verben + Desktop-Verben (`attach`, `windows`, `expand`, `invoke`); Web-Verben entfallen |
| Q7 | Stack | C# .NET 10 + FlaUI (UIA3), self-contained Single-File, kein NativeAOT |
| Q8 | Daemon | ja, pro Session, Auto-Start, 1 h Idle-Timeout |
| Q9 | KI-Anbindung | SKILL.md-Stub + `agent-wpf skills get core` (eingebettet); MCP nach MVP |
| Q10 | Interaktion | nur UIA-Patterns; SendInput nur explizit `--input` |
| Q11 | Sicherheit | Allowlist; leer ⇒ nur selbst gestartete/explizit attachte Prozesse |
| Q12 | Transport | Named Pipe `agent-wpf-<user>-<session>`, ACL aktueller User, JSON-Lines |
| Q13 | Prozess | ein Exe, `agent-wpf daemon`-Rolle, nur x64 |
| Q14 | Refs | stabil über Snapshots solange RuntimeId lebt; sonst Fehler „stale ref“ |
| Q15 | Snapshot | `- button "OK" [ref=e3] [id=btnOk]`, Zustände nur bei Abweichung, Vordergrundfenster default, `--all-windows`, `-i -c -d -s`, `--verbose` für ClassName/Bounds |
| Q16 | Virtualisierung | nur realisierte Zeilen + Hinweis; `scroll`/`find --realize`; `table @eN --rows a-b [--json]` |
| Q17 | Regeln | TDD, Clean Architecture, XML-Doku, NASA-Asserts, NUnit/Moq/Shouldly, CPM. **Nicht**: Base64-Strings, resx |
| Q18 | Tests | eigene `AgentWpf.TestApp`; E2E `[Category("E2E")]`; Unit-Tests gegen gemockte Ports |
| Q19 | Distribution | GitHub, Apache-2.0, Release-Zip; winget später; kein npm |
| Q20 | Schichten | Domain → Application (Ports) → Infrastructure.FlaUI; Protocol; Exe als Composition Root |
| Q21 | Screenshot | `PrintWindow(PW_RENDERFULLCONTENT)`, Element-Crop via Bounds; `--annotate` später |
| Q22 | Lebenszyklus | `open` startet+attacht (auto-allowlisted); `attach --pid/--process/--title`; `close` schließt nur Eigenes, `--kill` optional; Attachte nur detachen |
| Q23 | wait | `@eN [--state]`, `--text`, `--window`, `--idle` (StructureChanged-Ruhe), `<ms>`; Default 10 s, `--timeout` |
| Q24 | Fehler | Exit 0/1/2/3 stale/4 timeout/5 allowlist/6 UIPI; stderr englisch mit `hint:`; `--json` = `{ok,data,error:{code,message,hint}}` |
| Q25 | Elevation | UIPI erkennen → Exit 6, keine Auto-Elevation |
| Q26 | Roadmap nach MVP | MCP → `--annotate` → `diff snapshot` → Datei-Dialoge → `batch` → Injektion → winget |
| Q27 | Doku | `docs/` im Repo (Spec, ADRs, Plan) |

## Repo-Struktur

```
agent-wpf/
  AgentWpf.slnx, Directory.Build.props (TreatWarningsAsErrors, Nullable, AnalysisLevel latest-all),
  Directory.Packages.props (CPM), .editorconfig, LICENSE (Apache-2.0), README.md
  src/AgentWpf.Domain/            Ref, RefId, ElementSnapshot, Role, ElementState, AllowlistPolicy, ErrorCode
  src/AgentWpf.Application/       UseCases (Snapshot, Click, Fill, Select, Check, Press, Wait, Screenshot, Table,
                                  Open, Attach, Close, Windows), Ports: IAutomationDriver, IProcessLauncher,
                                  IScreenCapture, IRefRegistry, IClock; SnapshotRenderer (Text)
  src/AgentWpf.Infrastructure.FlaUI/  FlaUiAutomationDriver, PrintWindowCapture, ProcessLauncher, UipiDetector
  src/AgentWpf.Protocol/          Request/Response-DTOs, JSON-Lines-Codec (System.Text.Json source-gen)
  src/AgentWpf/                   System.CommandLine-CLI, DaemonHost (NamedPipeServerStream + PipeSecurity),
                                  DaemonClient (Auto-Start, Pipe-Connect), SessionManager, Composition Root
  skills/agent-wpf/SKILL.md       Stub (allowed-tools: Bash(agent-wpf:*)) → `agent-wpf skills get core`
  skill-data/core/SKILL.md + references/  als EmbeddedResource in AgentWpf
  tests/AgentWpf.Domain.Tests, AgentWpf.Application.Tests (Moq), AgentWpf.Protocol.Tests
  tests/AgentWpf.TestApp/         WPF: Button, TextBox, ComboBox, CheckBox, virtualisiertes DataGrid,
                                  TreeView, Menü, MessageBox, zweites Fenster, verzögert erscheinendes Element
  tests/AgentWpf.E2E/             [Category("E2E")], startet TestApp über echtes Exe
  docs/spec.md, docs/adr/0001…, docs/plan.md
```

## Umsetzung (TDD-Inkremente, jeweils RED → GREEN → REFACTOR)

1. **Gerüst**: Repo, slnx, Props, CPM (NUnit, NUnit3TestAdapter, NUnit.Analyzers, Microsoft.NET.Test.Sdk, Moq, Shouldly, FlaUI.UIA3, System.CommandLine), LICENSE, `docs/spec.md` mit obiger Entscheidungstabelle, ADRs für Q3/Q7/Q8/Q10/Q14.
2. **Domain**: RefId-Vergabe, ElementState-Abweichungslogik, AllowlistPolicy, ErrorCode→Exit-Code.
3. **SnapshotRenderer**: Baum → Textformat (Q15) inkl. Filter `-i -c -d -s`, Virtualisierungshinweis; JSON-Variante.
4. **RefRegistry**: RuntimeId↔Ref, stabil über Snapshots, stale-Erkennung.
5. **Use Cases** gegen gemockte Ports: click/fill/select/check/expand/invoke (nur Patterns, Fehler bei fehlendem Pattern, `--input`-Pfad), wait (Polling + Event-Port, Timeout), table, open/attach/close mit Allowlist.
6. **Protocol + Pipe**: Codec-Tests; Daemon/Client mit In-Memory-Stream testen, dann NamedPipe mit PipeSecurity (current user).
7. **CLI**: System.CommandLine-Baum, `--session`, `--json`, Exit-Codes, Auto-Start des Daemons, Idle-Timeout.
8. **Infrastructure.FlaUI**: Driver-Adapter (ControlType→Role, Patterns, CacheRequest für schnellen Baum-Walk, Virtualized/ItemContainer), PrintWindow-Capture, UIPI-Erkennung (Integrity-Level-Vergleich).
9. **TestApp + E2E**: je MVP-Befehl ein E2E-Szenario.
10. **Skill**: `skill-data/core/SKILL.md` (Core-Loop: open → `snapshot -i` → Ref-Aktion → re-snapshot; Waits; Tabellen; Fehler-Hints), Stub, `skills get core [--full]`.
11. **Release**: `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true`, GitHub-Actions-Workflow (Build + Unit-Tests; E2E nur lokal/manuell).

Public-Methoden: ≥2 `Debug.Assert` (Pre/Post), XML-Doku englisch, Methoden ≤ ~60 Zeilen, Schleifen begrenzt (z. B. Baum-Walk mit Max-Tiefe/Max-Knoten).

## Verifikation

- `dotnet build AgentWpf.slnx` ohne Warnungen.
- `dotnet test --filter "TestCategory!=E2E"` grün.
- Lokal interaktiv: `dotnet test --filter TestCategory=E2E` gegen TestApp grün.
- Manueller Smoke-Durchlauf:
  ```
  agent-wpf open tests\AgentWpf.TestApp\bin\...\AgentWpf.TestApp.exe
  agent-wpf snapshot -i
  agent-wpf fill @e4 "hallo"; agent-wpf click @e3; agent-wpf wait --window "Dialog"
  agent-wpf table @e7 --rows 0-20 --json
  agent-wpf screenshot out.png
  agent-wpf close
  ```
- Fremd-App-Smoke: `attach --process notepad` ohne Allowlist → Exit 5; mit `--allow-process notepad` → Snapshot ok.
- Claude Code mit installiertem Skill: Agent bedient TestApp end-to-end allein über den Skill.
