---
name: agent-wpf
description: Desktop automation CLI for AI agents on Windows. Use when the user needs to operate a Windows desktop application (WPF, WinForms, Win32) - open an app, click buttons, fill text fields, select options, read values or data grids, handle dialogs, take screenshots, or test a desktop UI. Triggers include "open the app", "click in the dialog", "fill out the form in the desktop app", "read the grid", "test the WPF window", "automate the Windows application".
allowed-tools: Bash(agent-wpf:*)
---

# agent-wpf

This skill is a stub. The full guide ships with the installed CLI so that it always matches its version.

Load it before the first command:

```
agent-wpf skills get core
```

For the full command reference:

```
agent-wpf skills get core --full
```

Core loop: `agent-wpf open <exe>` → `agent-wpf snapshot -i` → act by ref (`click @e3`, `fill @e4 "text"`) → `snapshot -i` again.
