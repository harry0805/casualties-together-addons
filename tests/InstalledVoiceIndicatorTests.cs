using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

// Optional IL compatibility check. The game's netstandard 2.1 recording code cannot
// be JIT-compiled by this .NET Framework harness; real detours use the fixture tests.
internal static class InstalledVoiceIndicatorTests
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
            var multiplayer = Assembly.LoadFrom(paths["CasualtiesMP"]);
            var addon = Assembly.LoadFrom(addonPath);
            var featureType = addon.GetType("CasualtiesTogetherAddons.Features.BetterVoiceIndicatorFeature", true);
            var gui = AccessTools.DeclaredMethod(multiplayer.GetType("Together.Multiplayer", true), "_GUI_RenderMainGUI");
            var original = PatchProcessor.GetOriginalInstructions(gui);
            var replaced = ((IEnumerable<CodeInstruction>)AccessTools.Method(featureType, "ReplaceText")
                .Invoke(null, new object[] { original.Select(c => new CodeInstruction(c)).ToList() })).ToList();
            if (replaced.Count != original.Count + 1 || replaced.Count(c => c.opcode == OpCodes.Call &&
                c.operand is MethodInfo method && method.Name == "DrawAndSuppress") != 1)
                throw new Exception("Installed HUD replacement did not insert exactly one drawing call.");
            Console.WriteLine("PASS: Installed multiplayer HUD accepts production voice text replacement");
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= resolver;
        }
    }
}
