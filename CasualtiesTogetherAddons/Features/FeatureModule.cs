using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace CasualtiesTogetherAddons.Features;

// Each feature owns its patches. Never unpatch another feature or the multiplayer mod.
internal abstract class FeatureModule : IDisposable
{
    private Harmony harmony;
    private ManualLogSource logger;
    public abstract string Id { get; }
    public virtual string DisplayName => Id;
    public string Summary => Description;
    public ConfigEntry<bool> Enabled { get; private set; }
    public bool StartupEnabled { get; private set; }
    public bool IsActive => harmony != null;
    public bool RestartRequired => Enabled != null && Enabled.Value != StartupEnabled;
    protected abstract string Description { get; }

    public void Initialize(ConfigFile config, ManualLogSource log)
    {
        logger = log;
        Enabled = config.Bind(Id, "Enabled", true,
            Description + " Requires a game restart.");
        StartupEnabled = Enabled.Value;
        if (!StartupEnabled)
        {
            log.LogInfo($"[{Id}] Disabled.");
            return;
        }

        harmony = new Harmony(Plugin.Id + "." + Id);
        try
        {
            ApplyPatches(harmony);
            log.LogInfo($"[{Id}] Enabled.");
        }
        catch (Exception error)
        {
            // Roll back even if installation failed after applying only some patches.
            Dispose();
            log.LogError($"[{Id}] Could not enable; feature skipped. {error}");
        }
    }

    protected abstract void ApplyPatches(Harmony patches);

    public void Dispose()
    {
        if (harmony == null) return;
        try { harmony.UnpatchSelf(); }
        catch (Exception error) { logger.LogError($"[{Id}] Could not remove patches: {error}"); }
        harmony = null;
    }
}
