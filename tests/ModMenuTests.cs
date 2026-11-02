using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using BepInEx.Logging;
using CasualtiesTogetherAddons.Features;
using CasualtiesTogetherAddons.UI;
using UnityEngine;

namespace CasualtiesTogetherAddons
{
    internal static class PluginInfo
    {
        public const string Name = "Casualties Together Addons";
        public const string Version = "0.0.1";
    }
}

namespace Together
{
    public static class Lang
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Get(in string key, in bool quiet) => "untranslated:" + key;
    }

    public static class UIMainMenu
    {
        public class SettingsTab
        {
            internal static List<SettingsTab> tabs = new List<SettingsTab>();
            public string name;
            public Action<Rect> gui_func;
            public Action on_reset;
            public SettingsTab() { tabs.Add(this); }
        }
        public static List<SettingsTab> ALL_SETTINGS = new List<SettingsTab> { new SettingsTab { name = "General" } };
        public static SettingsTab cur_settings_tab;
        internal static SettingsTab _bugreport_setting_tab = new SettingsTab { name = "Bug Report" };
        public static List<string> Drawn = new List<string>();
        public static SettingsTab Last;
        public static float GetMenuUIScale() => 1;
        private static void DrawEntry(SettingsTab tab)
        {
            Last = tab;
            bool quiet = false;
            Drawn.Add(tab.name == "CasualtiesTogetherAddons.Settings" ? Lang.Get(tab.name, quiet) : tab.name);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void _GUI_RenderSettingsMenu(Rect rect)
        {
            Action<SettingsTab> draw = DrawEntry;
            if (_bugreport_setting_tab != null) draw(_bugreport_setting_tab);
            // Merge target for both the present and absent Bug Report paths.
            GC.KeepAlive(draw);
        }
        public static void Render() => _GUI_RenderSettingsMenu(default(Rect));
    }
}

internal static class ModMenuTests
{
    public static void Run(MicTogglePersistenceFeature feature, ConfigFile config, ManualLogSource log, Action<bool, string> check)
    {
        int registered = Together.UIMainMenu.SettingsTab.tabs.Count;
        var menu = new ModMenuSettings(new FeatureModule[] { feature }, log);
        try
        {
            menu.Initialize();
            Together.UIMainMenu.Render();
            check(string.Join("|", Together.UIMainMenu.Drawn) == "Bug Report|Casualties Together Addons",
                "Sidebar adds correctly named section after Bug Report using real transpiler");
            check(Together.UIMainMenu.SettingsTab.tabs.Count == registered,
                "Overlay registration does not duplicate the native settings section");
            var tab = Together.UIMainMenu.Last;
            feature.Enabled.Value = false;
            tab.on_reset();
            check(feature.Enabled.Value && new ConfigFile(config.ConfigFilePath, false).Bind("MicTogglePersistence", "Enabled", false).Value,
                "Overlay reset restores and saves the shared feature default");
            Together.UIMainMenu._bugreport_setting_tab = null;
            Together.UIMainMenu.Drawn.Clear();
            Together.UIMainMenu.Render();
            check(string.Join("|", Together.UIMainMenu.Drawn) == "Casualties Together Addons",
                "Sidebar also works when Bug Report is absent (branch labels preserved)");
            bool quiet = false;
            check(Together.Lang.Get("OtherKey", quiet) == "untranslated:OtherKey",
                "Section title translation leaves other keys unchanged");
            Together.UIMainMenu.cur_settings_tab = tab;
            menu.Dispose();
            check(Together.UIMainMenu.cur_settings_tab == Together.UIMainMenu.ALL_SETTINGS[0],
                "Cleanup returns selected addon section to a regular tab");
            Together.UIMainMenu.Drawn.Clear();
            Together.UIMainMenu.Render();
            check(Together.UIMainMenu.Drawn.Count == 0, "Cleanup removes sidebar hook");
        }
        finally { menu.Dispose(); }
    }
}
