using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mono.Cecil;

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
            var version = mp.GetName().Version;
            var menu = mp.GetType("Together.UIMainMenu", true);
            // Read metadata directly: the new mod references default interface methods
            // that .NET Framework cannot resolve through Harmony's runtime IL reader.
            using var mpMetadata = AssemblyDefinition.ReadAssembly(paths["CasualtiesMP"]);
            var menuMetadata = mpMetadata.MainModule.GetType("Together.UIMainMenu");
            var caller = menuMetadata.Methods.FirstOrDefault(m => m.Name == "_GUI__DirectConnect")
                ?? menuMetadata.Methods.FirstOrDefault(m => m.Name == "_GUI_RenderDirectConnectMenu")
                ?? throw new Exception("Missing connection menu renderer.");
            if (caller.Body.Instructions.Count(i => i.Operand is MethodReference method
                && method.Name == "_GUI___DirectConnect_DoPasswordField") != 1)
                throw new Exception("Rejoin menu anchor is not drawn exactly once.");
            var addon = Assembly.LoadFrom(addonPath);
            void RequireMethod(string type, string name, string result, params string[] parameters)
            {
                if (!mpMetadata.MainModule.GetType(type).Methods.Any(m => m.IsStatic && m.Name == name
                    && m.ReturnType.FullName == result && m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(parameters)))
                    throw new Exception("Incompatible installed rejoin API: " + type + "." + name);
            }
            RequireMethod("Together.Multiplayer", "ShutdownNetwork", "System.Void");
            RequireMethod("Together.TransportSteamworks", "OnWantToJoinLobby", "System.Boolean", "System.UInt64");
            RequireMethod("Together.TransportLiteNetLib", "OnWantToConnect", "System.Boolean", "System.String", "Together.Net/NetType");
            RequireMethod("Together.UIMainMenu", "_GUI___DirectConnect_DoPasswordField", "System.Void",
                "UnityEngine.Rect", "System.Single", "System.Single", "System.Single");
            RequireMethod("Together.UIBullshit", "_GUI_DoTexturesForMPUserInterface", "System.Void");
            Console.WriteLine("PASS: Installed connection and menu API signatures match structural requirements");
            if (addon.GetReferencedAssemblies().Any(a => a.Name.IndexOf("steamworks", StringComparison.OrdinalIgnoreCase) >= 0))
                throw new Exception("Addon still has a hard Steamworks dependency.");
            var readerType = addon.GetType("CasualtiesTogetherAddons.Networking.SteamLobbyReader", true);
            // Validate the actual loaded mod's field chain without reading its static state or initializing Unity.
            Activator.CreateInstance(readerType, new object[] { mp.GetType("Together.KSteam", true) });
            Console.WriteLine("PASS: Actual installed Steam lobby field chain resolves without a hard Steamworks type reference");
            using var addonMetadata = AssemblyDefinition.ReadAssembly(addonPath);
            var feature = addonMetadata.MainModule.GetType("CasualtiesTogetherAddons.Networking.RejoinFeature");
            var calls = feature.Methods.Where(m => m.HasBody)
                .SelectMany(m => m.Body.Instructions)
                .Select(i => i.Operand).OfType<MethodReference>().ToList();
            foreach (string required in new[] { "ShutdownNetwork", "OnWantToConnect", "OnWantToJoinLobby" })
                if (!calls.Any(m => m.Name == required)) throw new Exception("Missing normal rejoin API: " + required);
            if (calls.Any(m => m.Name.Contains("Custom") || m.Name == "RequestFullBaseline" || m.Name == "CreateNamedWriter"))
                throw new Exception("Rejoin unexpectedly depends on a custom host protocol.");
            if (calls.Any(m => m.DeclaringType.FullName == menu.FullName && (m.Name == "SetOpen" || m.Name == "IsOpen")))
                throw new Exception("Rejoin still depends on the multiplayer menu's state/query.");
            var game = Assembly.LoadFrom(paths["Assembly-CSharp"]);
            if (AccessTools.PropertyGetter(game.GetType("AdaptiveButton", true), "overlayActive") == null
                || AccessTools.DeclaredMethod(game.GetType("UIUtil", true), "IsPointerOverUIElement", Type.EmptyTypes) == null
                || AccessTools.DeclaredMethod(game.GetType("PlayerCamera", true), "HandleInput", Type.EmptyTypes) == null
                || AccessTools.DeclaredMethod(mp.GetType("Together.PlayerInputPacket", true), "CollectInput", Type.EmptyTypes) == null)
                throw new Exception("Missing shared addon modal input hooks.");
            Console.WriteLine($"PASS: Installed {version} rejoin menu anchor and normal disconnect/direct/Steam connection APIs");
        }
        finally { AppDomain.CurrentDomain.AssemblyResolve -= resolver; }
    }
}
