# Launchpad Windows Design

Date: 2026-06-22
Status: approved design, pending written-spec review

## Goal

Build a native Windows 11 launchpad inspired by macOS Launchpad. The app runs in the background, opens with a global hotkey, shows a full-screen frosted-glass launcher on the screen where the mouse is located, and lets the user start desktop items with one click. It automatically scans the current user's Desktop folder and also supports manually added shortcuts, files, and folders.

## Decisions

- Technology stack: native WPF / .NET single-process desktop app.
- Target OS: Windows 11.
- Desktop scan scope: current user's resolved Desktop known folder only.
- Global hotkey: `Ctrl + Alt + Space` by default.
- Multi-monitor behavior: open on the monitor containing the current mouse pointer.
- Item behavior: single-click launches the item and closes the launchpad.
- Sorting: drag-and-drop reorder with persisted order.
- Settings: first version includes a simple settings window.

## Non-Goals For The First Version

- Do not scan the public Desktop folder.
- Do not recursively scan subfolders on the Desktop.
- Do not add free-form URL entry UI; first-version manual URL support is through `.url` files.
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

- schema version, starting at `1`;
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

For scanned Desktop entries, the stable ID should be derived from the source type and a normalized full path. Path normalization means resolving the absolute path, trimming trailing directory separators, and using case-insensitive comparison semantics. Renaming a Desktop item is treated as removing the old item and discovering a new item. Stale ordering IDs should be removed on the next successful merge/save pass. Stale hidden IDs may remain in settings and are ignored when no current scanned item matches them.

For manual entries, the ID should be generated once and persisted.

## Desktop Scanning And Merge Rules

The app resolves the current user's Desktop folder through Windows known-folder behavior, such as .NET's `Environment.SpecialFolder.DesktopDirectory` or a shell known-folder API. It must not build the Desktop path manually from `%USERPROFILE%`. OneDrive/Desktop redirection should therefore use the actual current-user Desktop path. If Desktop path resolution or enumeration fails, the scan failure path applies and manual entries remain available.

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
- If the item grid exceeds the available viewport, keep fixed-size tiles and allow vertical scrolling inside the overlay.
- Use fixed-size icon slots so hover states, drag behavior, and long names do not shift layout.
- Show system icons where possible.
- Clamp long names to two lines with ellipsis.
- Support drag-and-drop reordering and save the order immediately after drop.
- Single-clicking an item launches it, then closes the launchpad.
- Clicking the overlay background outside any item slot closes the launchpad. Item-slot whitespace, error messages, and active drag operations do not count as background clicks.
- Pressing `Esc` closes the launchpad.

The overlay should avoid permanent taskbar presence. If WPF requires a window for focus and keyboard handling, it should still behave as a transient launcher rather than a normal document window. The app should be per-monitor DPI aware and should place the overlay using the full bounds of the monitor containing the mouse pointer, including negative-coordinate monitors and mixed display scaling. The overlay covers the selected monitor's taskbar area while open.

The preferred Windows 11 visual treatment is a frosted-glass or acrylic-like background. If the system backdrop API is unavailable, the fallback is a translucent dark overlay that still keeps labels legible.

## Settings Window

The settings window is opened from the tray menu. It is a normal WPF window, not part of the full-screen overlay.

First version settings:

- Toggle start at login.
- Show and edit the global hotkey.
- Show the scanned Desktop folder path.
- Add a `.lnk`, `.url`, regular file, or folder manually.
- Remove manually added entries.
- Hide automatically scanned entries.
- Restore hidden entries.

The settings window shows separate lists for manual items, visible scanned Desktop items, and hidden scanned Desktop item IDs. Hide, restore, manual add, and manual remove actions save immediately. The next launchpad open must reflect those changes; if the overlay is already open, it may remain unchanged until reopened in the first version.

Tray menu:

- Open Launchpad.
- Settings.
- Exit.

Closing the settings window does not exit the app. Only the tray `Exit` command exits the app; exit closes any overlay, unregisters the hotkey, and disposes the tray icon.

## Autostart

Autostart is controlled per user without administrator permissions.

Implementation target:

`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

The Run value name is `LaunchpadWindows`. The value data is the quoted executable path, for example `"C:\Path\LaunchpadWindows.exe"`. The settings window toggles the registry entry on or off. If writing or deleting fails, the app reports the error in settings and does not change the persisted autostart setting.

## Hotkey Handling

Default hotkey: `Ctrl + Alt + Space`.

The app registers the hotkey during startup. If registration fails, it keeps running in the tray, reports that the hotkey is unavailable, and lets the user change it in settings. The tray menu can still open the launchpad.

Hotkey behavior:

- If the launchpad is closed, open it on the mouse pointer's monitor.
- If the launchpad is open, close it with fade-out.

Hotkey editing behavior:

- The settings UI accepts combinations with at least one modifier and one non-modifier key.
- Empty hotkeys and modifier-only hotkeys are invalid.
- When the user applies a new hotkey, the app attempts to register it before saving.
- If registration succeeds, the app saves the new hotkey and unregisters the old one.
- If registration fails, the app keeps the old registered hotkey and old stored setting, then shows an error in settings.

## Launching Items

Launching should use the Windows shell default behavior:

- `.lnk`: shell-execute the `.lnk` file itself so shortcut arguments, working directory, and shell metadata are preserved. The resolved target path is diagnostic metadata and may be used to detect obviously broken shortcuts, but it is not the default launch target.
- `.url`: open with the default browser.
- files: open with the default associated app.
- folders: open in File Explorer.

On successful launch, the launchpad closes automatically. On failure, the app shows a lightweight error message inside the overlay and keeps the item unless the user removes or hides it. Settings errors appear in the settings window error row. Startup or hotkey-registration errors may use a tray notification. Errors must not crash the app or delete entries.

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
- Registry write/delete failure: show the error in settings and leave the persisted autostart setting unchanged.
- Desktop scan failure: show an empty state or last known manual entries, then report the scan error.

## Testing And Verification

Verification should cover:

- first launch creates default settings;
- default settings include schema version `1`;
- corrupt settings JSON is backed up and replaced with defaults;
- current user Desktop scan discovers files, folders, `.lnk`, and `.url` entries;
- public Desktop items are not included;
- Desktop scan failure does not hide manual entries or crash the app;
- global hotkey opens and closes the launchpad;
- invalid or conflicting hotkey edits keep the previous hotkey;
- launchpad opens on the monitor where the mouse pointer is located;
- clicking blank space closes the overlay;
- pressing `Esc` closes the overlay;
- single-clicking an entry launches it and closes the overlay;
- `.url` entries open through the default browser path;
- drag reorder persists after app restart;
- manual add and remove work for shortcuts, files, and folders;
- hiding and restoring scanned entries works;
- autostart writes and removes the `HKCU` Run value;
- autostart write and delete failures do not lie about the stored autostart state;
- hotkey conflict is surfaced without crashing;
- missing files and broken shortcuts do not crash the app;
- icon extraction failure shows a fallback icon;
- tray Exit unregisters the hotkey and removes the tray icon.

Manual visual checks should verify:

- the frosted background appears on Windows 11;
- translucent fallback remains legible if acrylic is unavailable;
- fade-in and fade-out feel smooth;
- long labels do not overlap neighboring items;
- grid layout works at common desktop resolutions and multiple display scaling values, including 1920x1080 at 100%, 2560x1440 at 125%, 3840x2160 at 150%, and a mixed-DPI dual-monitor layout when available.

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
