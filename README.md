# Casualties Together Addons

A BepInEx 5 companion plugin for independently configurable features on top of Casualties Together. The original multiplayer DLL is referenced, not rebuilt or replaced.

## Features

### MicTogglePersistence

Enabled by default. Remembers your toggle-to-talk choice through world loading and reconnecting within the same running game. The patch preserves the toggle around the multiplayer mod's automatic reset; the original voice-chat method still controls recording and transmission.

- A mic toggled closed stays closed.
- A mic toggled open resumes when voice chat is available again.
- Setting mic volume to zero or switching away from toggle-to-talk clears the open choice.
- Server voice restrictions and character speech restrictions still apply.
- No microphone state is saved to disk: a full game restart starts with the original closed state.
- Other microphone modes retain their original recording behavior.

## Plugin identity

Display name: `Casualties Together Addons`. Plugin ID: `CasualtiesTogetherAddons`. Version: `0.0.1`.

The project file is the source of truth: `PluginId`, `Product`, and `Version` generate the runtime plugin constants during builds. The DLL, project, namespace, and installation folder all use `CasualtiesTogetherAddons`. Each feature's Harmony owner is the plugin ID followed by its feature ID (for example, `CasualtiesTogetherAddons.MicTogglePersistence`).

## In-game settings

Open **Settings > Multiplayer**, then find **Casualties Together Addons** directly below **Deactivate Multiplayer Mod**, before **User Interface**.

- **Remember microphone toggle** is the only checkbox for now, enabled by default.
- Changes save automatically and apply after a full game restart.
- In both menus, a changed setting is labeled **(restart required)** and shows its own **Restart Game** button. Reverting the setting to its value at launch removes the label and button. The game-settings layout makes room for visible restart buttons without leaving empty rows when hidden.
- There is no master switch or separate hotkey window.

The section reuses the game's existing checkbox and label prefabs and the multiplayer mod's layout helpers. No additional plugins or downloads are needed. Future registered features appear in this section automatically.

The mod-menu section uses prominent option names and smaller muted descriptions. Pending changes use the short restart-required suffix and Restart Game button, without extra explanatory text. Description text is indented and wraps within the content panel.

The same options are also available under **Multiplayer Mod Menu > Settings > Casualties Together Addons**, in the sidebar below **Bug Report**. Both menus use the same saved settings; this adds another entry point rather than a second configuration. Changes save automatically in this menu too. **Reset to Defaults** in our mod-menu section restores the feature's default of on and saves it. The existing **Save** button remains usable; addon choices are already saved when changed. Each pending option has a **Restart Game** button. It flushes the addon configuration and uses Casualties Together's restart function, restarting the entire game and applying all saved changes, not just that option.

## Configuration

After the first launch, edit this file inside the game directory:

```text
BepInEx\config\CasualtiesTogetherAddons.cfg
```

```ini
[MicTogglePersistence]
Enabled = true
```

Each feature has its own `Enabled` setting. Set it to `false` to disable that feature. **For manual edits, close the game before editing, then relaunch.**

## Build and install

From PowerShell in this repository:

```powershell
.\build.ps1
```

Close the game, then build and install:

```powershell
.\build.ps1 -Deploy
```

The default game folder is `C:\Program Files (x86)\Steam\steamapps\common\Casualties Unknown Demo`. Override it with `-GamePath 'D:\path\to\game'`. The script locates the installed `CasualtiesMP.dll` recursively and copies only our new DLL into `BepInEx\plugins\CasualtiesTogetherAddons`.

## Verify in game

Launch through Steam and look in the BepInEx console or `BepInEx\LogOutput.log` for:

```text
Casualties Together Addons 0.0.1 loaded. Settings: Multiplayer tab. Feature switches require a game restart.
[MicTogglePersistence] Enabled.
```

A disabled feature logs `Disabled.`. An incompatible feature logs `Could not enable; feature skipped` and removes its own patches.

Manual test with another player:

1. Select toggle-to-talk mode, toggle open, and confirm they hear you.
2. Load the next layer; confirm voice resumes without pressing the key again.
3. Toggle closed, load again, and confirm it stays closed.
4. Repeat open/closed checks when leaving and rejoining a lobby.
5. Set mic volume to zero while open, then raise it: it should stay closed until toggled.
6. Switch to another mic mode and back: it should stay closed until toggled.
7. In Settings > Multiplayer > Casualties Together Addons, disable the mic feature, confirm the restart-required label appears, restart, and confirm the original loading behavior returns. Re-enable and restart to restore the patch.

These multiplayer/audio checks must be performed in game; automated tests do not access a real microphone or lobby.

The mod-menu sidebar needs an in-game check too: open Settings, confirm our section appears below Bug Report, change its checkbox, and confirm the same preference appears in the game settings screen after reopening the Multiplayer tab. Reset to Defaults in our sidebar section should turn the feature on. The native menu also needs an in-game check: open the Multiplayer tab, confirm the add-on section appears below Deactivate Multiplayer Mod and above User Interface, change the checkbox, switch tabs and return to confirm the saved choice, then restart to check the changed behavior. Reopening the tab should not duplicate our controls. Toggle an option and confirm its restart button appears in both menus; toggle back and confirm it disappears. After saving any run you want to keep, use Restart Game to verify the original mod's relaunch flow and confirm the selected option is applied. The multiplayer mod's Reset to Default button continues to reset its own settings; set our checkbox directly to change our preference.

## Automated tests

```powershell
.\test.ps1
```

The Windows .NET Framework 4.8 harness links the production feature and mod-menu integration code and uses the installed HarmonyX library to patch a small voice-chat fixture. It checks saved preferences, restart status, patch installation/removal, repeated blocked/resumed transitions, open/closed choices, manual mute, mode changes, restrictions, and the individual feature switch. Additional checks verify shared pending-change labels, reverting settings, saving before a simulated restart request, and exercise the actual sidebar transpiler, title translation, shared default reset, and cleanup against a small menu fixture, including the branch where Bug Report is absent. It does not load the game's real voice-chat or menu implementation. The test build restores the .NET Framework reference-assemblies package; the plugin itself uses local game references.

## Adding another feature

1. Add a class under `CasualtiesTogetherAddons/Features` inheriting `FeatureModule`.
2. Give it a stable `Id`, a description, and an `ApplyPatches` implementation. Validate required methods and fields before patching.
3. Register an instance in `Plugin.features`.

The base class creates a separate config section and Harmony owner ID for each feature, skips disabled modules, and removes that module's patches if installation fails. Features with event handlers, objects, or other side effects must add their own cleanup rather than relying solely on unpatching. The entry point declares a hard dependency on the multiplayer plugin (`CasualtiesMP`).

## Multiplayer activation

The BepInEx hard dependency prevents this plugin from loading if `CasualtiesMP` is missing. If multiplayer is installed but deactivated, disabled, or failed to initialize, our component skips feature and menu initialization and disables itself. It checks the installed plugin instance, `SUCCESFULLY_INITIALIZED`, `FORCE_DISABLE_MP_MOD`, and multiplayer's saved deactivation preference. During an active session, losing these conditions removes our settings and patches. Re-enabling multiplayer uses the original mod's restart flow.

Manual activation checks: deactivate multiplayer and restart; confirm no addon settings or feature patches initialize. Reactivate multiplayer and restart; confirm the section returns and the saved feature preference is honored. Missing-plugin behavior is enforced by BepInEx's dependency loader. These engine-lifecycle checks are not covered by the voice-chat fixture tests.

## Compatibility and removal

Built against the locally installed multiplayer assembly 5.2.0. The sibling source checkout specifies 5.2.1, so it is used for reference rather than deployment. Multiplayer updates may require changes to the patches. A host's optional identical-plugin-list rule can still reject additional client plugins.

To uninstall, close the game and remove only `BepInEx\plugins\CasualtiesTogetherAddons`. Settings remain in the config file until you remove it separately.
