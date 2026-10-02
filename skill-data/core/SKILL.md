---
name: agent-wpf-core
description: Core guide for driving Windows desktop apps (WPF, WinForms, Win32) with the agent-wpf CLI through UI Automation.
---

# agent-wpf

`agent-wpf` lets you operate Windows desktop applications the way `agent-browser` operates web pages:
take a snapshot of the UI Automation tree, act on elements by ref (`@e3`), take a new snapshot.

## Core loop

```
agent-wpf open "C:\Path\App.exe"        # start the app, wait for its first window
agent-wpf snapshot -i                    # actionable elements with refs
agent-wpf fill @e4 "C:\data\in.xml"      # act by ref
agent-wpf click @e7
agent-wpf wait --window "Ergebnis"       # wait for the next dialog
agent-wpf snapshot -i                    # always re-snapshot after the UI changed
agent-wpf close
```

Rules of thumb:

1. **Snapshot before acting.** Refs come from the latest snapshot; never guess them.
2. **Re-snapshot after every change** that may alter the UI (click, fill, select, new window).
3. **Prefer `snapshot -i`** (interactive only). Use `-c` for compact structure, `-s @eN` for a subtree, `-d N` to limit depth.
4. **Wait instead of sleeping:** `wait @e3`, `wait --text "Fertig"`, `wait --window "Speichern"`, `wait --idle`. Use `wait <ms>` only as a last resort.
5. Exit codes tell you what happened (see below); read the `hint:` line on stderr.

## Snapshot format

```
# 2 windows; showing "Fehler" (modal). Run windows to list them.
- window "Fehler" [ref=e9] [id=ErrorDialog]
  - text "Datei nicht gefunden" [ref=e10]
  - button "OK" [ref=e11] [id=btnOk]
```

- `role "name" [ref=eN]`, then `[id=AutomationId]` when the developer set one.
- `value="…"` for text fields, combo boxes, sliders. Passwords show `***`.
- States appear only when they deviate: `[disabled]`, `[checked]`, `[indeterminate]`, `[expanded]`, `[collapsed]`, `[selected]`, `[offscreen]`, `[readonly]`, `[focused]`.
- `(+N children)` means `-d` cut the tree; `(N more items, virtualized; use table or scroll)` means a list or grid only realized the visible rows.
- A modal dialog is shown automatically; the line starting with `#` tells you other windows exist.
- Open menus and drop-downs appear after `# popup`.

Refs stay valid across snapshots while the element exists. A vanished element gives exit code 3 (`stale ref`): take a new snapshot.

## Acting on elements

| Command | Does |
|---|---|
| `click @e3` | Invoke (button, menu item), toggle (check box), select (list/tab item) or expand/collapse — whatever the element supports |
| `fill @e4 "text"` | Replace the value of a text field |
| `select @e5 "Option"` | Pick an option of a combo box or list by its text |
| `check @e6` / `uncheck @e6` | Set a check box; `check` also selects a radio button |
| `expand @e7` / `collapse @e7` | Tree items, combo boxes, menus |
| `invoke @e8` | InvokePattern only |
| `focus @e9` | Keyboard focus |
| `scroll @e10 down --pages 2` / `scrollintoview @e11` | Scroll containers or realize an item |
| `press Enter` / `press Control+S --target @e4` | Real keyboard |
| `type @e4 "text"` | Type with the real keyboard |
| `dblclick @e3` | Real mouse double click |

By default agent-wpf acts through **UI Automation patterns**: no mouse movement, no focus stealing, works on covered windows.
When an element supports no pattern (exit 1, hint mentions `--input`), retry with the real mouse/keyboard:
`click @e3 --input`, `click @e3 --input --right`, `fill @e4 "x" --input`. Real input brings the window to the foreground.

## Reading data

```
agent-wpf get text @e10          # the element's name
agent-wpf get value @e4          # the element's value
agent-wpf table @e12 --rows 0-49 # grid rows: "[index] cell | cell", realizes virtualized rows
agent-wpf table @e12 --rows 50-99 --json
agent-wpf screenshot             # PNG of the active window (temp file), works when covered
agent-wpf screenshot out.png --element @e12
```

## Windows and dialogs

```
agent-wpf windows                # all windows of the app with refs, [modal] [active] [popup]
agent-wpf window @e20            # make a window the default for snapshot/screenshot
agent-wpf wait --window "^Speichern" --timeout 20000
agent-wpf snapshot --all-windows
```

## Attaching to running apps

`open` trusts the app it starts. Attaching to an already running app requires the user to allow it when the session starts:

```
agent-wpf --allow-process notepad attach --process notepad
agent-wpf attach --pid 1234
agent-wpf attach --title "^Converter"
```

Without that, `attach` fails with exit 5 (`not-allowed`). Ask the user instead of working around it.
Apps running as administrator cannot be automated from a normal shell (exit 6, `access-denied`).

## Sessions

Each session has its own daemon, target app and refs. Default session: `default`.

```
agent-wpf --session review open app.exe
agent-wpf --session review snapshot -i
agent-wpf session list
agent-wpf session stop --session review
```

The daemon starts on the first command and stops after one idle hour.

## Exit codes and JSON

| Code | Meaning | Typical next step |
|---|---|---|
| 0 | ok | |
| 1 | action failed | read `hint:`; maybe `--input` or another element |
| 2 | invalid command | check syntax with `--help` |
| 3 | stale ref | `snapshot` again |
| 4 | timeout | snapshot to see the state, or raise `--timeout` |
| 5 | process not allowed | ask the user to allow it |
| 6 | access denied (elevated app) | ask the user |

Add `--json` to any command for `{"ok": true, "data": …, "error": null}` or
`{"ok": false, "data": null, "error": {"code": "stale-ref", "message": "…", "hint": "…"}}`.

Run `agent-wpf skills get core --full` for the full command reference.
