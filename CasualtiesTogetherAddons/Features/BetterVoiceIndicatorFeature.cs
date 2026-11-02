using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using CasualtiesTogetherUtils;
using HarmonyLib;
using Together;
using UnityEngine;

namespace CasualtiesTogetherAddons.Features;

internal sealed class BetterVoiceIndicatorFeature : FeatureModule
{
    public override string Id => "BetterVoiceIndicator";
    public override string DisplayName => "Better voice indicator";
    protected override string Description => "Replace voice-chat text with a pixel microphone showing outgoing voice activity.";
    internal static VoiceIndicatorState State { get; private set; } = new VoiceIndicatorState();
    private static double lastDraw;
    private static FieldInfo loopback;
    private static VoiceIndicatorSprites sprites;

    protected override void ApplyPatches(Harmony patches)
    {
        var record = AccessTools.DeclaredMethod(typeof(Voicechat), "RecordMicUpdate", Type.EmptyTypes);
        var audio = AccessTools.DeclaredMethod(typeof(Voicechat), "Client_SendMicDataFrame", [typeof(float[]), typeof(bool)]);
        var gui = AccessTools.DeclaredMethod(typeof(Multiplayer), "_GUI_RenderMainGUI", Type.EmptyTypes);
        loopback = AccessTools.DeclaredField(typeof(Voicechat), "ENABLE_LOCAL_LOOPBACK");
        if (record == null || !record.IsStatic || record.ReturnType != typeof(bool) ||
            audio == null || !audio.IsStatic || audio.ReturnType != typeof(void) ||
            gui == null || !gui.IsStatic || gui.ReturnType != typeof(void) ||
            loopback == null || !loopback.IsStatic || loopback.FieldType != typeof(bool))
            throw new NotSupportedException("Installed multiplayer voice indicator methods do not match.");
        State = new VoiceIndicatorState();
        lastDraw = 0;
        sprites = new VoiceIndicatorSprites();
        patches.Patch(record, postfix: new HarmonyMethod(typeof(BetterVoiceIndicatorFeature), nameof(AfterRecord)));
        patches.Patch(audio, postfix: new HarmonyMethod(typeof(BetterVoiceIndicatorFeature), nameof(AfterAudio)));
        patches.Patch(gui, transpiler: new HarmonyMethod(typeof(BetterVoiceIndicatorFeature), nameof(ReplaceText)));
    }

    protected override void ReleaseResources()
    {
        sprites?.Dispose();
        sprites = null;
        State = new VoiceIndicatorState();
        lastDraw = 0;
    }

    private static void AfterRecord(bool __result, bool ___ENABLE_LOCAL_LOOPBACK) =>
        State.ObserveRecording(__result, ___ENABLE_LOCAL_LOOPBACK, Time.realtimeSinceStartupAsDouble);

    private static void AfterAudio(bool ___ENABLE_LOCAL_LOOPBACK) =>
        State.ObserveAudio(Voicechat.my_output_volume, ___ENABLE_LOCAL_LOOPBACK, Time.realtimeSinceStartupAsDouble);

    internal static IEnumerable<CodeInstruction> ReplaceText(IEnumerable<CodeInstruction> source)
    {
        var code = new List<CodeInstruction>(source);
        var volume = AccessTools.Field(typeof(Voicechat), nameof(Voicechat.my_output_volume));
        int header = -1, text = -1, active = -1;
        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].opcode != OpCodes.Ldstr) continue;
            if (Equals(code[i].operand, "Voicechat current volume level: ")) text = i;
            if (Equals(code[i].operand, "Voicechat active.")) active = i;
        }
        if (text >= 6 && active > text)
            for (int i = Math.Max(0, text - 20); i + 2 < text; i++)
                if (code[i].LoadsField(volume) && code[i + 1].opcode == OpCodes.Ldc_R4 &&
                    Equals(code[i + 1].operand, 0f) &&
                    (code[i + 2].opcode == OpCodes.Ble_Un_S || code[i + 2].opcode == OpCodes.Ble_Un))
                {
                    if (header != -1) throw new NotSupportedException("Ambiguous voice indicator branch.");
                    header = i;
                }
        // Existing label: new Rect(20, y, 300, 20). Reuse its actual y local.
        if (header < 0 || !IsLoadLocal(code[text - 4].opcode) ||
            code[text - 1].opcode != OpCodes.Newobj || code[text - 2].opcode != OpCodes.Ldc_R4 ||
            !Equals(code[text - 3].operand, 300f))
            throw new NotSupportedException("Installed multiplayer voice text layout does not match.");

        // Draw at the original text slot, then return zero to skip just its voice labels.
        // Preserve all existing labels, branches, and other multiplayer HUD content.
        code[header].opcode = code[text - 4].opcode;
        code[header].operand = code[text - 4].operand;
        code.Insert(header + 1, new CodeInstruction(OpCodes.Call,
            AccessTools.Method(typeof(BetterVoiceIndicatorFeature), nameof(DrawAndSuppress))));
        return code;
    }

    private static bool IsLoadLocal(OpCode op) => op == OpCodes.Ldloc || op == OpCodes.Ldloc_S ||
        op == OpCodes.Ldloc_0 || op == OpCodes.Ldloc_1 || op == OpCodes.Ldloc_2 || op == OpCodes.Ldloc_3;

    private static float DrawAndSuppress(float originalY)
    {
        if (Event.current.type != EventType.Repaint) return 0;
        var now = Time.realtimeSinceStartupAsDouble;
        bool open = State.IsOpen(Voicechat.VoiceChatIsEnabled() && !(bool)loopback.GetValue(null), now);
        State.Animate(open, now, (float)Math.Min(0.1, now - lastDraw));
        lastDraw = now;
        bool inWorld = Util.IsInWorld();
        if (!State.IsVisible(Voicechat.IsMicModeDisabled(), inWorld, open)) return 0;

        float scale = Math.Max(0.5f, UIBullshit.uiScale);
        int pixel = Math.Max(1, (int)Math.Round(2 * scale));
        var rect = VoiceIndicatorLayout.Place(Screen.width, Screen.height, pixel, scale, inWorld, originalY);
        DrawMic(rect, pixel, open, State.Fill);
        return 0;
    }

    private static void DrawMic(Rect rect, int pixel, bool open, float fill)
    {
        var previous = GUI.color;
        try
        {
            GUI.color = Color.white;
            // PNGs include one pixel of keyline padding around the original 16x20 footprint.
            GUI.DrawTexture(new Rect(rect.x - pixel, rect.y - pixel, 18 * pixel, 22 * pixel),
                sprites.Get(open, fill));
        }
        finally { GUI.color = previous; }
    }
}
