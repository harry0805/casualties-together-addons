using System;
using System.Collections.Generic;
using HarmonyLib;
using Together;

namespace CasualtiesTogetherAddons.UI;

// Shared input integration for addon modals. Providers report actual visibility;
// no menu-open flags, pause state or network/session state are changed here.
internal sealed class AddonMenuInput : IDisposable
{
    private static AddonMenuInput instance;
    private readonly Harmony patches = new(Plugin.Id + ".MenuInput");
    private readonly HashSet<Registration> menus = new();
    private bool disposed;

    public AddonMenuInput()
    {
        if (instance != null) throw new InvalidOperationException("Addon menu input is already initialized.");
        var overlay = AccessTools.PropertyGetter(typeof(AdaptiveButton), nameof(AdaptiveButton.overlayActive));
        var pointer = AccessTools.GetDeclaredMethods(typeof(UIUtil)).FindAll(method =>
            method.IsStatic && method.Name == nameof(UIUtil.IsPointerOverUIElement) && method.ReturnType == typeof(bool));
        var camera = AccessTools.DeclaredMethod(typeof(PlayerCamera), nameof(PlayerCamera.HandleInput), Type.EmptyTypes);
        var packet = AccessTools.DeclaredMethod(typeof(PlayerInputPacket), nameof(PlayerInputPacket.CollectInput), Type.EmptyTypes);
        if (overlay == null || pointer.Count == 0 || camera == null || packet == null)
            throw new NotSupportedException("Missing addon modal input hooks.");
        instance = this;
        try
        {
            patches.Patch(overlay, postfix: new HarmonyMethod(typeof(AddonMenuInput), nameof(BlockOverlay)));
            foreach (var query in pointer)
                patches.Patch(query, postfix: new HarmonyMethod(typeof(AddonMenuInput), nameof(BlockOverlay)));
            patches.Patch(camera, prefix: new HarmonyMethod(typeof(AddonMenuInput), nameof(AllowGameplayInput)) { priority = Priority.First });
            patches.Patch(packet, postfix: new HarmonyMethod(typeof(AddonMenuInput), nameof(BlockPlayerInput)));
        }
        catch { Dispose(); throw; }
    }

    public IDisposable Register(Func<bool> isVisible)
    {
        if (disposed) throw new ObjectDisposedException(nameof(AddonMenuInput));
        if (isVisible == null) throw new ArgumentNullException(nameof(isVisible));
        var registration = new Registration(this, isVisible);
        menus.Add(registration);
        return registration;
    }

    private bool BlocksInput
    {
        get
        {
            foreach (var menu in menus)
                if (menu.IsVisible()) return true;
            return false;
        }
    }

    private static void BlockOverlay(ref bool __result) => __result |= instance?.BlocksInput == true;
    private static bool AllowGameplayInput() => instance?.BlocksInput != true;

    private static void BlockPlayerInput(ref PlayerInputPacket __instance)
    {
        if (instance?.BlocksInput != true) return;
        __instance.IsInPauseMenu = true;
        __instance.Move = default;
        __instance.ButtonAttack = false;
        __instance.ButtonIteminteract = false;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (instance == this) instance = null;
        foreach (var menu in menus) menu.Detach();
        menus.Clear();
        patches.UnpatchSelf();
    }

    private sealed class Registration : IDisposable
    {
        private AddonMenuInput owner;
        private Func<bool> isVisible;
        public Registration(AddonMenuInput owner, Func<bool> isVisible) { this.owner = owner; this.isVisible = isVisible; }
        public bool IsVisible() => isVisible?.Invoke() == true;
        public void Detach() { owner = null; isVisible = null; }
        public void Dispose()
        {
            owner?.menus.Remove(this);
            Detach();
        }
    }
}
