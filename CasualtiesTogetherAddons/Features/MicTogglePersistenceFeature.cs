using System;
using HarmonyLib;
using Together;

namespace CasualtiesTogetherAddons.Features;

internal sealed class MicTogglePersistenceFeature : FeatureModule
{
    public override string Id => "MicTogglePersistence";
    public override string DisplayName => "Persist Toggle to talk";
    protected override string Description =>
        "Remember toggle-to-talk across loading and reconnecting within this game session.";

    protected override void ApplyPatches(Harmony patches)
    {
        var voiceType = typeof(Voicechat);
        RequireField(voiceType, "_toggle_to_talk_toggled", typeof(bool));
        RequireField(voiceType, "_push_to_talk_pushed", typeof(bool));
        RequireField(voiceType, "current_microphone_mode", typeof(byte));
        RequireField(voiceType, "my_volume", typeof(float));

        var record = AccessTools.DeclaredMethod(voiceType, "RecordMicUpdate", Type.EmptyTypes);
        var setMode = AccessTools.DeclaredMethod(voiceType, "SetMicrophoneMode", [typeof(byte)]);
        var voiceEnabled = AccessTools.DeclaredMethod(voiceType, "VoiceChatIsEnabled", Type.EmptyTypes);
        if (record == null || !record.IsStatic || record.ReturnType != typeof(bool) ||
            setMode == null || !setMode.IsStatic || setMode.ReturnType != typeof(void) ||
            voiceEnabled == null || !voiceEnabled.IsStatic || voiceEnabled.ReturnType != typeof(bool))
            throw new NotSupportedException("Installed multiplayer voice-chat methods do not match this feature.");

        patches.Patch(record,
            prefix: new HarmonyMethod(typeof(MicTogglePersistenceFeature), nameof(BeforeRecord)),
            postfix: new HarmonyMethod(typeof(MicTogglePersistenceFeature), nameof(AfterRecord)));
        patches.Patch(setMode,
            postfix: new HarmonyMethod(typeof(MicTogglePersistenceFeature), nameof(AfterSetMode)));
    }

    private static void RequireField(Type type, string name, Type fieldType)
    {
        var field = AccessTools.DeclaredField(type, name);
        if (field == null || !field.IsStatic || field.FieldType != fieldType)
            throw new MissingFieldException(type.FullName, name);
    }

    private static void BeforeRecord(ref bool ____toggle_to_talk_toggled,
        ref bool ____push_to_talk_pushed, out bool __state)
    {
        __state = false;
        if (!Voicechat.IsMicModeToggleToTalk() || !(Voicechat.my_volume > 0))
        {
            // Explicit mute / mode change must not leave an open choice to restore later.
            if (Voicechat.IsMicModeToggleToTalk()) ____push_to_talk_pushed = false;
            ____toggle_to_talk_toggled = false;
            return;
        }

        // Remember only around the original unavailable-voice early return.
        // Always execute the original method, including its recording/transmission gates.
        __state = ____toggle_to_talk_toggled && !Voicechat.VoiceChatIsEnabled();
    }

    private static void AfterRecord(bool __state, ref bool ____toggle_to_talk_toggled)
    {
        if (__state && Voicechat.IsMicModeToggleToTalk() && Voicechat.my_volume > 0)
            ____toggle_to_talk_toggled = true;
    }

    private static void AfterSetMode(ref bool ____toggle_to_talk_toggled,
        ref bool ____push_to_talk_pushed)
    {
        if (Voicechat.IsMicModeToggleToTalk()) return;
        ____toggle_to_talk_toggled = false;
        ____push_to_talk_pushed = false;
    }
}
