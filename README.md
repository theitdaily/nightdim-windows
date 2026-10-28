# NightDim — Windows Screen Dimmer (Brightness Below Minimum)

**NightDim** is a free, open-source **screen dimmer for Windows 10 and 11**. It makes your laptop or monitor look darker than the minimum brightness setting in Windows — useful for reading, working, or browsing at night.

**Languages:** [English](README.md) · [Русский](README_RU.md) · [Српски (ћирилица)](README_RS.md)

> **No installer required.** NightDim is a small portable Windows `.exe` application. The app interface is available in **English, Russian, and Serbian Cyrillic**. You can change the language at any time in the settings window.

## Download and start (3 steps)

1. Open the [latest GitHub release](../../releases/latest).
2. Under **Assets**, download **`NightDim.exe`** (not **Source code (zip)** or **Source code (tar.gz)**).
3. Double-click the downloaded **`NightDim.exe`** to start NightDim. No extraction or installation is needed.

Requires **Windows 10/11 and .NET Framework 4.x**. Most Windows 10/11 computers already have a compatible .NET Framework installed. If you are viewing source code instead of a release, see [Build from source](#build-from-source).

## Features

- **Extra screen dimming (0–95%)** using a dark overlay, below Windows' usual minimum brightness.
- **30-second safety confirmation** only when a change makes the screen darker. The confirmation opens about **0.7 seconds after you finish moving the slider**, not while dragging it. If you do not confirm, NightDim restores the previous settings automatically.
- **Automatic pause:** no dimming from **08:00 to 20:00** by default. Change those times or disable the schedule.
- **Three interface languages:** English, Russian, and Serbian (Cyrillic). Language choice is saved automatically and does not need a dimming confirmation.
- **System tray controls:** the app can hide near the Windows clock.
- **Multiple monitors** and keyboard shortcuts.
- **Experimental cursor dimming:** attempts to darken the mouse pointer using graphics-card gamma control; results depend on Windows, the display, and your driver.

## How to use it

1. Use the **Language** menu at the top of the settings window to choose **English**, **Русский**, or **Српски (ћирилица)**. The selection is saved immediately.
2. Drag the dimming slider and release it. **If the screen gets darker**, the confirmation window appears after about **0.7 seconds**; the 30-second safety countdown runs from your **last change**. If the screen gets lighter or its visible brightness stays unchanged (for example, during a scheduled pause), no confirmation is needed.
3. When prompted, click **Keep changes** within **30 seconds**, or click **Revert (Esc)** to undo the change. If you do nothing, the previous confirmed dimming settings return automatically. The confirmation window intentionally stays bright so you can find it.
4. For the daytime break, tick **Pause dimming on a schedule** and choose start/end times (default **08:00–20:00**). Untick it for dimming all day.
5. Close the settings window to hide NightDim in the tray. Use the tray icon to reopen settings or exit. The **Exit** button closes NightDim completely.

**Important:** The app interface itself, taskbar and tray are darkened by the overlay when dimming is active. The confirmation window is the safety exception. Cursor dimming is optional and may not work on some computers.

### Emergency reset and shortcuts

| Keys | Action |
| --- | --- |
| `Ctrl + Alt + ↓` | Make the screen 5% darker (confirmation required) |
| `Ctrl + Alt + ↑` | Make the screen 5% lighter (no new confirmation required) |
| `Ctrl + Alt + F12` | Disable dimming immediately for the current session |
| `Esc` in the confirmation window | Discard the proposed change |

If the screen becomes too dark, use **`Ctrl + Alt + F12`**. If the hotkey is unavailable, open **Command Prompt** in the app folder and enter **`NightDim.exe --stop`** to close it safely. Do not force-kill the app.

## Important limitations

- NightDim reduces **perceived brightness** with software; it does **not** lower the monitor's physical backlight or guarantee power savings.
- It cannot promise to darken *everything*: secure Windows dialogs (including UAC), the sign-in screen, exclusive fullscreen applications, HDR content, and protected video may behave differently.
- **Mouse cursor dimming is experimental.** It uses graphics-card gamma control, which may conflict with HDR, Night light, ICC color profiles, or other color-management tools. If colors look wrong, turn cursor dimming off and exit NightDim normally so it can try to restore the previous gamma settings. Avoid forcibly killing the process.
- The hotkeys may not work if another app has already registered them.
- The `.exe` is currently **unsigned**. Windows SmartScreen or antivirus software may show a warning. Download `NightDim.exe` only from this repository's Releases page and check files before running them.
- NightDim does **not** start automatically with Windows.

Confirmed settings are saved in `%APPDATA%\NightDim\settings.txt`. Delete that file while NightDim is closed to reset to defaults.

## Build from source

You only need Windows and the **.NET Framework 4.x C# compiler** (`csc.exe`). Keep `NightDim.ico`, `NightDim.cs`, `UiText.cs`, and `AssemblyInfo.cs` in the same folder.

1. Download the repository source as a ZIP from **Code → Download ZIP** and extract it.
2. Double-click **`Build_NightDim.bat`**.
3. Double-click the new **`NightDim.exe`** to start the app.

## License

[MIT](LICENSE) — free to use, modify and distribute while keeping the license notice.
