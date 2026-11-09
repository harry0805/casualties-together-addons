using BepInEx;
using BepInEx.Bootstrap;
using CasualtiesTogetherAddons.Features;
using CasualtiesTogetherAddons.UI;
using CasualtiesTogetherAddons.Networking;
using CasualtiesTogetherUtils;

namespace CasualtiesTogetherAddons;

[BepInProcess("CasualtiesUnknown.exe")]
[BepInPlugin(Id, PluginInfo.Name, PluginInfo.Version)]
[BepInDependency("CasualtiesMP", BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Id = PluginInfo.Id;
    private FeatureModule[] features = [];
    private MultiplayerSettings settings;
    private ModMenuSettings modMenuSettings;
    private bool initialized;
    private AddonMenuInput menuInput;

    private static bool MultiplayerIsActive()
    {
        return Chainloader.PluginInfos.TryGetValue("CasualtiesMP", out var info)
            && info.Instance is Together.Plugin multiplayer
            && multiplayer != null && multiplayer.isActiveAndEnabled
            && Together.Plugin.SUCCESFULLY_INITIALIZED
            && !Together.Plugin.FORCE_DISABLE_MP_MOD
            && !PlayerPrefsExtended.GetBool("CasualtiesMP_FORCE_DISABLE_MP_MOD", false);
    }

    private void Awake()
    {
        if (!MultiplayerIsActive())
        {
            Logger.LogInfo("Casualties Together is inactive or failed to initialize; addons will not initialize.");
            enabled = false;
            return;
        }

        try { menuInput = new AddonMenuInput(); }
        catch (System.Exception error) { Logger.LogError($"Addon menu input could not initialize: {error}"); }
        features = [new MicTogglePersistenceFeature(), new BetterVoiceIndicatorFeature(), new RejoinFeature(menuInput)];
        foreach (var feature in features)
            feature.Initialize(Config, Logger);
        settings = new MultiplayerSettings(features, Logger);
        settings.Initialize();
        modMenuSettings = new ModMenuSettings(features, Logger);
        modMenuSettings.Initialize();
        initialized = true;
        Logger.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} loaded. Feature switches require a game restart.");
    }

    private void Update()
    {
        if (!initialized) return;
        if (MultiplayerIsActive())
        {
            foreach (var feature in features)
                if (feature is RejoinFeature rejoin) rejoin.Tick();
            return;
        }
        Shutdown();
        enabled = false;
        Logger.LogInfo("Casualties Together was deactivated; addon patches and settings have been removed.");
    }

    private void OnGUI()
    {
        if (!initialized) return;
        foreach (var feature in features)
            if (feature is RejoinFeature rejoin) rejoin.DrawPopup();
    }

    private void Shutdown()
    {
        initialized = false;
        try
        {
            try { modMenuSettings?.Dispose(); }
            finally { settings?.Dispose(); }
        }
        finally
        {
            modMenuSettings = null;
            settings = null;
            try { foreach (var feature in features) feature.Dispose(); }
            finally
            {
                menuInput?.Dispose();
                menuInput = null;
            }
        }
    }

    private void OnDestroy() => Shutdown();
}
