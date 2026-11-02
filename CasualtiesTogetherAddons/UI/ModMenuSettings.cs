using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Logging;
using CasualtiesTogetherAddons.Features;
using HarmonyLib;
using Together;
using UnityEngine;

namespace CasualtiesTogetherAddons.UI;

internal sealed class ModMenuSettings : IDisposable
{
    private const string LocaleKey = "CasualtiesTogetherAddons.Settings";
    private static ModMenuSettings instance;
    private readonly FeatureModule[] features;
    private readonly ManualLogSource logger;
    private readonly Harmony harmony = new Harmony(Plugin.Id + ".ModMenu");
    private UIMainMenu.SettingsTab tab;

    public ModMenuSettings(FeatureModule[] features, ManualLogSource logger)
    {
        this.features = features;
        this.logger = logger;
    }

    public void Initialize()
    {
        try
        {
            var registry = AccessTools.DeclaredField(typeof(UIMainMenu.SettingsTab), "tabs")?.GetValue(null)
                as List<UIMainMenu.SettingsTab>;
            var render = AccessTools.DeclaredMethod(typeof(UIMainMenu), "_GUI_RenderSettingsMenu", new[] { typeof(Rect) });
            var translate = AccessTools.DeclaredMethod(typeof(Lang), "Get",
                new[] { typeof(string).MakeByRefType(), typeof(bool).MakeByRefType() });
            if (registry == null || render == null || translate == null)
                throw new NotSupportedException("Installed multiplayer mod-menu API is not supported.");

            tab = new UIMainMenu.SettingsTab { name = LocaleKey, gui_func = Draw, on_reset = ResetDefaults };
            // The constructor auto-registers for the separate native settings screen.
            // We already integrate there; keep this tab exclusive to the mod menu.
            registry.Remove(tab);
            instance = this;
            harmony.Patch(translate, prefix: new HarmonyMethod(typeof(ModMenuSettings), nameof(TranslateTitle)));
            harmony.Patch(render, transpiler: new HarmonyMethod(typeof(ModMenuSettings), nameof(InsertSidebarEntry)));
        }
        catch (Exception error)
        {
            Dispose();
            logger.LogError($"Could not add the mod-menu settings section. {error}");
        }
    }

    // Reuse the original sidebar's own drawing/selection delegate after Bug Report.
    internal static IEnumerable<CodeInstruction> InsertSidebarEntry(IEnumerable<CodeInstruction> source)
    {
        var code = new List<CodeInstruction>(source);
        var bugReport = AccessTools.DeclaredField(typeof(UIMainMenu), "_bugreport_setting_tab");
        var invoke = typeof(Action<UIMainMenu.SettingsTab>).GetMethod("Invoke");
        int insertion = -1;
        for (int i = 2; i + 1 < code.Count; i++)
        {
            if (code[i].Calls(invoke) && code[i - 1].LoadsField(bugReport))
            {
                if (insertion != -1) throw new NotSupportedException("Ambiguous sidebar insertion point.");
                insertion = i;
            }
        }
        if (insertion == -1 || !IsLoadLocal(code[insertion - 2].opcode))
            throw new NotSupportedException("Could not locate the multiplayer settings sidebar.");

        // All branches after the optional Bug Report row must also draw our row.
        var loadRenderer = new CodeInstruction(code[insertion - 2].opcode, code[insertion - 2].operand);
        // The installed HarmonyX exposes framework Label types; use the non-generic
        // list interface to retain branch targets without binding to its mscorlib.
        var labels = AccessTools.Field(typeof(CodeInstruction), "labels");
        var previousLabels = (System.Collections.IList)labels.GetValue(code[insertion + 1]);
        var newLabels = (System.Collections.IList)labels.GetValue(loadRenderer);
        foreach (var label in previousLabels) newLabels.Add(label);
        previousLabels.Clear();
        code.InsertRange(insertion + 1, new[]
        {
            loadRenderer,
            new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(typeof(ModMenuSettings), nameof(DrawSidebarEntry)))
        });
        return code;
    }

    private static bool IsLoadLocal(OpCode code) => code == OpCodes.Ldloc || code == OpCodes.Ldloc_S
        || code == OpCodes.Ldloc_0 || code == OpCodes.Ldloc_1 || code == OpCodes.Ldloc_2 || code == OpCodes.Ldloc_3;

    private static void DrawSidebarEntry(Action<UIMainMenu.SettingsTab> renderTab)
    {
        if (instance?.tab != null) renderTab(instance.tab);
    }

    private static bool TranslateTitle(string __0, ref string __result)
    {
        if (__0 != LocaleKey) return true;
        __result = PluginInfo.Name;
        return false;
    }

    private void Draw(Rect rect)
    {
        float scale = UIMainMenu.GetMenuUIScale();
        int baseSize = GUI.skin.label.fontSize;
        if (baseSize <= 0) baseSize = Mathf.RoundToInt(20 * scale);

        // Clone styles rather than changing the shared multiplayer-menu skin.
        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = baseSize,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft
        };
        titleStyle.normal.textColor = Color.white;
        var descriptionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(12, Mathf.RoundToInt(baseSize * 0.78f)),
            wordWrap = true,
            alignment = TextAnchor.UpperLeft
        };
        descriptionStyle.normal.textColor = new Color(0.72f, 0.75f, 0.80f);

        GUILayout.Space(10 * scale);
        foreach (var feature in features)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(FeatureSettingsActions.Title(feature), titleStyle, GUILayout.Width(rect.width * 0.7f));
            bool selected = GUILayout.Toggle(feature.Enabled.Value, "");
            GUILayout.EndHorizontal();
            if (selected != feature.Enabled.Value)
                FeatureSettingsActions.SetEnabled(feature, selected);

            // Indented supporting text keeps each explanation attached to its option.
            GUILayout.Space(3 * scale);
            GUILayout.BeginHorizontal();
            GUILayout.Space(12 * scale);
            GUILayout.BeginVertical(GUILayout.MaxWidth(Mathf.Max(100, rect.width - 24 * scale)));
            GUILayout.Label(feature.Summary, descriptionStyle);
            if (feature.RestartRequired)
            {
                GUILayout.Space(5 * scale);
                if (GUILayout.Button("Restart Game", GUILayout.ExpandWidth(false)))
                    FeatureSettingsActions.Restart(feature);
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(16 * scale);
        }
    }
    private void ResetDefaults()
    {
        foreach (var feature in features)
        {
            feature.Enabled.Value = (bool)feature.Enabled.DefaultValue;
            feature.Enabled.ConfigFile.Save();
        }
    }

    public void Dispose()
    {
        if (instance == this) instance = null;
        harmony.UnpatchSelf();
        if (tab != null && UIMainMenu.cur_settings_tab == tab)
            UIMainMenu.cur_settings_tab = UIMainMenu.ALL_SETTINGS.Count > 0 ? UIMainMenu.ALL_SETTINGS[0] : null;
        tab = null;
    }
}
