# Rejoin server

## Use

Install the addon on the client; the host needs no addon. Supported multiplayer assembly: Casualties Together 5.3.1.0.

Open Multiplayer Mod Menu > Multiplayer. **Rejoin server** and its warning appear below the password row while connected as a client. It is enabled in a loaded world, including for dead/spectating clients. The button is disabled during an attempt, while a dialog is open, and during its ten-second cooldown. Hosts cannot use it.

The **Rejoin server button** setting defaults to enabled and requires a restart when changed:

```ini
[Rejoin]
Enabled = true
```

Clicking the button opens a small popup with animated gray progress text and **Cancel**. It remains independent of the menu during disconnection and connection. Success or cancellation closes it. Failure displays a short red message with **Close** until dismissed. The warning stays beneath the original menu button. World generation uses the normal loading screen, without the popup over it.

The popup reuses the multiplayer mod's panel textures, font preference, button states and click sound. Text and controls are measured using the native styles, and the complete dialog is uniformly reduced if necessary to fit the screen. It registers actual visibility with the shared AddonMenuInput layer. That layer blocks main-menu buttons, pointer-over-UI queries, local gameplay handling and outgoing movement/actions. It does not modify any menu's open state or IsOpen query, pause the world, or alter connection state. Closing the popup releases its input block immediately.

## Connection behavior

The addon captures the active direct peer endpoint or current Steam lobby ID before disconnecting. It holds the current introduction name/color, password and join secret in memory for one attempt, without logging or saving the copied credentials. Direct connections use the active endpoint rather than the address text field.

The next plugin Update calls the stock disconnect handler, returning to the main menu and leaving the Steam lobby where applicable. After local cleanup and a minimum two-second gap, it makes one stock connection attempt to the captured target. Copied credentials are released after starting the handshake. Ordinary join synchronization and character restoration then run on the host. Other players see the normal departure and return.

Cancel before disconnection leaves the original connection alone. Cancel during the menu gap stops the queued reconnect; while connecting it disconnects only the transport created by this attempt. Starting a different connection supersedes the attempt. The addon does not automatically retry.

Main-menu readiness is limited to 20 seconds, connection setup to 60 seconds, and connected world loading to 120 seconds. A connection timeout cancels the owned transport. A loading timeout stops tracking and leaves the connected session running. Detailed host rejection information remains in the original multiplayer status display.

## Limitations

- This is an actual leave/rejoin, with loading time and bandwidth/processing cost. It can repair stale client items/entities through normal joining, but does not fix the underlying recurring multiplayer bug.
- The host controls checkpoint restoration, inventory, health, skills/XP, spawn position and spectator behavior. The addon does not copy back stats or items. Recent actions may be lost, and exact character state or position is not guaranteed.
- The world continues while you are away. Trades, crafting, minigames and carry relationships can be interrupted. Disconnected-character behavior follows the host's rules.
- A full/locked lobby, late-join restrictions, changed credentials, bans, plugin-list enforcement, host shutdown or Steam errors can reject the attempt. No slot is reserved and no host rules are bypassed.
- The two-second gap is not a remote acknowledgement. Host cleanup, old callbacks and layer transitions can still expose stock reconnect races under latency/load.
- Steam reuses the lobby ID, whose owner can change. Direct connect reuses the resolved peer endpoint rather than resolving the original hostname again.
- Cancel after disconnection may leave you at the main menu. The feature is pinned to the inspected 5.3.1 build; other patches can change native behavior.

## Verification

Build with `build.ps1`; install with `build.ps1 -Deploy` while the game is closed. If multiple multiplayer DLLs are installed, specify the active one with `-TogetherDll`, using its location from the BepInEx log.

The fixture suite passes 128 checks: 18 rejoin flow, 5 Steam lobby compatibility, 16 popup layout, 11 shared modal input, and 78 existing feature checks. Shared input tests execute the production Harmony patches against fixtures, covering multiple dialogs, both pointer query paths, native overlays, outgoing input, and registration/plugin cleanup. The optional --installed mode checks the real native input hook locations and that the feature does not call the multiplayer menu's open-state setter/query. Loading the addon for this optional mode has been blocked by Windows Application Control; it is not a live gameplay test.

## Reusing modal input in another addon feature

The plugin owns one AddonMenuInput service for its lifetime. A modal feature receives that service and keeps the IDisposable returned by `menuInput.Register(() => dialogIsActuallyVisible)`. The visibility predicate should be a side-effect-free check and return false when its UI is hidden, including during loading if appropriate. Closing a dialog changes its visibility; feature disposal must dispose the registration. The service releases input only after all registered dialogs are hidden or removed. Rendering and network operations remain the feature's responsibility. No additional input patches are needed for another modal using the same interaction paths.

In-game checks still required:

1. Rejoin an unmodified host using direct connect and Steam. Compare items, equipment, nested bags, other players/entities and character progress before/after.
2. Confirm successful rejoining returns to the game's normal menu state, without reopening the multiplayer menu. Check native popup styling and Cancel/Close visibility at normal/high UI scales and in smaller windows.
3. Test cancellation before disconnect, during the menu gap and while connecting; test failure dismissal and starting a replacement connection. The addon must not disconnect a replacement session.
4. Test password/invite-secret sessions, IPv4/IPv6, latency, slow loading, full/locked lobbies, host shutdown and a host changing layers during the gap. Verify one attempt and useful native rejection details.
5. Disable Rejoin and restart: its button and input hook should disappear while the voice features remain usable.
