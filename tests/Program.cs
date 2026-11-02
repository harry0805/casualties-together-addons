using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using BepInEx.Logging;
using CasualtiesTogetherAddons;
using CasualtiesTogetherAddons.Features;
using HarmonyLib;
using Together;

namespace CasualtiesTogetherAddons
{
    // Tests link the production feature files, not the Unity plugin entry point.
    internal static class Plugin { public const string Id = "CasualtiesTogetherAddons.Tests"; }
}

namespace Together
{
    // Models only the installed DLL's voice gate, toggle handling, and mode setter.
    // The tests use real HarmonyX detours and private-field injection from the game installation.
    public static class Voicechat
    {
        public static byte current_microphone_mode = 3;
        public static float my_volume = 1;
        private static bool _toggle_to_talk_toggled;
        private static bool _push_to_talk_pushed;
        public static bool Loading, ServerAllows = true, KeyPressed, SpeechAllowed = true;
        public static bool Toggle => _toggle_to_talk_toggled;
        public static bool IsMicModeToggleToTalk() => current_microphone_mode == 3;
        public static bool VoiceChatIsEnabled() => !Loading && ServerAllows && my_volume > 0 && current_microphone_mode != 0;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SetMicrophoneMode(byte mode) { current_microphone_mode = mode; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool RecordMicUpdate()
        {
            if (!VoiceChatIsEnabled())
            {
                _push_to_talk_pushed = false;
                _toggle_to_talk_toggled = false;
                return false;
            }
            if (IsMicModeToggleToTalk())
            {
                if (KeyPressed) _toggle_to_talk_toggled = !_toggle_to_talk_toggled;
                if (_toggle_to_talk_toggled) _push_to_talk_pushed = true;
                if (!_push_to_talk_pushed) return false;
            }
            if (!SpeechAllowed) return false;
            _push_to_talk_pushed = false; // model consumption of the buffered audio
            return true;
        }
        public static void Reset()
        {
            current_microphone_mode = 3; my_volume = 1;
            Loading = KeyPressed = _toggle_to_talk_toggled = _push_to_talk_pushed = false;
            ServerAllows = SpeechAllowed = true;
        }
    }
}

internal sealed class FailingFeature : FeatureModule
{
    public override string Id => "FailureFixture";
    protected override string Description => "Test rollback";
    protected override void ApplyPatches(Harmony patches)
    {
        patches.Patch(typeof(Voicechat).GetMethod(nameof(Voicechat.SetMicrophoneMode)),
            postfix: new HarmonyMethod(typeof(FailingFeature), nameof(NoOp)));
        throw new NotSupportedException("Simulated incompatible second patch");
    }
    private static void NoOp() { }
}

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        checks++;
        Console.WriteLine("PASS: " + name);
    }
    private static bool Frame(bool key = false)
    {
        Voicechat.KeyPressed = key;
        var result = Voicechat.RecordMicUpdate();
        Voicechat.KeyPressed = false;
        return result;
    }
    private static void NoOp() { }
    private static void Main()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CasualtiesTogetherAddons-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var log = new ManualLogSource("FeatureTests");
        var config = new ConfigFile(Path.Combine(directory, "features.cfg"), false);
        var feature = new MicTogglePersistenceFeature();
        try
        {
            feature.Initialize(config, log, true);
            var method = typeof(Voicechat).GetMethod(nameof(Voicechat.RecordMicUpdate));
            Check(Harmony.GetPatchInfo(method)?.Owners.Contains(Plugin.Id + ".MicTogglePersistence") == true,
                "Production feature installed real Harmony patches");
            Voicechat.Reset();
            Check(!Frame(), "Starts closed");
            Check(Frame(true) && Voicechat.Toggle, "Manual toggle opens mic");
            for (var transition = 0; transition < 3; transition++)
            {
                Voicechat.Loading = true;
                for (var frame = 0; frame < 5; frame++)
                    Check(!Frame() && Voicechat.Toggle, "Loading blocks transmission and preserves open choice");
                Voicechat.Loading = false;
                Check(Frame() && Voicechat.Toggle, "Open choice resumes after transition");
            }
            Check(!Frame(true) && !Voicechat.Toggle, "Manual toggle closes mic");
            Voicechat.Loading = true; Frame(); Voicechat.Loading = false;
            Check(!Frame() && !Voicechat.Toggle, "Closed mic stays closed across transition");
            Frame(true); Voicechat.ServerAllows = false;
            Check(!Frame() && Voicechat.Toggle, "Server restriction cannot be bypassed");
            Voicechat.ServerAllows = true;
            Check(Frame(), "Choice resumes when server allows voice");
            Voicechat.SpeechAllowed = false;
            Check(!Frame(), "Speech restriction cannot be bypassed");
            Voicechat.SpeechAllowed = true;
            Voicechat.my_volume = 0;
            Check(!Frame() && !Voicechat.Toggle, "Volume zero mutes and clears choice");
            Voicechat.my_volume = 1;
            Check(!Frame(), "Raising volume does not reopen mic");
            Frame(true);
            Voicechat.SetMicrophoneMode(1); Voicechat.SetMicrophoneMode(3);
            Check(!Frame() && !Voicechat.Toggle, "Switching away and back clears choice even between frames");
            Frame(true); Voicechat.Loading = true; Voicechat.my_volume = 0; Frame();
            Voicechat.my_volume = 1; Voicechat.Loading = false;
            Check(!Frame(), "Explicit mute during loading cancels remembered choice");
            var otherOwner = new Harmony("tests.other.feature");
            otherOwner.Patch(method, postfix: new HarmonyMethod(typeof(Program), nameof(NoOp)));
            feature.Dispose(); feature.Dispose();
            Check(Harmony.GetPatchInfo(method).Owners.Contains("tests.other.feature"),
                "Feature cleanup leaves other owners intact");
            otherOwner.UnpatchSelf();
            var failing = new FailingFeature();
            failing.Initialize(config, log, true);
            Check(Harmony.GetPatchInfo(typeof(Voicechat).GetMethod(nameof(Voicechat.SetMicrophoneMode)))?.Owners.Contains(
                Plugin.Id + ".FailureFixture") != true, "Failed feature rolls back partially installed patches");
            failing.Dispose();
            Voicechat.Reset(); Frame(true); Voicechat.Loading = true;
            Check(!Frame() && !Voicechat.Toggle, "Disposal restores original reset behavior");
            config.Bind("MicTogglePersistence", "Enabled", true).Value = false;
            feature.Initialize(config, log, true);
            Check(Harmony.GetPatchInfo(method)?.Owners.Contains(Plugin.Id + ".MicTogglePersistence") != true,
                "Feature switch off installs no patches");
            config.Bind("MicTogglePersistence", "Enabled", true).Value = true;
            feature.Initialize(config, log, false);
            Check(Harmony.GetPatchInfo(method)?.Owners.Contains(Plugin.Id + ".MicTogglePersistence") != true,
                "Master switch off installs no patches");
            feature.Initialize(config, log, true);
            Voicechat.Reset();
            Check(!Frame() && !Voicechat.Toggle, "New session starts closed");
            Console.WriteLine($"All {checks} checks passed.");
        }
        finally
        {
            feature.Dispose();
            // Only files created in this unique test directory are removed.
            File.Delete(Path.Combine(directory, "features.cfg"));
            Directory.Delete(directory);
        }
    }
}
