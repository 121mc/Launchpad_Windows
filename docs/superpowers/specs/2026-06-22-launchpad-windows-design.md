# Launchpad Windows Design

Date: 2026-06-22
Status: approved design, pending written-spec review

## Goal

Build a native Windows 11 launchpad inspired by macOS Launchpad. The app runs in the background, opens with a global hotkey, shows a full-screen frosted-glass launcher on the screen where the mouse is located, and lets the user start desktop items with one click. It automatically scans the current user's Desktop folder and also supports manually added shortcuts, files, and folders.

## Decisions

- Technology stack: native WPF / .NET single-process desktop app.
- Target OS: Windows 11.
- Desktop scan scope: current user's Desktop folder only.
- Global hotkey: `Ctrl + Alt + Space` by default.
- Multi-monitor behavior: open on the monitor containing the current mouse pointer.
- Item behavior: single-click launches the item and closes the launchpad.
- Sorting: drag-and-drop reorder with persisted order.
- Settings: first version includes a simple settings window.

## Non-Goals For The First Version

- Do not scan the public Desktop folder.
- Do not recursively scan subfolders on the Desktop.
- Do not build a plugin system or app marketplace.
- Do not sync settings between devices.
- Do not implement a full macOS clone; match the broad interaction model while staying native to Windows.

## Architecture

The app is a WPF background application with a tray icon and a transient full-screen overlay window. It starts silently, registers system integrations, and waits for the user to open the launchpad through the global hotkey or tray menu.

Core modules:

- `Shell/Tray`: owns application startup, tray menu, settings window entry point, and controlled shutdown.
- `HotkeyService`: registers and unregisters the global hotkey, detects registration failure, and raises open/close commands.
- `DesktopScanner`: scans only the current user's Desktop folder and normalizes discovered files, folders, `.lnk`, and `.url` entries.
- `ItemStore`: persists settings, manual entries, hidden scanned entries, and user-defined ordering in local JSON.
- `LaunchpadWindow`: renders the full-screen overlay, icon grid, frosted background, fade animations, drag sorting, click-to-launch behavior, and blank-area dismissal.
- `LauncherService`: launches files, folders, shortcuts, and URLs through Windows shell behavior and reports launch failures.

This split keeps Windows integration, persistence, and UI behavior separate. The launchpad window should not directly edit registry keys, register hotkeys, or parse shortcut files.

## Data Model

Settings are stored under the current user's app data directory:

`%AppData%\LaunchpadWindows\settings.json`

The settings file stores:

- hotkey definition, defaulting to `Ctrl + Alt + Space`;
- autostart enabled state;
- ordered item IDs;
- manually added entries;
- hidden automatically scanned entries;
- animation preferences for the overlay;
- settings window size and position.

Each launch item has:

- stable ID;
- display name;
- source type: `DesktopScan` or `Manual`;
- item kind: `Shortcut`, `Url`, `File`, or `Folder`;
- path or URL;
- resolved target path when available;
- icon cache key;
- last seen timestamp for scanned entries;
- ordering metadata.

For scanned Desktop entries, the stable ID should be derived from the full Desktop path and source type. For manual entries, the ID should be generated once and persisted.

## Desktop Scanning And Merge Rules

The app scans the current user's Desktop folder before showing the launchpad. First version scanning is non-recursive.

Supported automatic entries:

- `.lnk` shortcuts;
- `.url` shortcuts;
- regular files;
- folders.

Merge behavior:

- Existing entries keep the user's saved drag order.
- New Desktop entries are appended after known entries.
- Desktop entries deleted from disk disappear from the launchpad.
- Hidden Desktop entries stay hidden until restored from settings.
- Manual entries are independent of Desktop scanning and remain until removed by the user.
- If icon or shortcut target resolution fails, the entry still appears with a fallback icon.

## Launchpad Window

The launchpad opens as a borderless, topmost, full-screen WPF window on the monitor that contains the mouse pointer at the time of activation.

Visual and interaction requirements:

- Use a Windows 11 style frosted-glass or acrylic-like background.
- Fade in when opened and fade out when dismissed.
- Center a responsive icon grid over the background.
- Use fixed-size icon slots so hover states, drag behavior, and long names do not shift layout.
- Show system icons where possible.
- Clamp long names to two lines with ellipsis.
- Support drag-and-drop reordering and save the order immediately after drop.
- Single-clicking an item launches it, then closes the launchpad.
- Clicking outside the item grid closes the launchpad.
- Pressing `Esc` closes the launchpad.

The overlay should avoid permanent taskbar presence. If WPF requires a window for focus and keyboard handling, it should still behave as a transient launcher rather than a normal document window.

## Settings Window

The settings window is opened from the tray menu. It is a normal WPF window, not part of the full-screen overlay.

First version settings:

- Toggle start at login.
- Show and edit the global hotkey.
- Show the scanned Desktop folder path.
- Add a shortcut, file, or folder manually.
- Remove manually added entries.
- Hide automatically scanned entries.
- Restore hidden entries.

Tray menu:

- Open Launchpad.
- Settings.
- Exit.

## Autostart

Autostart is controlled per user without administrator permissions.

Implementation target:

`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

The settings window toggles the registry entry on or off. If writing fails, the app reports the error in settings and leaves the stored setting consistent with the actual registry state.

## Hotkey Handling

Default hotkey: `Ctrl + Alt + Space`.

The app registers the hotkey during startup. If registration fails, it keeps running in the tray, reports that the hotkey is unavailable, and lets the user change it in settings. The tray menu can still open the launchpad.

Hotkey behavior:

- If the launchpad is closed, open it on the mouse pointer's monitor.
- If the launchpad is open, close it with fade-out.

## Launching Items

Launching should use the Windows shell default behavior:

- `.lnk`: launch the shortcut target through shell execution.
- `.url`: open with the default browser.
- files: open with the default associated app.
- folders: open in File Explorer.

On successful launch, the launchpad closes automatically. On failure, the app shows a lightweight error message and keeps the item unless the user removes or hides it.

Failure cases to handle:

- missing path;
- broken shortcut;
- permission denied;
- no associated app;
- shell launch error;
- icon extraction failure.

## Error Handling

Errors should be visible but not dramatic. The app should avoid crashing for bad Desktop entries or stale manual paths.

Rules:

- Hotkey conflict: show a settings/tray notification and keep the tray app running.
- Broken path: show a short launch error and preserve the entry.
- Broken icon extraction: use a fallback icon.
- Corrupt settings JSON: back up the corrupt file, create a clean default settings file, and continue.
- Registry write failure: show the error in settings and leave autostart disabled if the registry entry cannot be written.
- Desktop scan failure: show an empty state or last known manual entries, then report the scan error.

## Testing And Verification

Verification should cover:

- first launch creates default settings;
- current user Desktop scan discovers files, folders, `.lnk`, and `.url` entries;
- public Desktop items are not included;
- global hotkey opens and closes the launchpad;
- launchpad opens on the monitor where the mouse pointer is located;
- clicking blank space closes the overlay;
- pressing `Esc` closes the overlay;
- single-clicking an entry launches it and closes the overlay;
- drag reorder persists after app restart;
- manual add and remove work for shortcuts, files, and folders;
- hiding and restoring scanned entries works;
- autostart writes and removes the `HKCU` Run value;
- hotkey conflict is surfaced without crashing;
- missing files and broken shortcuts do not crash the app.

Manual visual checks should verify:

- the frosted background appears on Windows 11;
- fade-in and fade-out feel smooth;
- long labels do not overlap neighboring items;
- grid layout works at common desktop resolutions and multiple display scaling values.

## Implementation Order

1. Create the WPF project skeleton and app lifecycle.
2. Add settings persistence and item data models.
3. Implement Desktop scanning and merge behavior.
4. Implement tray menu and settings window basics.
5. Register the global hotkey and wire open/close behavior.
6. Build the full-screen launchpad overlay.
7. Add icon extraction, launch behavior, and error reporting.
8. Add drag-and-drop ordering.
9. Add autostart registry integration.
10. Verify behavior and polish visuals.
