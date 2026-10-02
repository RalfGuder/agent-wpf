# agent-wpf

Desktop automation CLI for AI agents on Windows: operate WPF, WinForms and Win32 applications through
UI Automation the way [agent-browser](https://github.com/vercel-labs/agent-browser) operates web pages.

```
agent-wpf open "C:\Program Files\MyApp\MyApp.exe"
agent-wpf snapshot -i
# - window "MyApp" [ref=e1]
#   - edit "Name" [ref=e7] [id=txtName] value=""
#   - button "Speichern" [ref=e8] [id=btnSave]
agent-wpf fill @e7 "Ralf"
agent-wpf click @e8
agent-wpf wait --window "Gespeichert"
agent-wpf close
```

- **Snapshot with refs:** the UI Automation tree as compact text, `@eN` refs stay stable while the element lives.
- **No mouse by default:** actions use UIA patterns (Invoke, Value, Toggle, SelectionItem, ExpandCollapse); `--input` switches to the real mouse and keyboard.
- **Desktop specifics:** modal dialogs first, popups (menus, drop-downs), virtualized grids via `table`, screenshots of covered windows.
- **Safe by default:** `open` trusts the app it starts; `attach` needs an allowlist (`--allow-process`), elevated apps are reported instead of escalated.
- **Agent-friendly:** exit codes with `hint:` lines, `--json` envelope on every command, per-session daemon over a user-only named pipe.

## Install

Download `agent-wpf-<version>-win-x64.zip` from the releases, put `agent-wpf.exe` on `PATH`.
For Claude Code, copy `skills/agent-wpf` into your skills folder; the stub loads the full guide from the
CLI with `agent-wpf skills get core`.

## Build

```
dotnet build AgentWpf.slnx
dotnet test AgentWpf.slnx --filter "TestCategory!=E2E"   # unit tests
dotnet test tests/AgentWpf.E2E                            # E2E, needs an interactive desktop
```

See [docs/spec.md](docs/spec.md) for the design and [docs/adr](docs/adr) for the key decisions.

## License

Apache-2.0
