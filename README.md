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

## Configuration

After the first launch, edit this file inside the game directory:

```text
BepInEx\config\CasualtiesTogetherAddons.cfg
```

```ini
[General]
Enabled = true

[MicTogglePersistence]
Enabled = true
```

`General.Enabled` controls all features. Each feature also has its own `Enabled` setting. Set either value to `false` to disable it. **Close the game before editing, then relaunch. All switches currently require a game restart.** An in-game settings panel is not implemented yet.

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

Installation also removes the old starter `BepInEx\plugins\CasualtiesMicMemory\CasualtiesMicMemory.dll`, if present. It does not remove other plugins, alter the multiplayer DLL, or overwrite game dependencies.

## Verify in game

Launch through Steam and look in the BepInEx console or `BepInEx\LogOutput.log` for:

```text
Casualties Together Addons 0.0.1 loaded. Feature switches require a game restart.
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
7. Disable `MicTogglePersistence.Enabled`, restart, and confirm the original loading behavior returns.

These multiplayer/audio checks must be performed in game; automated tests do not access a real microphone or lobby.

## Automated tests

```powershell
.\test.ps1
```

The Windows .NET Framework 4.8 harness links the production feature code and uses the installed HarmonyX library to patch a small voice-chat fixture. It checks patch installation/removal, repeated blocked/resumed transitions, open/closed choices, manual mute, mode changes, restrictions, and both feature switches. It does not load the game's real voice-chat implementation. The test build restores the .NET Framework reference-assemblies package; the plugin itself uses local game references.

## Adding another feature

1. Add a class under `CasualtiesTogetherAddons/Features` inheriting `FeatureModule`.
2. Give it a stable `Id`, a description, and an `ApplyPatches` implementation. Validate required methods and fields before patching.
3. Register an instance in `Plugin.features`.

The base class creates a separate config section and Harmony owner ID for each feature, skips disabled modules, and removes that module's patches if installation fails. Features with event handlers, objects, or other side effects must add their own cleanup rather than relying solely on unpatching. The entry point declares a hard dependency on the multiplayer plugin (`CasualtiesMP`).

## Compatibility and removal

Built against the locally installed multiplayer assembly 5.2.0. The sibling source checkout specifies 5.2.1, so it is used for reference rather than deployment. Multiplayer updates may require changes to the patches. A host's optional identical-plugin-list rule can still reject additional client plugins.

To uninstall, close the game and remove only `BepInEx\plugins\CasualtiesTogetherAddons`. Settings remain in the config file until you remove it separately.
