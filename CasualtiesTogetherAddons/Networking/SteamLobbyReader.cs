using System;
using System.Reflection;

namespace CasualtiesTogetherAddons.Networking;

// Bind through the loaded mod's actual field types. Two Steamworks assemblies can expose
// identically named CSteamID structs that are not interchangeable in a compiled field reference.
internal sealed class SteamLobbyReader
{
    private readonly FieldInfo currentLobby, lobbyId, rawId;
    public SteamLobbyReader(Type steamType)
    {
        const BindingFlags fields = BindingFlags.Public | BindingFlags.NonPublic;
        currentLobby = steamType.GetField("CURRENT_LOBBY", fields | BindingFlags.Static)
            ?? throw new MissingFieldException("CURRENT_LOBBY");
        lobbyId = currentLobby.FieldType.GetField("lobby_steamID", fields | BindingFlags.Instance)
            ?? throw new MissingFieldException("lobby_steamID");
        rawId = lobbyId.FieldType.GetField("m_SteamID", fields | BindingFlags.Instance)
            ?? throw new MissingFieldException("m_SteamID");
        if (rawId.FieldType != typeof(ulong)) throw new NotSupportedException("Unsupported Steam lobby ID representation.");
    }

    public ulong Read()
    {
        object lobby = currentLobby.GetValue(null);
        if (lobby == null) throw new InvalidOperationException("No current Steam lobby.");
        object id = lobbyId.GetValue(lobby);
        if (id == null) throw new InvalidOperationException("No current Steam lobby ID.");
        return (ulong)rawId.GetValue(id);
    }
}
