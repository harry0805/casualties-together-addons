using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;

internal static class InstalledRejoinTests
{
    internal static void Run(string gamePath, string addonPath)
    {
        var paths = Directory.GetFiles(Path.Combine(gamePath, "CasualtiesUnknown_Data", "Managed"), "*.dll")
            .Concat(Directory.GetFiles(Path.Combine(gamePath, "BepInEx"), "*.dll", SearchOption.AllDirectories))
            .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        ResolveEventHandler resolver = (sender, args) => paths.TryGetValue(new AssemblyName(args.Name).Name, out var path)
            ? Assembly.LoadFrom(path) : null;
        AppDomain.CurrentDomain.AssemblyResolve += resolver;
        try
        {
            var mp = Assembly.LoadFrom(paths["CasualtiesMP"]);
            if (mp.GetName().Version != new Version(5, 3, 1, 0)) throw new Exception("Expected 5.3.1.");
            var menu = mp.GetType("Together.UIMainMenu", true);
            const BindingFlags methods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var caller = menu.GetMethod("_GUI__DirectConnect", methods)
                ?? menu.GetMethod("_GUI_RenderDirectConnectMenu", methods)
                ?? throw new Exception("Missing connection menu renderer.");
            if (PatchProcessor.GetOriginalInstructions(caller).Count(i => i.operand is MethodInfo method
                && method.Name == "_GUI___DirectConnect_DoPasswordField") != 1)
                throw new Exception("Rejoin menu anchor is not drawn exactly once.");
            var addon = Assembly.LoadFrom(addonPath);
            if (addon.GetReferencedAssemblies().Any(a => a.Name.IndexOf("steamworks", StringComparison.OrdinalIgnoreCase) >= 0))
                throw new Exception("Addon still has a hard Steamworks dependency.");
            var readerType = addon.GetType("CasualtiesTogetherAddons.Networking.SteamLobbyReader", true);
            // Validate the actual loaded mod's field chain without reading its static state or initializing Unity.
            Activator.CreateInstance(readerType, new object[] { mp.GetType("Together.KSteam", true) });
            Console.WriteLine("PASS: Actual installed Steam lobby field chain resolves without a hard Steamworks type reference");
            var feature = addon.GetType("CasualtiesTogetherAddons.Networking.RejoinFeature", true);
            var calls = feature.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetMethodBody() != null)
                .SelectMany(m => PatchProcessor.GetOriginalInstructions(m))
                .Select(i => i.operand).OfType<MethodInfo>().ToList();
            foreach (string required in new[] { "ShutdownNetwork", "OnWantToConnect", "OnWantToJoinLobby" })
                if (!calls.Any(m => m.Name == required)) throw new Exception("Missing normal rejoin API: " + required);
            if (calls.Any(m => m.Name.Contains("Custom") || m.Name == "RequestFullBaseline" || m.Name == "CreateNamedWriter"))
                throw new Exception("Rejoin unexpectedly depends on a custom host protocol.");
            if (calls.Any(m => m.DeclaringType == menu && (m.Name == "SetOpen" || m.Name == "IsOpen")))
                throw new Exception("Rejoin still depends on the multiplayer menu's state/query.");
            var game = Assembly.LoadFrom(paths["Assembly-CSharp"]);
            if (AccessTools.PropertyGetter(game.GetType("AdaptiveButton", true), "overlayActive") == null
                || AccessTools.DeclaredMethod(game.GetType("UIUtil", true), "IsPointerOverUIElement", Type.EmptyTypes) == null
                || AccessTools.DeclaredMethod(game.GetType("PlayerCamera", true), "HandleInput", Type.EmptyTypes) == null
                || AccessTools.DeclaredMethod(mp.GetType("Together.PlayerInputPacket", true), "CollectInput", Type.EmptyTypes) == null)
                throw new Exception("Missing shared addon modal input hooks.");
            Console.WriteLine("PASS: Installed 5.3.1 rejoin menu anchor and normal disconnect/direct/Steam connection APIs");
        }
        finally { AppDomain.CurrentDomain.AssemblyResolve -= resolver; }
    }
}
