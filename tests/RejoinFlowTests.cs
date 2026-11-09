using System;
using CasualtiesTogetherAddons.Networking;

internal static class RejoinFlowTests
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string description)
        {
            if (!condition) throw new Exception("FAIL: " + description);
            checks++;
            Console.WriteLine("PASS: " + description);
        }
        var flow = new RejoinFlow();
        RejoinAction Step(float t, bool transport = false, bool menu = false, bool connected = false, bool loaded = false)
            => flow.Step(t, transport, menu, connected, loaded);
        Check(flow.Begin(0) && !flow.Begin(0), "Rejoin accepts one queued attempt");
        Check(Step(0, true) == RejoinAction.Disconnect, "Rejoin requests a real disconnect first");
        Check(Step(1, false, true) == RejoinAction.None, "Rejoin allows time for remote disconnect cleanup");
        Check(Step(2) == RejoinAction.None, "Rejoin waits for a ready main menu");
        Check(Step(3, false, true) == RejoinAction.Connect, "Rejoin connects once after cleanup and menu readiness");
        Check(Step(4, true, true) == RejoinAction.None, "Pending connection does not repeat the connect action");
        Check(Step(5, true, false, true) == RejoinAction.None && flow.Phase == RejoinPhase.Loading,
            "Transport connection is not reported as a fully loaded rejoin");
        Check(Step(6, true, false, true, true) == RejoinAction.Completed && !flow.Active,
            "Normal world readiness completes the rejoin");
        Check(Step(100) == RejoinAction.None, "Completed rejoin never retries automatically");
        Check(!flow.Begin(7) && flow.Begin(10), "Rejoin cooldown prevents rapid repeated attempts");
        flow.Cancel();
        Check(!flow.Active && Step(11, true) == RejoinAction.None, "Cancel before disconnect leaves no queued action");
        flow.Begin(20); Step(20, true);
        Check(Step(22, true, true) == RejoinAction.Cancelled, "A manually started connection cancels the pending rejoin");
        flow.Begin(30); Step(30, true);
        Check(Step(50) == RejoinAction.Failed && !flow.Active, "Main-menu wait is bounded without starting a connection");
        flow.Begin(60); Step(60, true); Step(62, false, true);
        Check(Step(63) == RejoinAction.Failed && !flow.Active, "Rejected or aborted connection stops the attempt");
        flow.Begin(80); Step(80, true); Step(82, false, true);
        Check(Step(142, true) == RejoinAction.ConnectTimedOut, "Connection timeout requests owned-connection cleanup");
        flow.Begin(150); Step(150, true); Step(152, false, true); Step(153, true, false, true);
        Check(Step(273, true, false, true) == RejoinAction.LoadTimedOut,
            "Slow world loading stops tracking without requesting a disconnect");
        flow.Begin(280); Step(280, true); Step(282, false, true); Step(283, true, false, true);
        Check(Step(284) == RejoinAction.Failed, "Disconnect during loading stops the attempt");
        Check(Step(400, true, true) == RejoinAction.None, "Failed rejoin does not retry or interfere with another session");
        Console.WriteLine("All " + checks + " rejoin flow checks passed.");
    }
}
