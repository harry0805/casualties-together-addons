using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BepInEx.Configuration;
using BepInEx.Logging;
using CasualtiesTogetherAddons.Features;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesTogetherUtils
{
    public static class Util { public static bool IsInWorld() => true; }
}

namespace Together
{
    public static class UIBullshit { public static float uiScale = 1; }
    public static class Multiplayer
    {
        // Tests never execute Unity GUI, but Harmony compiles the real replacement against this layout.
        private static void _GUI_RenderMainGUI()
        {
            float y = 20;
            if (Voicechat.my_output_volume > 0)
            {
                if (DateTime.Now.Ticks > 0)
                {
                    GUI.Label(new Rect(20, y, 300, 20), "Voicechat current volume level: " + Voicechat.my_output_volume.ToString("F2"));
                    y += 20;
                }
                else
                {
                    GUI.Label(new Rect(20, y, 200, 20), "Voicechat active.");
                    y += 20;
                }
            }
        }
    }
}

internal static class VoiceIndicatorTests
{
    internal static void Run(ConfigFile config, ManualLogSource log, Action<bool, string> check)
    {
        var state = new VoiceIndicatorState();
        check(!state.IsOpen(true, 0), "Indicator starts closed before voice pipeline runs");
        state.ObserveRecording(true, false, 1);
        state.Animate(true, 1, 0.1f);
        check(state.IsOpen(true, 1) && state.Fill == 0, "Open but silent is distinct from mute");
        state.ObserveAudio(0.7f, false, 1);
        state.Animate(true, 1, 0.1f);
        check(Math.Abs(state.Fill - 0.7f) < 0.001, "Fill uses outgoing peak without changing audio gain");
        state.ObserveRecording(false, false, 1.01);
        check(!state.IsOpen(true, 1.01) && state.Fill == 0, "Blocked recording immediately clears open state and level");
        state.ObserveAudio(1, false, 1.02);
        check(!state.IsOpen(true, 1.02), "Flushing a final audio buffer does not reopen closed mic");
        state.ObserveRecording(true, true, 2);
        check(!state.IsOpen(true, 2), "Local microphone test does not claim outgoing voice is open");
        state.ObserveRecording(true, false, 3);
        check(!state.IsOpen(false, 3), "Current server/local gate overrides prior recording result");
        check(!state.IsOpen(true, 3.3), "Stale recording result cannot leave indicator open");
        state.ObserveAudio(1, false, 3);
        state.Animate(true, 3, 0.1f);
        state.Animate(true, 3.2, 0.1f);
        check(state.Fill > 0 && state.Fill < 1, "Missing audio frames decay instead of holding stale loudness");
        state.Animate(false, 3.21, 0);
        check(state.Fill == 0, "Mute clears fill without waiting for smoothing");
        check(!state.IsVisible(true, true, true) && !state.IsVisible(true, false, true), "VC Off hides indicator in world and menu");
        check(state.IsVisible(false, true, false) && !state.IsVisible(false, false, false) && state.IsVisible(false, false, true),
            "Closed mic appears only in world; open mic appears in menu too");

        foreach (var size in new[] { new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(3440, 1440) })
        {
            var rect = VoiceIndicatorLayout.Place(size.x, size.y, 2, 1, true, 20);
            check(Math.Abs(rect.center.x - 150) <= 0.5f &&
                Math.Abs(rect.center.y - (size.y - 140)) <= 0.5f,
                "Microphone stays beside second bottom-left HUD button at " + size);
        }
        var menuRect = VoiceIndicatorLayout.Place(1920, 1080, 2, 1, false, 130);
        check(menuRect.x < 40 && menuRect.y == 130, "Main menu reuses original text row near top left");

        var owner = new BetterVoiceIndicatorFeature();
        try
        {
            owner.Initialize(config, log);
            check(owner.IsActive, "Better voice indicator installs against voice and GUI fixtures");
            var record = AccessTools.Method(typeof(Together.Voicechat), "RecordMicUpdate");
            var audio = AccessTools.Method(typeof(Together.Voicechat), "Client_SendMicDataFrame");
            var info = Harmony.GetPatchInfo(audio);
            check(info.Prefixes.Count == 0 && info.Transpilers.Count == 0 && info.Postfixes.Count == 1,
                "Audio method has only an observing postfix; original send path remains intact");
            check(Harmony.GetPatchInfo(record).Postfixes.Any(p => p.owner == "CasualtiesTogetherAddons.Tests.BetterVoiceIndicator"),
                "Indicator observes original recording result alongside persistence feature");
            owner.Dispose();
            check(!owner.IsActive && !owner.RestartRequired && !BetterVoiceIndicatorFeature.State.IsOpen(true, 0),
                "Disposal resets indicator state");
            check(Harmony.GetPatchInfo(audio) == null || Harmony.GetPatchInfo(audio).Owners.Count == 0,
                "Disposal removes audio observer");
            owner.Enabled.Value = false;
            owner.Initialize(config, log);
            check(!owner.IsActive, "Independent indicator switch disables its patches");
        }
        finally { owner.Dispose(); }

        bool rejected = false;
        try { BetterVoiceIndicatorFeature.ReplaceText(new List<CodeInstruction> { new CodeInstruction(OpCodes.Ret) }).ToList(); }
        catch (NotSupportedException) { rejected = true; }
        check(rejected, "Unexpected multiplayer GUI fails safely instead of hiding unrelated HUD");
    }
}
