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

            var voice = multiplayer.GetType("Together.Voicechat", true);
            foreach (var name in new[] { "_toggle_to_talk_toggled", "_push_to_talk_pushed", "ENABLE_LOCAL_LOOPBACK" })
            {
                var field = AccessTools.DeclaredField(voice, name);
                if (field == null || !field.IsStatic || field.FieldType != typeof(bool))
                    throw new Exception("Missing compatible voice field: " + name);
            }
            foreach (var name in new[] { "RecordMicUpdate", "VoiceChatIsEnabled" })
            {
                var method = AccessTools.DeclaredMethod(voice, name, Type.EmptyTypes);
                if (method == null || !method.IsStatic || method.ReturnType != typeof(bool))
                    throw new Exception("Missing compatible voice method: " + name);
            }
            var setMode = AccessTools.DeclaredMethod(voice, "SetMicrophoneMode", new[] { typeof(byte) });
            var audio = AccessTools.DeclaredMethod(voice, "Client_SendMicDataFrame", new[] { typeof(float[]), typeof(bool) });
            if (setMode == null || !setMode.IsStatic || setMode.ReturnType != typeof(void)
                || audio == null || !audio.IsStatic || audio.ReturnType != typeof(void)
                || AccessTools.DeclaredField(voice, "current_microphone_mode")?.FieldType != typeof(byte)
                || AccessTools.DeclaredField(voice, "my_volume")?.FieldType != typeof(float))
                throw new Exception("Installed voice mode/audio API changed.");
            var record = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(voice, "RecordMicUpdate"));
            foreach (var name in new[] { "_toggle_to_talk_toggled", "_push_to_talk_pushed" })
                if (!record.Any(c => c.opcode == OpCodes.Stsfld && Equals(c.operand, AccessTools.DeclaredField(voice, name))))
                    throw new Exception("Recording no longer writes expected state: " + name);
            Console.WriteLine("PASS: Installed voice patch signatures, injected fields, and recording state writes");

            var menu = multiplayer.GetType("Together.UIMainMenu", true);
            var render = AccessTools.DeclaredMethod(menu, "_GUI_RenderSettingsMenu");
            var sidebar = addon.GetType("CasualtiesTogetherAddons.UI.ModMenuSettings", true);
            var sidebarOriginal = PatchProcessor.GetOriginalInstructions(render);
            var sidebarReplaced = ((IEnumerable<CodeInstruction>)AccessTools.Method(sidebar, "InsertSidebarEntry")
                .Invoke(null, new object[] { sidebarOriginal.Select(c => new CodeInstruction(c)).ToList() })).ToList();
            if (sidebarReplaced.Count != sidebarOriginal.Count + 2
                || sidebarReplaced.Count(c => c.operand is MethodInfo m && m.Name == "DrawSidebarEntry") != 1)
                throw new Exception("Installed settings sidebar replacement failed.");
            var native = multiplayer.GetType("Together.SettingsMenu_Start_MultiplayerPatch", true);
            if (AccessTools.DeclaredMethod(native, "BuildMPSettingsUI", Type.EmptyTypes) == null
                || AccessTools.DeclaredField(native, "DeactivateButton") == null
                || AccessTools.DeclaredField(menu.GetNestedType("SettingsTab"), "tabs") == null)
                throw new Exception("Installed native settings hooks changed.");
            Console.WriteLine("PASS: Installed settings sidebar accepts production transpiler and native settings hooks resolve");
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= resolver;
        }
    }
}
