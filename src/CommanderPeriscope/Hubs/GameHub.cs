using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Game;
using CommanderPeriscope.Domain.Maps;
using CommanderPeriscope.Domain.Systems;
using CommanderPeriscope.Domain.Weapons;
using CommanderPeriscope.Lobby;
using CommanderPeriscope.Visibility;
using Microsoft.AspNetCore.SignalR;
using LobbySession = CommanderPeriscope.Lobby.Lobby;

namespace CommanderPeriscope.Hubs;

/// <summary>
/// The real-time transport for an in-progress game: clients send intents, this hub validates them against
/// the domain engine and role/turn rules, then broadcasts the resulting scoped views via the visibility
/// layer. It never hands out raw <see cref="GameEngine"/> state — every outbound message goes
/// through <see cref="ScopedBroadcastPlanner"/> first.
///
/// The pre-game lobby deliberately does NOT use this hub — it has no hidden information, so it's driven by
/// plain injected services and Blazor Server's own circuit. This hub exists specifically because the game
/// does have hidden information, and SignalR groups are what let the server enforce that structurally instead
/// of trusting the client to filter what it renders.
/// </summary>
public sealed class GameHub(LobbyService lobbyService, GameConnectionRegistry registry) : Hub
{
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (registry.TryGet(Context.ConnectionId, out var info))
        {
            lobbyService.HandleDisconnect(info.LobbyCode, info.PlayerId);
            registry.Remove(Context.ConnectionId);

            // A disconnect can change who's currently absorbing this player's role (see ActingOccupantOf) —
            // re-sync every other live connection in the lobby so the absorbing teammate's client starts
            // receiving that role's private broadcasts immediately, not just after their next reconnect.
            await ResyncTeammateGroupsAsync(info.LobbyCode);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Joins this connection to every visibility-scoped group this player belongs to, then seeds it with exactly (and only) the views those groups carry.</summary>
    public async Task JoinGame(string lobbyCode, string playerId)
    {
        var lobby = RequireLobby(lobbyCode);
        var engine = RequireGame(lobby);
        RequireSeat(lobby, playerId);

        registry.Set(Context.ConnectionId, lobbyCode, playerId);
        lobbyService.HandleReconnect(lobbyCode, playerId);

        await SyncConnectionGroupsAndSeedAsync(lobby, engine, Context.ConnectionId, playerId);

        // This player reconnecting can hand a role's duties back from whichever teammate was absorbing
        // it — re-sync the rest of the team so that teammate's client stops receiving it.
        await ResyncTeammateGroupsAsync(lobbyCode, except: Context.ConnectionId);
    }

    /// <summary>
    /// Adds one connection to every SignalR group it's currently entitled to (own team, and every role it
    /// either holds directly or is absorbing from a disconnected teammate per <see cref="TeamRoster.ActingOccupantOf"/>),
    /// removes it from any role group it no longer qualifies for, then seeds it with exactly that state.
    /// </summary>
    private async Task SyncConnectionGroupsAndSeedAsync(LobbySession lobby, GameEngine engine, string connectionId, string playerId)
    {
        var roster = lobby.RosterOf(playerId);
        if (roster is null)
        {
            return;
        }

        var lobbyCode = lobby.Code;
        var myGroups = new HashSet<string>
        {
            GroupNames.AllPlayers(lobbyCode),
            GroupNames.Team(lobbyCode, roster.Color),
        };

        await Groups.AddToGroupAsync(connectionId, GroupNames.AllPlayers(lobbyCode));
        await Groups.AddToGroupAsync(connectionId, GroupNames.Team(lobbyCode, roster.Color));

        foreach (var role in Enum.GetValues<CrewRole>())
        {
            var group = GroupNames.Role(lobbyCode, roster.Color, role);
            if (roster.ActingOccupantOf(role, pid => IsConnected(lobby, pid)) == playerId)
            {
                myGroups.Add(group);
                await Groups.AddToGroupAsync(connectionId, group);
            }
            else
            {
                await Groups.RemoveFromGroupAsync(connectionId, group);
            }
        }

        foreach (var broadcast in ScopedBroadcastPlanner.PlanFullStateRefresh(lobbyCode, engine))
        {
            if (myGroups.Contains(broadcast.Group))
            {
                await Clients.Client(connectionId).SendAsync("ScopedUpdate", broadcast.Payload.GetType().Name, broadcast.Payload);
            }
        }
    }

    /// <summary>Re-syncs every other live connection in the lobby after a disconnect/reconnect may have changed who's absorbing which role.</summary>
    private async Task ResyncTeammateGroupsAsync(string lobbyCode, string? except = null)
    {
        var lobby = lobbyService.TryGet(lobbyCode);
        if (lobby?.Game is null)
        {
            return;
        }

        foreach (var (connectionId, playerId) in registry.ConnectionsFor(lobbyCode))
        {
            if (connectionId == except)
            {
                continue;
            }

            await SyncConnectionGroupsAndSeedAsync(lobby, lobby.Game, connectionId, playerId);
        }
    }

    /// <summary>Both Captains privately mark their starting X before "Dive!" — never broadcast beyond their own Captain group.</summary>
    public async Task SetStartingPosition(string lobbyCode, string playerId, Coordinate start)
    {
        var (_, engine, roster) = RequireAuthorizedAction(lobbyCode, playerId, CrewRole.Captain);

        await ExecuteAsync(lobbyCode, engine,
            mutate: () => engine.SetStartingPosition(roster.Color, start),
            planBroadcasts: () => [ScopedBroadcastPlanner.PlanCaptainRefresh(lobbyCode, engine, roster.Color)]);
    }

    public async Task Move(string lobbyCode, string playerId, Direction direction)
    {
        var (lobby, engine, roster) = RequireAuthorizedAction(lobbyCode, playerId, CrewRole.Captain);
        RequireTurn(lobby, engine, roster.Color);

        await ExecuteAsync(lobbyCode, engine,
            mutate: () => engine.Move(roster.Color, direction),
            onSuccess: () => AdvanceTurn(lobby, engine, roster.Color),
            planBroadcasts: () => ScopedBroadcastPlanner.PlanMove(lobbyCode, engine, roster.Color, direction));
    }

    public async Task ActivateSilence(string lobbyCode, string playerId, Direction direction, int steps)
    {
        var (lobby, engine, roster) = RequireAuthorizedAction(lobbyCode, playerId, CrewRole.Captain);
        RequireTurn(lobby, engine, roster.Color);

        await ExecuteAsync(lobbyCode, engine,
            mutate: () => engine.ActivateSilence(roster.Color, direction, steps),
            onSuccess: () => AdvanceTurn(lobby, engine, roster.Color),
            planBroadcasts: () =>
            [
                ScopedBroadcastPlanner.PlanCaptainRefresh(lobbyCode, engine, roster.Color),
                ScopedBroadcastPlanner.PlanFirstMateRefresh(lobbyCode, engine, roster.Color),
            ]);
    }

    public async Task PlaceMine(string lobbyCode, string playerId, Coordinate target)
    {
        var (lobby, engine, roster) = RequireAuthorizedAction(lobbyCode, playerId, CrewRole.Captain);
        RequireTurn(lobby, engine, roster.Color);

        await ExecuteAsync(lobbyCode, engine,
            mutate: () => engine.PlaceMine(roster.Color, target),
            onSuccess: () => AdvanceTurn(lobby, engine, roster.Color),
            planBroadcasts: () =>
            [
                ScopedBroadcastPlanner.PlanCaptainRefresh(lobbyCode, engine, roster.Color),
                ScopedBroadcastPlanner.PlanFirstMateRefresh(lobbyCode, engine, roster.Color),
            ]);
    }

    /// <summary>Detonating a placed mine "can happen at any time" per the rules — not gated by whose turn it is.</summary>
    public async Task DetonateMine(string lobbyCode, string playerId, Coordinate mineLocation)
    {
        var (_, engine, roster) = RequireAuthorizedAction(lobbyCode, playerId, CrewRole.Captain);

        MineResult? result = null;
        await ExecuteAsync(lobbyCode, engine,
            mutate: () => result = engine.DetonateMine(roster.Color, mineLocation),
            planBroadcasts: () =>
            [
                ScopedBroadcastPlanner.PlanCaptainRefresh(lobbyCode, engine, roster.Color),
                ScopedBroadcastPlanner.PlanCaptainRefresh(lobbyCode, engine, roster.Color.Opponent()),
                .. ScopedBroadcastPlanner.PlanStatusBars(lobbyCode, engine),
                ScopedBroadcastPlanner.PlanToast(lobbyCode, DescribeWeaponResult(roster.Color, "mine", mineLocation, result!.Impact)),
            ]);
    }

    public async Task FireTorpedo(string lobbyCode, string playerId, Coordinate impact)
    {
        var (lobby, engine, roster) = RequireAuthorizedAction(lobbyCode, playerId, CrewRole.Captain);
        RequireTurn(lobby, engine, roster.Color);

        TorpedoResult? result = null;
        await ExecuteAsync(lobbyCode, engine,
            mutate: () => result = engine.FireTorpedo(roster.Color, impact),
            onSuccess: () => AdvanceTurn(lobby, engine, roster.Color),
            planBroadcasts: () =>
            [
                ScopedBroadcastPlanner.PlanCaptainRefresh(lobbyCode, engine, roster.Color),
                ScopedBroadcastPlanner.PlanCaptainRefresh(lobbyCode, engine, roster.Color.Opponent()),
                ScopedBroadcastPlanner.PlanFirstMateRefresh(lobbyCode, engine, roster.Color),
                .. ScopedBroadcastPlanner.PlanStatusBars(lobbyCode, engine),
                ScopedBroadcastPlanner.PlanToast(lobbyCode, DescribeWeaponResult(roster.Color, "torpedo", impact, result!.Impact)),
            ]);
    }

    /// <summary>The First Mate's response to every Captain order: marks one empty box on a gauge of their choice. Not itself an order — doesn't consume a turn.</summary>
    public async Task MarkGauge(string lobbyCode, string playerId, SystemType type)
    {
        var (_, engine, roster) = RequireAuthorizedAction(lobbyCode, playerId, CrewRole.FirstMate);

        await ExecuteAsync(lobbyCode, engine,
            mutate: () => engine.MarkGauge(roster.Color, type),
            planBroadcasts: () => ScopedBroadcastPlanner.PlanGaugeMark(lobbyCode, engine, roster.Color));
    }

    public async Task RequestSonar(string lobbyCode, string playerId)
    {
        var (lobby, engine, roster) = RequireAuthorizedAction(lobbyCode, playerId, CrewRole.FirstMate);
        RequireTurn(lobby, engine, roster.Color);

        await ExecuteAsync(lobbyCode, engine,
            mutate: () => engine.RequestSonar(roster.Color),
            onSuccess: () => AdvanceTurn(lobby, engine, roster.Color),
            planBroadcasts: () => ScopedBroadcastPlanner.PlanGaugeMark(lobbyCode, engine, roster.Color));
    }

    public async Task RequestDrone(string lobbyCode, string playerId)
    {
        var (lobby, engine, roster) = RequireAuthorizedAction(lobbyCode, playerId, CrewRole.FirstMate);
        RequireTurn(lobby, engine, roster.Color);

        await ExecuteAsync(lobbyCode, engine,
            mutate: () => engine.RequestDrone(roster.Color),
            onSuccess: () => AdvanceTurn(lobby, engine, roster.Color),
            planBroadcasts: () => ScopedBroadcastPlanner.PlanGaugeMark(lobbyCode, engine, roster.Color));
    }

    /// <summary>Addresses a breakdown item by its fixed position in the Engineer's board — that list never reorders or resizes.</summary>
    public async Task MarkBreakdown(string lobbyCode, string playerId, int breakdownIndex)
    {
        var (_, engine, roster) = RequireAuthorizedAction(lobbyCode, playerId, CrewRole.Engineer);

        var breakdowns = engine.SubFor(roster.Color).Engineer.Breakdowns;
        if (breakdownIndex < 0 || breakdownIndex >= breakdowns.Count)
        {
            throw new HubException("Invalid breakdown index.");
        }

        var item = breakdowns[breakdownIndex];

        await ExecuteAsync(lobbyCode, engine,
            mutate: () => engine.MarkBreakdown(roster.Color, item),
            planBroadcasts: () =>
            [
                .. ScopedBroadcastPlanner.PlanBreakdownMark(lobbyCode, engine, roster.Color),
                .. ScopedBroadcastPlanner.PlanStatusBars(lobbyCode, engine),
            ]);
    }

    public async Task Surface(string lobbyCode, string playerId)
    {
        var (lobby, engine, roster) = RequireAuthorizedAction(lobbyCode, playerId, CrewRole.Captain);
        RequireTurn(lobby, engine, roster.Color);

        await ExecuteAsync(lobbyCode, engine,
            mutate: () => engine.Surface(roster.Color),
            onSuccess: () => AdvanceTurn(lobby, engine, roster.Color),
            planBroadcasts: () =>
            [
                ScopedBroadcastPlanner.PlanCaptainRefresh(lobbyCode, engine, roster.Color),
                .. ScopedBroadcastPlanner.PlanSurface(lobbyCode, engine, roster.Color),
                ScopedBroadcastPlanner.PlanRepairSessionRefresh(lobbyCode, engine),
                ScopedBroadcastPlanner.PlanToast(lobbyCode, $"{roster.Color} surfaced in sector {engine.SectorFor(engine.SubFor(roster.Color))}."),
            ]);
    }

    public async Task SecureRealTimeRepairSection(string lobbyCode, string playerId, CrewRole role)
    {
        var (_, engine, roster) = RequireAuthorizedAction(lobbyCode, playerId, role);

        await ExecuteAsync(lobbyCode, engine,
            mutate: () => engine.SecureRealTimeRepairSection(roster.Color, role),
            planBroadcasts: () => engine.ActiveRealTimeRepair is null
                ? ScopedBroadcastPlanner.PlanRepairComplete(lobbyCode, engine, roster.Color)
                : [ScopedBroadcastPlanner.PlanRepairSessionRefresh(lobbyCode, engine)]);
    }

    public async Task ConsumeTurnByTurnBonusAction(string lobbyCode, string playerId)
    {
        var (lobby, engine, roster) = RequireSeatedPlayer(lobbyCode, playerId);

        var bonusSession = engine.ActiveTurnByTurnSurface ?? throw new HubException("No turn-by-turn bonus round is in progress.");
        if (roster.Color != bonusSession.SurfacingTeam.Opponent())
        {
            throw new HubException("It's not your team's bonus action.");
        }

        RequireRole(lobby, roster, playerId, CrewRole.Captain);
        var surfacingTeam = bonusSession.SurfacingTeam;

        await ExecuteAsync(lobbyCode, engine,
            mutate: () => engine.ConsumeTurnByTurnBonusAction(),
            onSuccess: () =>
            {
                if (engine.ActiveTurnByTurnSurface is null)
                {
                    // The bonus round just finished — play resumes with the team that surfaced.
                    lobby.CurrentOrderTeam = surfacingTeam;
                }
            },
            planBroadcasts: () => engine.ActiveTurnByTurnSurface is null
                ? ScopedBroadcastPlanner.PlanRepairComplete(lobbyCode, engine, surfacingTeam)
                : [ScopedBroadcastPlanner.PlanRepairSessionRefresh(lobbyCode, engine)]);
    }

    private async Task ExecuteAsync(
        string lobbyCode,
        GameEngine engine,
        Action mutate,
        Func<IReadOnlyList<ScopedBroadcast>> planBroadcasts,
        Action? onSuccess = null)
    {
        try
        {
            mutate();
        }
        catch (Exception ex) when (ex is not HubException)
        {
            throw new HubException(ex.Message);
        }

        onSuccess?.Invoke();

        foreach (var broadcast in planBroadcasts().Concat(ScopedBroadcastPlanner.PlanGameOver(lobbyCode, engine)))
        {
            await Clients.Group(broadcast.Group).SendAsync("ScopedUpdate", broadcast.Payload.GetType().Name, broadcast.Payload);
        }
    }

    private LobbySession RequireLobby(string lobbyCode) =>
        lobbyService.TryGet(lobbyCode) ?? throw new HubException($"No lobby found with code '{lobbyCode}'.");

    private static GameEngine RequireGame(LobbySession lobby) =>
        lobby.Game ?? throw new HubException("This lobby's game hasn't started yet.");

    /// <summary>
    /// Used by every *mutating* hub method (never by <see cref="JoinGame"/>, which must still work while
    /// paused so a reconnecting player can see paused state). Without this check, a rogue or just-slow client
    /// could keep sending intents while the game was paused (manually, or because the opponent disconnected),
    /// silently defeating "all timers/turns stop" for the other team.
    /// </summary>
    private static GameEngine RequireActiveGame(LobbySession lobby)
    {
        var engine = RequireGame(lobby);
        if (lobby.Status != LobbyStatus.InProgress)
        {
            throw new HubException(lobby.Status == LobbyStatus.Paused ? "The game is paused." : $"The game is not in progress ({lobby.Status}).");
        }

        return engine;
    }

    private static TeamRoster RequireSeat(LobbySession lobby, string playerId) =>
        lobby.RosterOf(playerId) ?? throw new HubException("You are not seated in this lobby.");

    /// <summary>The common prelude every mutating hub method needs: an in-progress game and the caller's own seat on it.</summary>
    private (LobbySession Lobby, GameEngine Engine, TeamRoster Roster) RequireSeatedPlayer(string lobbyCode, string playerId)
    {
        var lobby = RequireLobby(lobbyCode);
        var engine = RequireActiveGame(lobby);
        var roster = RequireSeat(lobby, playerId);
        return (lobby, engine, roster);
    }

    /// <summary>The common prelude for any intent gated to one specific role — everything above <see cref="RequireSeatedPlayer"/>, plus the role check.</summary>
    private (LobbySession Lobby, GameEngine Engine, TeamRoster Roster) RequireAuthorizedAction(string lobbyCode, string playerId, CrewRole role)
    {
        var (lobby, engine, roster) = RequireSeatedPlayer(lobbyCode, playerId);
        RequireRole(lobby, roster, playerId, role);
        return (lobby, engine, roster);
    }

    /// <summary>
    /// Accepts the role's real occupant, or — when "Pause on Player Disconnect" is off and that occupant is
    /// disconnected — whoever is absorbing it per <see cref="TeamRoster.ActingOccupantOf"/>. When the option
    /// is on, a disconnect pauses the lobby instead, and <see cref="RequireActiveGame"/> already rejects every
    /// mutating call at that point, so absorption never actually applies mid-pause.
    /// </summary>
    private static void RequireRole(LobbySession lobby, TeamRoster roster, string playerId, CrewRole role)
    {
        if (roster.ActingOccupantOf(role, pid => IsConnected(lobby, pid)) != playerId)
        {
            throw new HubException($"Only the {role} can do that.");
        }
    }

    private static bool IsConnected(LobbySession lobby, string playerId) =>
        lobby.Players.TryGetValue(playerId, out var player) && player.ConnectionStatus == PlayerConnectionStatus.Connected;

    /// <summary>Turn-by-Turn only: whose Captain may act next — the active surface bonus round takes priority over the normal alternation when one is in progress.</summary>
    private static void RequireTurn(LobbySession lobby, GameEngine engine, TeamColor team)
    {
        if (engine.Options.Pacing != PacingMode.TurnByTurn)
        {
            return;
        }

        var expectedTeam = engine.ActiveTurnByTurnSurface is { IsComplete: false } bonus
            ? bonus.SurfacingTeam.Opponent()
            : lobby.CurrentOrderTeam;

        if (team != expectedTeam)
        {
            throw new HubException("It's not your team's turn.");
        }
    }

    private static void AdvanceTurn(LobbySession lobby, GameEngine engine, TeamColor actingTeam)
    {
        if (engine.Options.Pacing != PacingMode.TurnByTurn)
        {
            return;
        }

        if (engine.ActiveTurnByTurnSurface is { IsComplete: false })
        {
            return; // A surface bonus round governs turn order instead until it completes.
        }

        lobby.CurrentOrderTeam = actingTeam.Opponent();
    }

    private static string DescribeWeaponResult(TeamColor team, string weaponName, Coordinate location, ImpactResult impact) =>
        $"{team} {weaponName} impact at {location}: {impact}.";
}
