# Command reference

Global options (any position): `--session <name>` (or `AGENT_WPF_SESSION`), `--allow-process <name>` (repeatable, only when the session starts; also `AGENT_WPF_ALLOW_PROCESSES=a;b`), `--json`.

## Lifecycle

| Command | Options |
|---|---|
| `open <exe> [-- args…]` | `--timeout <ms>` (default 30000) for the first window |
| `attach` | exactly one of `--pid <id>`, `--process <name>`, `--title <regex>` |
| `close` | `--kill` terminates the app if it does not exit within 5 s |
| `windows` | |
| `window <ref>` | |

## Snapshot

`snapshot [-i|--interactive] [-c|--compact] [-d|--depth N] [-s|--scope <ref>] [--all-windows] [--verbose]`

## Interaction

| Command | Notes |
|---|---|
| `click <ref> [--input] [--right]` | `--right` needs `--input` |
| `dblclick <ref>` | real mouse |
| `invoke <ref>` | |
| `fill <ref> <text> [--input]` | `--input` focuses, selects all, types |
| `type <ref> <text>` | real keyboard |
| `press <keys> [--target <ref>]` | `Enter`, `Escape`, `Tab`, `Backspace`, `Delete`, `Home`, `End`, `PageUp`, `PageDown`, arrows, `Space`, `F1`–`F24`, letters, digits; modifiers `Control`/`Ctrl`, `Shift`, `Alt`, `Win` joined with `+` |
| `select <ref> <option>` | |
| `check <ref>` / `uncheck <ref>` | |
| `expand <ref>` / `collapse <ref>` | |
| `focus <ref>` | |
| `scroll <ref> [up\|down\|left\|right] [--pages N]` | without direction: scroll into view |
| `scrollintoview <ref>` | |

## Reading

| Command | Notes |
|---|---|
| `get text <ref>` | |
| `get value <ref>` | |
| `table <ref> [--rows a-b]` | default `0-49`, at most 500 rows per call |
| `screenshot [path] [--element <ref>]` | relative paths resolve against the current directory |

## Waiting

`wait <ref> [--state visible|hidden|enabled|disabled]`, `wait <ms>`, `wait --text <text>`, `wait --window <regex>`, `wait --idle [ms]` — all with `--timeout <ms>` (default 10000).

## Sessions and guides

`session list`, `session stop`, `skills list`, `skills get <name> [--full]`, `--help`, `--version`.
