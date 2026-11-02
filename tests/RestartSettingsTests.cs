using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using CasualtiesTogetherAddons.Features;
using CasualtiesTogetherAddons.UI;

namespace Together
{
    // Observe restart requests without starting or stopping any process during tests.
    public static class Plugin
    {
        public static int RestartCount;
        public static Action BeforeRestart;
        public static void RestartGame() { BeforeRestart?.Invoke(); RestartCount++; }
    }
}

internal static class RestartSettingsTests
{
    public static void Run(MicTogglePersistenceFeature feature, ConfigFile config, ManualLogSource log, Action<bool, string> check)
    {
        feature.Dispose();
        feature.Enabled.Value = true;
        feature.Initialize(config, log);
        Together.Plugin.RestartCount = 0;
        check(!feature.RestartRequired && FeatureSettingsActions.Title(feature) == feature.DisplayName,
            "Unchanged setting has no restart-required label or button state");
        FeatureSettingsActions.Restart(feature);
        check(Together.Plugin.RestartCount == 0, "Unchanged option cannot request restart");
        FeatureSettingsActions.SetEnabled(feature, false);
        check(feature.RestartRequired && FeatureSettingsActions.Title(feature).EndsWith(" (restart required)"),
            "Shared menu actions mark changed option as restart required");
        check(!new ConfigFile(config.ConfigFilePath, false).Bind("MicTogglePersistence", "Enabled", true).Value,
            "Shared checkbox action persists preference");
        config.SaveOnConfigSet = false;
        config.Bind("AnotherFeature", "Enabled", true).Value = false;
        bool savedBeforeRestart = false;
        Together.Plugin.BeforeRestart = () =>
        {
            var saved = new ConfigFile(config.ConfigFilePath, false);
            savedBeforeRestart = !saved.Bind("AnotherFeature", "Enabled", true).Value;
        };
        try
        {
            FeatureSettingsActions.Restart(feature);
            check(Together.Plugin.RestartCount == 1 && savedBeforeRestart,
                "Restart button flushes all settings before invoking original restart flow");
        }
        finally
        {
            Together.Plugin.BeforeRestart = null;
            config.SaveOnConfigSet = true;
        }
        FeatureSettingsActions.SetEnabled(feature, true);
        check(!feature.RestartRequired && FeatureSettingsActions.Title(feature) == feature.DisplayName,
            "Reverting option clears restart-required label and button state");
        FeatureSettingsActions.Restart(feature);
        check(Together.Plugin.RestartCount == 1, "Reverted option cannot request unnecessary restart");
    }
}
