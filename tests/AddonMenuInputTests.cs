using System;
using System.Runtime.CompilerServices;
using CasualtiesTogetherAddons.UI;

// Fixtures execute real Harmony patches from the shared production input layer.
public sealed class AdaptiveButton
{
    public static bool NativeOverlay;
    public bool overlayActive { [MethodImpl(MethodImplOptions.NoInlining)] get => NativeOverlay; }
}
public static class UIUtil
{
    public static bool NativeOverlay;
    [MethodImpl(MethodImplOptions.NoInlining)] public static bool IsPointerOverUIElement() => NativeOverlay;
    [MethodImpl(MethodImplOptions.NoInlining)] public static bool IsPointerOverUIElement(int unused) => NativeOverlay;
}
public sealed class PlayerCamera
{
    public int Calls;
    [MethodImpl(MethodImplOptions.NoInlining)] public void HandleInput() => Calls++;
}
namespace Together
{
    public struct PlayerInputPacket
    {
        public int Move;
        public bool IsInPauseMenu, ButtonAttack, ButtonIteminteract;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void CollectInput() { Move = 1; IsInPauseMenu = false; ButtonAttack = ButtonIteminteract = true; }
    }
}

internal static class AddonMenuInputTests
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string description)
        {
            if (!condition) throw new Exception("FAIL: " + description);
            checks++;
            Console.WriteLine("PASS: " + description);
        }
        var camera = new PlayerCamera();
        var button = new AdaptiveButton();
        var packet = new Together.PlayerInputPacket();
        bool firstVisible = false, secondVisible = false;
        var input = new AddonMenuInput();
        var first = input.Register(() => firstVisible);
        var second = input.Register(() => secondVisible);
        try
        {
            camera.HandleInput(); packet.CollectInput();
            Check(!button.overlayActive && !UIUtil.IsPointerOverUIElement() && camera.Calls == 1 && packet.Move == 1,
                "Hidden addon dialogs preserve ordinary native input");
            firstVisible = true;
            camera.HandleInput(); packet.CollectInput();
            Check(button.overlayActive && UIUtil.IsPointerOverUIElement() && UIUtil.IsPointerOverUIElement(0),
                "A visible addon dialog blocks native main-menu and both pointer-query paths");
            Check(camera.Calls == 1, "Modal stops local gameplay input without changing any menu state");
            Check(packet.IsInPauseMenu && packet.Move == 0 && !packet.ButtonAttack && !packet.ButtonIteminteract,
                "Modal suppresses outgoing movement and actions using the native player-input channel");
            firstVisible = false;
            secondVisible = true;
            Check(button.overlayActive, "A second addon dialog reuses the same input hooks");
            first.Dispose(); first.Dispose();
            Check(button.overlayActive, "Unregistering one dialog does not unblock another visible dialog");
            secondVisible = false;
            AdaptiveButton.NativeOverlay = UIUtil.NativeOverlay = true;
            Check(button.overlayActive && UIUtil.IsPointerOverUIElement(), "Native overlays remain blocked independently of addon visibility");
            AdaptiveButton.NativeOverlay = UIUtil.NativeOverlay = false;
            camera.HandleInput(); packet.CollectInput();
            Check(!button.overlayActive && camera.Calls == 2 && packet.Move == 1 && !packet.IsInPauseMenu && packet.ButtonAttack,
                "Closing the last addon dialog immediately restores native input");
            secondVisible = true;
            second.Dispose();
            Check(!button.overlayActive, "Disposing an open dialog's registration releases its input block");
            using (input.Register(() => true)) Check(button.overlayActive, "Shared layer can register another dialog after earlier cleanup");
            var remaining = input.Register(() => true);
            input.Dispose(); remaining.Dispose();
            camera.HandleInput(); packet.CollectInput();
            Check(!button.overlayActive && !UIUtil.IsPointerOverUIElement() && camera.Calls == 3 && packet.Move == 1,
                "Plugin cleanup removes all modal hooks and registrations");
        }
        finally { first.Dispose(); second.Dispose(); input.Dispose(); }
        Console.WriteLine("All " + checks + " shared menu input checks passed.");
    }
}
