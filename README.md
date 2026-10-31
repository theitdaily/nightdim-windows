# NightDim — Windows Screen Dimmer (Brightness Below Minimum)

**NightDim** is a free, open-source **screen dimmer for Windows 10 and 11**. It makes your laptop or monitor look darker than the minimum brightness setting in Windows — useful for reading, working, or browsing at night.

**Languages:** [English](README.md) · [Русский](README_RU.md) · [Српски (ћирилица)](README_RS.md)

> **One-file installer.** The release contains `NightDimSetup.exe`, which installs NightDim to a folder you choose. The app supports **English, Russian and Serbian Cyrillic**.

## Download and install

1. Open the [latest GitHub release](../../releases/latest).
2. Under **Assets**, download **`NightDimSetup.exe`** (not the auto-generated **Source code** archives).
3. Run the setup wizard, accept the license agreement, choose a folder (default: `Program Files (x86)\TheITDaily\NightDim`), then select whether to **launch with Windows** and **create a Start menu shortcut**. Click **Install**.
4. On the final page, leave **Launch NightDim now** checked to start immediately, or start it later from the Start menu.

Windows may request administrator permission when **installing or uninstalling files in a protected folder**. Startup and Start menu shortcuts are created for the current Windows user. Windows 10/11 and .NET Framework 4.x are required. The original portable `NightDim.exe` can still be built from source.

## Features

- **Extra screen dimming (0–95%)** using a dark overlay, below Windows' usual minimum brightness.
- **30-second safety confirmation** only when a change makes the screen darker. The confirmation opens about **0.7 seconds after you finish moving the slider**, not while dragging it. If you do not confirm, NightDim restores the previous settings automatically.
- **Automatic pause:** no dimming from **08:00 to 20:00** by default. Change those times or disable the schedule.
- **Three interface languages:** English, Russian, and Serbian (Cyrillic). Language choice is saved automatically and does not need a dimming confirmation.
- **Windows integration:** optional per-user startup and a shortcut in the Start menu. Both can also be changed later in NightDim settings. Autostart launches the app in the system tray.
- **Manual update check:** click **Check for updates** to compare your version with the latest published GitHub Release. If a newer release exists, you can open its download page. No silent downloads or automatic installation.
- **System tray controls:** the app can hide near the Windows clock.
- **Multiple monitors** and keyboard shortcuts.
- **Experimental cursor dimming:** attempts to darken the mouse pointer using graphics-card gamma control; results depend on Windows, the display, and your driver.

## How to use it

1. Use the **Language** menu at the top of the settings window to choose **English**, **Русский**, or **Српски (ћирилица)**. The selection is saved immediately.
2. Drag the dimming slider and release it. **If the screen gets darker**, the confirmation window appears after about **0.7 seconds**; the 30-second safety countdown runs from your **last change**. If the screen gets lighter or its visible brightness stays unchanged (for example, during a scheduled pause), no confirmation is needed.
3. When prompted, click **Keep changes** within **30 seconds**, or click **Revert (Esc)** to undo the change. If you do nothing, the previous confirmed dimming settings return automatically. The confirmation window intentionally stays bright so you can find it.
4. For the daytime break, tick **Pause dimming on a schedule** and choose start/end times (default **08:00–20:00**). Untick it for dimming all day.
5. Close the settings window to hide NightDim in the tray. Use the tray icon to reopen settings or exit. The **Exit** button closes NightDim completely.

6. Toggle **Launch with Windows** and **Add shortcut to Start menu** at any time in the settings window. These options do not change screen brightness.
7. Click **Check for updates** whenever you want to look for a new release. An internet connection is required.

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
- The installer and app are currently **unsigned**. Windows SmartScreen or antivirus software may show a warning. Download only from this repository's Releases page.

Confirmed settings are saved in `%APPDATA%\NightDim\settings.txt`. Delete that file while NightDim is closed to reset to defaults.

## Build from source

You need Windows and the **.NET Framework 4.x C# compiler** (`csc.exe`). Keep `NightDim.ico`, `NightDim.cs`, `ReleaseChecker.cs`, `NightDim.Setup.cs`, `WindowsIntegration.cs`, `UiText.cs`, `AssemblyInfo.cs`, `EULA.txt` and `LICENSE` together.

1. Download the source through **Code → Download ZIP** and extract it.
2. Run **`Build_NightDim.bat`** to create the portable `NightDim.exe` in the project folder.
3. Run **`Build_Release.bat`** to compile the app and the self-extracting installer. The upload-ready file is `dist/<version>/NightDimSetup.exe`.

The setup wizard displays an [end-user notice and MIT license](EULA.txt), including an **“as-is” warranty disclaimer and limitation of liability**. Its text is also saved in the installation directory as `EULA.txt`, alongside `LICENSE.txt`. These disclaimers are subject to applicable law.

## Update or reinstall

Download the newer `NightDimSetup.exe` from GitHub Releases and run it over the existing installation. The setup wizard detects the installed version and selects the current folder. It closes the running NightDim gracefully before replacing the application, keeps your preferences, and remembers your startup and Start menu choices. The final page offers **Launch NightDim now**. Running the same installer again repairs/reinstalls that version; installing an older version over a newer one is blocked. To change the installation folder, uninstall first.

The update checker does **not** automatically download or execute installers. GitHub must have a published release with a newer version number for a notification to appear.

## Uninstall

Open **Windows Settings → Apps → Installed apps** (Windows 11) or **Apps & features** (Windows 10), find **NightDim**, and choose **Uninstall**. You can also run `uninstall.exe` from the installation folder.

NightDim will close safely, remove its application files, current-user startup entry, Start menu shortcut, and Windows uninstall entry. If it was installed in **Program Files (x86)**, Windows may request administrator permission to remove the application files. Your preferences in `%APPDATA%\NightDim\settings.txt` are **kept** for future installations. Other files you placed in the installation directory will not be deleted.

The portable `NightDim.exe` (built without the installer) does not appear in Windows Installed apps.


## License

[MIT](LICENSE) — free to use, modify and distribute while keeping the license notice.
