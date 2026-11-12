using System;
using CasualtiesTogetherAddons.Networking;

internal static class RejoinCompatibilityTests
{
    private sealed class Compatible
    {
        public static string Password = "";
        public static bool Connect(string endpoint) => endpoint.Length > 0;
        public static int UnrelatedNewFeature() => 42;
    }

    private sealed class Changed
    {
        public static readonly string Password = "";
        public static int Connect(string endpoint) => 0;
        public bool InstanceConnect(string endpoint) => true;
    }

    internal static void Run()
    {
        int checks = 0;
        void Reject(Action action)
        {
            try { action(); }
            catch (NotSupportedException) { checks++; return; }
            throw new Exception("Incompatible rejoin API was accepted.");
        }

        RejoinCompatibility.RequireStaticMethod(typeof(Compatible), "Connect", typeof(bool), typeof(string));
        RejoinCompatibility.RequireStaticField(typeof(Compatible), "Password", typeof(string));
        checks += 2; // Extra upstream methods do not affect compatibility.
        Reject(() => RejoinCompatibility.RequireStaticMethod(typeof(Compatible), "Removed", typeof(void)));
        Reject(() => RejoinCompatibility.RequireStaticMethod(typeof(Compatible), "Connect", typeof(bool), typeof(ulong)));
        Reject(() => RejoinCompatibility.RequireStaticMethod(typeof(Changed), "Connect", typeof(bool), typeof(string)));
        Reject(() => RejoinCompatibility.RequireStaticMethod(typeof(Changed), "InstanceConnect", typeof(bool), typeof(string)));
        Reject(() => RejoinCompatibility.RequireStaticField(typeof(Compatible), "Removed", typeof(string)));
        Reject(() => RejoinCompatibility.RequireStaticField(typeof(Compatible), "Password", typeof(int)));
        Reject(() => RejoinCompatibility.RequireStaticField(typeof(Changed), "Password", typeof(string)));
        Console.WriteLine($"All {checks} rejoin API compatibility checks passed.");
    }
}
