using System;
using BepInEx.Logging;
using CasualtiesTogetherAddons.Features;
using CasualtiesTogetherAddons.UI;
using CasualtiesTogetherUtils;
using HarmonyLib;
using Together;
using UnityEngine;

namespace CasualtiesTogetherAddons.Networking;

internal sealed class RejoinFeature : FeatureModule
{
    private static RejoinFeature instance;
    private readonly RejoinFlow flow = new();
    private Target target;
    private NetTransportBase originalTransport, joiningTransport;
    private bool cancelRequested;
    private SteamLobbyReader steamLobby;
    private const string ButtonDescription = "Disconnects, then rejoins the same host. Progress may be lost if the host doesn't restore your game session.";
    private RejoinPopup popup;
    private readonly AddonMenuInput menuInput;
    private IDisposable menuRegistration;

    public RejoinFeature(AddonMenuInput menuInput) => this.menuInput = menuInput;
    private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(Plugin.Id + ".Rejoin");
    public override string Id => "Rejoin";
    public override string DisplayName => "Rejoin server button";
    protected override string Description => "Adds a rejoin button for the current connected session to fix sync issues.";

    // Connection credentials are held only for this attempt, never logged or written to configuration.
    private sealed class Target
    {
        public ulong Lobby;
        public string Address, Password, JoinSecret, Name;
        public Color24 Color;
    }

    protected override void ApplyPatches(Harmony patches)
    {
        if (typeof(Multiplayer).Assembly.GetName().Version != new Version(5, 3, 1, 0))
            throw new NotSupportedException("Rejoin currently supports Casualties Together 5.3.1 only.");
        var draw = AccessTools.DeclaredMethod(typeof(UIMainMenu), "_GUI___DirectConnect_DoPasswordField");
        if (draw == null) throw new NotSupportedException("Missing 5.3.1 multiplayer menu hook.");
        steamLobby = new SteamLobbyReader(typeof(KSteam));
        var skin = AccessTools.DeclaredMethod(typeof(UIBullshit), "_GUI_DoTexturesForMPUserInterface", Type.EmptyTypes);
        if (skin == null) throw new NotSupportedException("Missing multiplayer menu skin helper.");
        if (menuInput == null) throw new NotSupportedException("Addon menu input is unavailable.");
        popup = new RejoinPopup((Action)skin.CreateDelegate(typeof(Action)));
        instance = this;
        patches.Patch(draw, postfix: new HarmonyMethod(typeof(RejoinFeature), nameof(DrawButton)));
        menuRegistration = menuInput.Register(() => PopupVisible);
    }

    private static bool Ready() => ConnectedToServer() && Util.IsWorldGenerated() && !ClientMain.ServerIsGeneratingWorld;

    private static bool ConnectedToServer() => Net.IsClient && Net.IsConnected;

    private bool PopupVisible => popup?.IsOpen == true && !Util.IsGeneratingWorld();

    private static void DrawButton()
    {
        var self = instance;
        if (self == null || !ConnectedToServer()) return;
        bool enabled = GUI.enabled;
        bool clicked;
        try
        {
            GUI.enabled = enabled && !self.popup.IsOpen && !self.flow.Active && Ready()
                && Time.realtimeSinceStartup >= self.flow.CooldownUntil;
            clicked = GUILayout.Button("Rejoin server");
        }
        finally { GUI.enabled = enabled; }
        GUILayout.Label(ButtonDescription, new GUIStyle(GUI.skin.label) { wordWrap = true });
        if (clicked) self.QueueRejoin();
    }

    internal void DrawPopup()
    {
        if (IsActive && PopupVisible) popup.Draw(flow.Phase, () => cancelRequested = true);
    }

    private void QueueRejoin()
    {
        if (!Ready()) return;
        try
        {
            var captured = new Target();
            if (Net.IsRunningSteam && Net.Transport is TransportSteamworks)
            {
                captured.Lobby = steamLobby.Read();
                if (!KSteam.IS_IN_LOBBY || captured.Lobby == 0)
                    throw new InvalidOperationException("The current Steam lobby could not be identified.");
            }
            else if (Net.Transport is TransportLiteNetLib direct && direct.CLIENT_PEER != null)
            {
                // Use the active peer, not a potentially stale or edited address text field.
                captured.Address = direct.CLIENT_PEER.ToString();
                if (!Net.TryParseIPPORT(captured.Address, out _, out _))
                    throw new InvalidOperationException("The active server address is not supported by the stock connector.");
            }
            else throw new InvalidOperationException("The current connection type is not supported.");
            captured.Password = Multiplayer.INPUT_PASSWORD;
            captured.JoinSecret = Multiplayer.CLIENT_JOIN_SECRET;
            captured.Name = UIMainMenu.USERINPUT_NAME;
            captured.Color = UIMainMenu.LAST_VALID_INPUT_COLOR;
            if (!flow.Begin(Time.realtimeSinceStartup)) return;
            target = captured;
            originalTransport = Net.Transport;
            joiningTransport = null;
            cancelRequested = false;
            popup.ShowProgress();
        }
        catch (Exception)
        {
            // Avoid logging transport/credential values from an exception.
            Fail("Can't rejoin this session.");
        }
    }

    internal void Tick()
    {
        if (!IsActive) return;
        if (!flow.Active) return;
        if (cancelRequested) { Cancel(); return; }
        if (flow.Phase == RejoinPhase.Queued && (!Ready() || !ReferenceEquals(Net.Transport, originalTransport)))
        { EndCancelledAttempt(); return; }
        if ((flow.Phase == RejoinPhase.Connecting || flow.Phase == RejoinPhase.Loading)
            && Net.Transport != null && !ReferenceEquals(Net.Transport, joiningTransport))
        { EndCancelledAttempt(); return; }
        try
        {
            bool connected = ConnectedToServer();
            bool loaded = connected && ScavPlayer.LocalPlayer != null && (Util.IsWorldGenerated()
                || (Util.IsInMainMenu() && Net.CurrentServerInfo.cur_layer < 0));
            var action = flow.Step(Time.realtimeSinceStartup, Net.Transport != null,
                Util.IsInMainMenu() && PreRunScript.instance != null && !KSteam.IS_IN_LOBBY, connected, loaded);
            switch (action)
            {
                case RejoinAction.Disconnect:
                    Multiplayer.ShutdownNetwork();
                    break;
                case RejoinAction.Connect:
                    // Restore the captured introduction fields before the stock connector builds its handshake.
                    Multiplayer.INPUT_PASSWORD = target.Password;
                    Multiplayer.CLIENT_JOIN_SECRET = target.JoinSecret;
                    UIMainMenu.USERINPUT_NAME = target.Name;
                    UIMainMenu.LAST_VALID_INPUT_COLOR = target.Color;
                    bool started = target.Lobby != 0
                        ? TransportSteamworks.OnWantToJoinLobby(target.Lobby)
                        : TransportLiteNetLib.OnWantToConnect(target.Address, Net.NetType.Client);
                    joiningTransport = Net.Transport;
                    target = null; // Release copied credentials as soon as the handshake has been started.
                    originalTransport = null;
                    if (!started || joiningTransport == null)
                    {
                        StopOwnedConnection();
                        Fail("Couldn't reconnect.");
                    }
                    break;
                case RejoinAction.Completed:
                    Complete();
                    break;
                case RejoinAction.ConnectTimedOut:
                    StopOwnedConnection();
                    Fail("Connection timed out.");
                    break;
                case RejoinAction.LoadTimedOut:
                    StopTrackingSlowLoad();
                    break;
                case RejoinAction.Failed:
                    Fail("Couldn't reconnect.");
                    break;
                case RejoinAction.Cancelled:
                    EndCancelledAttempt();
                    break;
            }
        }
        catch (Exception)
        {
            // Never print exceptions that could include connection credentials or private endpoints.
            StopOwnedConnection();
            Fail("Rejoin failed.");
        }
    }

    private void StopOwnedConnection()
    {
        if (joiningTransport != null && ReferenceEquals(Net.Transport, joiningTransport))
        {
            try { Multiplayer.ShutdownNetwork(); }
            catch (Exception) { Log.LogError("The stock disconnect handler failed while cancelling a rejoin."); }
        }
    }

    private void Cancel()
    {
        // Before the disconnect step, cancellation leaves the original connection alone.
        // During a new connection, cancel only the transport created by this attempt.
        StopOwnedConnection();
        EndCancelledAttempt();
    }

    private void Complete()
    {
        ResetAttempt();
        popup.Close();
        Log.LogInfo("Rejoined successfully.");
    }

    private void EndCancelledAttempt()
    {
        ResetAttempt();
        popup.Close();
        Log.LogInfo("Rejoin cancelled.");
    }

    private void StopTrackingSlowLoad()
    {
        ResetAttempt();
        popup.Close();
        Log.LogInfo("World loading continues; rejoin tracking stopped.");
    }

    private void Fail(string message)
    {
        ResetAttempt();
        popup.ShowError(message);
        Log.LogError(message);
        // The stock multiplayer status retains the host's detailed rejection reason.
    }

    private void ResetAttempt()
    {
        flow.Cancel();
        target = null;
        originalTransport = joiningTransport = null;
        cancelRequested = false;
    }

    protected override void ReleaseResources()
    {
        if (instance == this) instance = null;
        menuRegistration?.Dispose();
        menuRegistration = null;
        popup?.Close();
        // Stop future automatic actions without tearing down a live session.
        ResetAttempt();
    }
}
