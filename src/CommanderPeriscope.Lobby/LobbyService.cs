using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Game;
using CommanderPeriscope.Domain.Maps;

namespace CommanderPeriscope.Lobby;

/// <summary>
/// Orchestrates lobby lifecycle: create/join, role claiming, ready-checks, host-only options and controls, and
/// the disconnect/pause/restart/rematch flows. Raises <see cref="LobbyChanged"/> so Blazor Server components —
/// which already hold a live circuit per user — can re-render on change without any extra transport of their own.
/// </summary>
public sealed class LobbyService(ILobbyRepository repository)
{
    public event Action<string>? LobbyChanged;

    public Lobby CreateLobby(string hostPlayerId, string hostDisplayName, string mapId)
    {
        string code;
        do
        {
            code = LobbyCodeGenerator.Generate();
        }
        while (repository.Exists(code));

        var lobby = new Lobby(code, hostPlayerId, new LobbyOptions { MapId = mapId });
        lobby.Players[hostPlayerId] = new Player(hostPlayerId, hostDisplayName);
        repository.Add(lobby);
        return lobby;
    }

    public Lobby Join(string code, string playerId, string displayName)
    {
        var lobby = Require(code);
        lock (lobby.Lock)
        {
            if (lobby.Players.TryGetValue(playerId, out var existing))
            {
                existing.DisplayName = displayName;
                existing.ConnectionStatus = PlayerConnectionStatus.Connected;
            }
            else
            {
                if (lobby.Status != LobbyStatus.Open)
                {
                    throw new InvalidOperationException("This lobby has already started — you can't join mid-game.");
                }

                // No hard cap here on purpose: a lobby can carry more connected players than its configured
                // capacity (e.g. right after the host shrinks team sizes) — anyone still unseated gets dropped
                // when the host starts the game instead (see StartGame).
                lobby.Players[playerId] = new Player(playerId, displayName);
            }
        }

        Notify(lobby);
        return lobby;
    }

    public void ClaimRole(string code, string playerId, TeamColor team, CrewRole role)
    {
        var lobby = Require(code);
        lock (lobby.Lock)
        {
            RequireOpen(lobby);
            RequirePlayer(lobby, playerId);

            var roster = lobby.RosterFor(team);
            if (!roster.ActiveRoles.Contains(role))
            {
                throw new InvalidOperationException($"{role} is not a separately claimable seat on a {roster.ConfiguredSize}-player team.");
            }

            var currentOccupant = roster.OccupantOf(role);
            if (currentOccupant is not null && currentOccupant != playerId)
            {
                throw new InvalidOperationException($"{role} is already claimed.");
            }

            // Release whichever seat (on either team) this player currently holds before claiming the new one.
            lobby.Red.Vacate(playerId);
            lobby.Blue.Vacate(playerId);
            roster.Claim(role, playerId);
        }

        Notify(lobby);
    }

    public void SetReady(string code, string playerId, bool ready)
    {
        var lobby = Require(code);
        lock (lobby.Lock)
        {
            var roster = lobby.RosterOf(playerId) ?? throw new InvalidOperationException("Claim a role before readying up.");
            roster.SetReady(playerId, ready);
        }

        Notify(lobby);
    }

    public void RenameSubmarine(string code, string playerId, TeamColor team, string name)
    {
        var lobby = Require(code);
        lock (lobby.Lock)
        {
            var roster = lobby.RosterFor(team);
            if (roster.OccupantOf(CrewRole.Captain) != playerId)
            {
                throw new InvalidOperationException("Only that team's Captain can rename the submarine.");
            }

            var trimmed = name.Trim();
            if (trimmed.Length == 0 || trimmed.Length > SubmarineNameGenerator.MaxLength)
            {
                throw new ArgumentException($"Submarine name must be 1–{SubmarineNameGenerator.MaxLength} characters.", nameof(name));
            }

            roster.SubmarineName = trimmed;
        }

        Notify(lobby);
    }

    public void UpdateOptions(string code, string hostPlayerId, LobbyOptions options)
    {
        var lobby = Require(code);
        lock (lobby.Lock)
        {
            RequireOpen(lobby);
            RequireHost(lobby, hostPlayerId);

            if (!LobbyOptions.ValidTeamSizes.Contains(options.RedTeamSize) || !LobbyOptions.ValidTeamSizes.Contains(options.BlueTeamSize))
            {
                throw new ArgumentException("Team size must be 2, 3, or 4.", nameof(options));
            }

            EnsureShrinkIsSafe(lobby.Red, options.RedTeamSize);
            EnsureShrinkIsSafe(lobby.Blue, options.BlueTeamSize);

            lobby.Options = options;
            lobby.Red.ConfiguredSize = options.RedTeamSize;
            lobby.Blue.ConfiguredSize = options.BlueTeamSize;
        }

        Notify(lobby);
    }

    public void StartGame(string code, string hostPlayerId)
    {
        var lobby = Require(code);
        lock (lobby.Lock)
        {
            RequireOpen(lobby);
            RequireHost(lobby, hostPlayerId);

            if (!lobby.Red.AllActiveRolesClaimedAndReady() || !lobby.Blue.AllActiveRolesClaimedAndReady())
            {
                throw new InvalidOperationException("Every claimed role on both teams must be ready before starting.");
            }

            // Any connected-but-unassigned player never got a seat — they're dropped from the session.
            var seated = lobby.Red.DistinctPlayerIds.Concat(lobby.Blue.DistinctPlayerIds).ToHashSet();
            foreach (var playerId in lobby.Players.Keys.Where(id => !seated.Contains(id)).ToList())
            {
                lobby.Players.Remove(playerId);
            }

            var loadedMap = MapFixture.LoadReference(lobby.Options.MapId);
            lobby.Game = new GameEngine(loadedMap.Map, lobby.Options.ToGameOptions());
            lobby.Status = LobbyStatus.InProgress;
            lobby.PauseReason = PauseReason.None;
            lobby.CurrentOrderTeam = TeamColor.Red;
        }

        Notify(lobby);
    }

    public void Pause(string code, string hostPlayerId)
    {
        var lobby = Require(code);
        lock (lobby.Lock)
        {
            RequireHost(lobby, hostPlayerId);

            // A host can pause a running game, or escalate an auto-pause (from a disconnect) into a manual one
            // so a reconnect doesn't auto-resume it out from under them.
            var canPause = lobby.Status == LobbyStatus.InProgress
                || (lobby.Status == LobbyStatus.Paused && lobby.PauseReason == PauseReason.Disconnect);
            if (!canPause)
            {
                throw new InvalidOperationException("Only an in-progress (or disconnect-paused) game can be paused.");
            }

            lobby.Status = LobbyStatus.Paused;
            lobby.PauseReason = PauseReason.Host;
        }

        Notify(lobby);
    }

    public void Resume(string code, string hostPlayerId)
    {
        var lobby = Require(code);
        lock (lobby.Lock)
        {
            RequireHost(lobby, hostPlayerId);
            if (lobby.Status != LobbyStatus.Paused || lobby.PauseReason != PauseReason.Host)
            {
                throw new InvalidOperationException("Only a host-initiated pause can be resumed manually.");
            }

            lobby.Status = LobbyStatus.InProgress;
            lobby.PauseReason = PauseReason.None;
        }

        Notify(lobby);
    }

    /// <summary>Host-only Restart, and the end-of-game Rematch, are the same operation: same code, same roster, fresh game state, ready reset.</summary>
    public void RestartToLobby(string code, string hostPlayerId)
    {
        var lobby = Require(code);
        lock (lobby.Lock)
        {
            RequireHost(lobby, hostPlayerId);

            lobby.Status = LobbyStatus.Open;
            lobby.PauseReason = PauseReason.None;
            lobby.Game = null;

            foreach (var playerId in lobby.Players.Keys)
            {
                lobby.Red.SetReadyIfSeated(playerId, false);
                lobby.Blue.SetReadyIfSeated(playerId, false);
            }
        }

        Notify(lobby);
    }

    public void HandleDisconnect(string code, string playerId)
    {
        var lobby = Require(code);
        lock (lobby.Lock)
        {
            if (!lobby.Players.TryGetValue(playerId, out var player))
            {
                return;
            }

            player.ConnectionStatus = PlayerConnectionStatus.Disconnected;

            if (lobby.Status == LobbyStatus.InProgress && lobby.Options.PauseOnDisconnect)
            {
                lobby.Status = LobbyStatus.Paused;
                lobby.PauseReason = PauseReason.Disconnect;
            }
        }

        Notify(lobby);
    }

    public void HandleReconnect(string code, string playerId)
    {
        var lobby = Require(code);
        lock (lobby.Lock)
        {
            if (!lobby.Players.TryGetValue(playerId, out var player))
            {
                return;
            }

            player.ConnectionStatus = PlayerConnectionStatus.Connected;

            var allReconnected = lobby.Players.Values.All(p => p.ConnectionStatus == PlayerConnectionStatus.Connected);
            if (lobby.Status == LobbyStatus.Paused && lobby.PauseReason == PauseReason.Disconnect && allReconnected)
            {
                lobby.Status = LobbyStatus.InProgress;
                lobby.PauseReason = PauseReason.None;
            }
        }

        Notify(lobby);
    }

    public Lobby? TryGet(string code) => repository.TryGet(code);

    private Lobby Require(string code) =>
        repository.TryGet(code) ?? throw new InvalidOperationException($"No lobby found with code '{code}'.");

    private static void RequireOpen(Lobby lobby)
    {
        if (lobby.Status != LobbyStatus.Open)
        {
            throw new InvalidOperationException("The lobby options are locked once the game has started.");
        }
    }

    private static void RequireHost(Lobby lobby, string playerId)
    {
        if (lobby.HostPlayerId != playerId)
        {
            throw new InvalidOperationException("Only the host can do that.");
        }
    }

    private static void RequirePlayer(Lobby lobby, string playerId)
    {
        if (!lobby.Players.ContainsKey(playerId))
        {
            throw new InvalidOperationException("You haven't joined this lobby.");
        }
    }

    private static void EnsureShrinkIsSafe(TeamRoster roster, int newSize)
    {
        var newActiveRoles = TeamRoster.RolesForSize(newSize);
        var orphaned = roster.ActiveRoles.Where(r => !newActiveRoles.Contains(r) && roster.IsRoleClaimed(r));
        if (orphaned.Any())
        {
            throw new InvalidOperationException("Vacate the roles that would no longer exist at that team size before shrinking it.");
        }
    }

    private void Notify(Lobby lobby) => LobbyChanged?.Invoke(lobby.Code);
}
