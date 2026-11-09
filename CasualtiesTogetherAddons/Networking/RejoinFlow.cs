namespace CasualtiesTogetherAddons.Networking;

internal enum RejoinPhase { Idle, Queued, WaitingForMenu, Connecting, Loading }
internal enum RejoinAction { None, Disconnect, Connect, Completed, Cancelled, Failed, ConnectTimedOut, LoadTimedOut }

// One attempt, driven by Update rather than a coroutine on scene-owned objects.
internal sealed class RejoinFlow
{
    public RejoinPhase Phase { get; private set; }
    public bool Active => Phase != RejoinPhase.Idle;
    public float CooldownUntil { get; private set; }
    private float deadline, reconnectAfter;

    public bool Begin(float now)
    {
        if (Active || now < CooldownUntil) return false;
        Phase = RejoinPhase.Queued;
        CooldownUntil = now + 10f;
        return true;
    }

    public void Cancel() => Phase = RejoinPhase.Idle;

    public RejoinAction Step(float now, bool hasTransport, bool menuReady, bool connected, bool worldReady)
    {
        switch (Phase)
        {
            case RejoinPhase.Queued:
                Phase = RejoinPhase.WaitingForMenu;
                deadline = now + 20f;
                reconnectAfter = now + 2f;
                return RejoinAction.Disconnect;
            case RejoinPhase.WaitingForMenu:
                if (hasTransport) return End(RejoinAction.Cancelled); // Another connection supersedes this attempt.
                if (now >= deadline) return End(RejoinAction.Failed);
                if (menuReady && now >= reconnectAfter)
                {
                    Phase = RejoinPhase.Connecting;
                    deadline = now + 60f;
                    return RejoinAction.Connect;
                }
                break;
            case RejoinPhase.Connecting:
                if (!hasTransport) return End(RejoinAction.Failed);
                if (connected)
                {
                    Phase = RejoinPhase.Loading;
                    deadline = now + 120f;
                    if (worldReady) return End(RejoinAction.Completed);
                }
                else if (now >= deadline) return End(RejoinAction.ConnectTimedOut);
                break;
            case RejoinPhase.Loading:
                if (!hasTransport || !connected) return End(RejoinAction.Failed);
                if (worldReady) return End(RejoinAction.Completed);
                if (now >= deadline) return End(RejoinAction.LoadTimedOut);
                break;
        }
        return RejoinAction.None;
    }

    private RejoinAction End(RejoinAction action) { Cancel(); return action; }
}
