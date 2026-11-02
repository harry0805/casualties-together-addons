using BepInEx;
using CasualtiesTogetherAddons.Features;

namespace CasualtiesTogetherAddons;

[BepInProcess("CasualtiesUnknown.exe")]
[BepInPlugin(Id, PluginInfo.Name, PluginInfo.Version)]
[BepInDependency("CasualtiesMP", BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Id = PluginInfo.Id;
    private readonly FeatureModule[] features = [new MicTogglePersistenceFeature()];

    private void Awake()
    {
        var enabled = Config.Bind("General", "Enabled", true,
            "Enable add-on features. All switches take effect after restarting the game.");
        Logger.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} loaded. Feature switches require a game restart.");
        foreach (var feature in features)
            feature.Initialize(Config, Logger, enabled.Value);
    }

    private void OnDestroy()
    {
        foreach (var feature in features)
            feature.Dispose();
    }
}
