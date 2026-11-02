using System;

namespace CasualtiesTogetherAddons.Features;

// Only observations of the original voice pipeline; never controls recording or sending.
internal sealed class VoiceIndicatorState
{
    private double lastRecord = double.NegativeInfinity;
    private double lastAudio = double.NegativeInfinity;
    private float audio;
    private bool allowed;
    public float Fill { get; private set; }

    public void ObserveRecording(bool result, bool loopback, double now)
    {
        allowed = result && !loopback;
        lastRecord = now;
        if (!allowed) { audio = Fill = 0; lastAudio = double.NegativeInfinity; }
    }

    public void ObserveAudio(float level, bool loopback, double now)
    {
        if (loopback) return;
        audio = float.IsNaN(level) || float.IsInfinity(level) ? 0 : Math.Max(0, Math.Min(1, level));
        lastAudio = now;
    }

    public bool IsOpen(bool voiceEnabled, double now) => voiceEnabled && allowed && now - lastRecord <= 0.25;
    public bool IsVisible(bool modeOff, bool inWorld, bool open) => !modeOff && (inWorld || open);

    public void Animate(bool open, double now, float elapsed)
    {
        if (!open) { Fill = 0; return; }
        var target = now - lastAudio <= 0.15 ? audio : 0;
        // Peak samples already include the mod's microphone gain. No extra amplification.
        var step = Math.Max(0, elapsed) * (target > Fill ? 12f : 3f);
        Fill += Math.Sign(target - Fill) * Math.Min(Math.Abs(target - Fill), step);
    }
}
