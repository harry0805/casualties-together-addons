using CasualtiesTogetherAddons.Features;

namespace CasualtiesTogetherAddons.UI;

// Both menu renderers use the same pending-state label and save/restart actions.
internal static class FeatureSettingsActions
{
    public static string Title(FeatureModule feature) =>
        feature.DisplayName + (feature.RestartRequired ? " (restart required)" : "");

    public static void SetEnabled(FeatureModule feature, bool enabled)
    {
        if (feature.Enabled.Value == enabled) return;
        feature.Enabled.Value = enabled;
        feature.Enabled.ConfigFile.Save();
    }

    public static void Restart(FeatureModule feature)
    {
        if (!feature.RestartRequired) return;
        // All feature options share this config; flush all saved choices before restarting.
        feature.Enabled.ConfigFile.Save();
        Together.Plugin.RestartGame();
    }
}
