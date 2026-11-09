using System;
using System.Reflection;
using System.Reflection.Emit;
using CasualtiesTogetherAddons.Networking;

internal static class SteamLobbyReaderTests
{
    private static Type Fixture(string suffix, ulong number)
    {
        var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(
            new AssemblyName("SteamLobbyFixture" + suffix), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Main");
        var idBuilder = module.DefineType("Steamworks.CSteamID",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout, typeof(ValueType));
        idBuilder.DefineField("m_SteamID", typeof(ulong), FieldAttributes.Public);
        var idType = idBuilder.CreateType();
        var lobbyBuilder = module.DefineType("Together.KSteam+Lobby", TypeAttributes.Public);
        lobbyBuilder.DefineField("lobby_steamID", idType, FieldAttributes.Public);
        var lobbyType = lobbyBuilder.CreateType();
        var rootBuilder = module.DefineType("Together.KSteam", TypeAttributes.Public);
        rootBuilder.DefineField("CURRENT_LOBBY", lobbyType, FieldAttributes.Public | FieldAttributes.Static);
        var root = rootBuilder.CreateType();
        var id = Activator.CreateInstance(idType);
        idType.GetField("m_SteamID").SetValue(id, number);
        var lobby = Activator.CreateInstance(lobbyType);
        lobbyType.GetField("lobby_steamID").SetValue(lobby, id);
        root.GetField("CURRENT_LOBBY").SetValue(null, lobby);
        return root;
    }

    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string description)
        {
            if (!condition) throw new Exception("FAIL: " + description);
            checks++;
            Console.WriteLine("PASS: " + description);
        }
        var first = Fixture("A", 41);
        var second = Fixture("B", 82);
        var firstId = first.GetField("CURRENT_LOBBY").FieldType.GetField("lobby_steamID").FieldType;
        var secondId = second.GetField("CURRENT_LOBBY").FieldType.GetField("lobby_steamID").FieldType;
        Check(firstId.FullName == secondId.FullName && firstId != secondId,
            "Regression fixture contains identical Steam ID names from different assemblies");
        var readerA = new SteamLobbyReader(first);
        var readerB = new SteamLobbyReader(second);
        Check(readerA.Read() == 41 && readerB.Read() == 82,
            "Lobby reads bind to the actual mod's Steam ID type rather than a same-named dependency");
        var lobbyField = second.GetField("CURRENT_LOBBY");
        var replacement = Activator.CreateInstance(lobbyField.FieldType);
        lobbyField.SetValue(null, replacement);
        Check(readerB.Read() == 0, "Lobby reader follows the current lobby object after shutdown/replacement");
        lobbyField.SetValue(null, null);
        bool rejectedNull = false;
        try { readerB.Read(); } catch (InvalidOperationException) { rejectedNull = true; }
        Check(rejectedNull, "Missing lobby is rejected before starting a disconnect");
        bool rejectedLayout = false;
        try { new SteamLobbyReader(typeof(string)); } catch (MissingFieldException) { rejectedLayout = true; }
        Check(rejectedLayout, "Incompatible lobby API is rejected during initialization");
        Console.WriteLine("All " + checks + " Steam lobby compatibility checks passed.");
    }
}
