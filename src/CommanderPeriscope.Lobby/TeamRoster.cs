using CommanderPeriscope.Domain.Common;

namespace CommanderPeriscope.Lobby;

/// <summary>
/// One team's seats. Team size determines which of the 4 roles are independently claimable — a 3-player team
/// combines Captain+First Mate into one seat, a 2-player team combines Captain+First Mate+Engineer, matching
/// the tabletop's role-doubling rules. The "extra" roles simply mirror whoever holds Captain.
/// </summary>
public sealed class TeamRoster(TeamColor color)
{
    private readonly Dictionary<CrewRole, string> _seats = [];
    private readonly HashSet<string> _readyPlayerIds = [];

    public TeamColor Color { get; } = color;
    public string SubmarineName { get; set; } = SubmarineNameGenerator.Generate();
    public int ConfiguredSize { get; set; } = 4;

    public static IReadOnlyList<CrewRole> RolesForSize(int size) => size switch
    {
        4 => [CrewRole.Captain, CrewRole.FirstMate, CrewRole.Engineer, CrewRole.RadioOperator],
        3 => [CrewRole.Captain, CrewRole.Engineer, CrewRole.RadioOperator],
        2 => [CrewRole.Captain, CrewRole.RadioOperator],
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Team size must be 2, 3, or 4."),
    };

    public IReadOnlyList<CrewRole> ActiveRoles => RolesForSize(ConfiguredSize);

    /// <summary>Maps a nominal role to the seat that actually governs it — e.g. First Mate resolves to Captain on a 3-player team.</summary>
    public CrewRole ResolveActiveRole(CrewRole role) => (ConfiguredSize, role) switch
    {
        (3, CrewRole.FirstMate) => CrewRole.Captain,
        (2, CrewRole.FirstMate) => CrewRole.Captain,
        (2, CrewRole.Engineer) => CrewRole.Captain,
        _ => role,
    };

    public string? OccupantOf(CrewRole role) => _seats.GetValueOrDefault(ResolveActiveRole(role));

    public bool IsRoleClaimed(CrewRole role) => OccupantOf(role) is not null;

    /// <summary>
    /// Which role a disconnected role's duties fall back to when "Pause on Player Disconnect" is off — reusing
    /// the exact same target the under-crewed-team groupings already use for that role (§2.2), just applied
    /// per-role instead of scaling the whole team down (which would wrongly also reassign a still-connected
    /// teammate's role). Radio Operator and Captain have no fallback: nobody else absorbs them.
    /// </summary>
    private static CrewRole? AbsorptionFallback(CrewRole role) => role switch
    {
        CrewRole.FirstMate or CrewRole.Engineer => CrewRole.Captain,
        _ => null,
    };

    /// <summary>
    /// Who may currently act for <paramref name="role"/>: its own occupant if connected, otherwise — when
    /// "Pause on Player Disconnect" is off, so the game is still running — whoever the role's absorption
    /// fallback occupant is, if that player is connected. Falls back to the real occupant (even if
    /// disconnected, e.g. no one connected can absorb it) rather than returning null, since the seat itself
    /// is still claimed.
    /// </summary>
    public string? ActingOccupantOf(CrewRole role, Func<string, bool> isConnected)
    {
        var occupant = OccupantOf(role);
        if (occupant is null || isConnected(occupant))
        {
            return occupant;
        }

        if (AbsorptionFallback(role) is { } fallbackRole)
        {
            var fallbackOccupant = OccupantOf(fallbackRole);
            if (fallbackOccupant is not null && isConnected(fallbackOccupant))
            {
                return fallbackOccupant;
            }
        }

        return occupant;
    }

    public CrewRole? SeatOf(string playerId)
    {
        foreach (var (seatRole, occupantId) in _seats)
        {
            if (occupantId == playerId)
            {
                return seatRole;
            }
        }

        return null;
    }

    public bool IsSeated(string playerId) => SeatOf(playerId) is not null;

    /// <summary>
    /// Claims a seat. <paramref name="role"/> must itself be one of <see cref="ActiveRoles"/> at the current
    /// team size — a mirrored role (e.g. First Mate on a 3-player team) can't be claimed directly, only read
    /// via <see cref="OccupantOf"/>, since it has no seat of its own to hold.
    /// </summary>
    public void Claim(CrewRole role, string playerId)
    {
        if (!ActiveRoles.Contains(role))
        {
            throw new InvalidOperationException($"{role} is not an independently claimable seat at team size {ConfiguredSize}.");
        }

        _seats[role] = playerId;
        _readyPlayerIds.Remove(playerId);
    }

    public void Vacate(string playerId)
    {
        var seat = SeatOf(playerId);
        if (seat is not null)
        {
            _seats.Remove(seat.Value);
        }

        _readyPlayerIds.Remove(playerId);
    }

    public void SetReady(string playerId, bool ready)
    {
        if (!IsSeated(playerId))
        {
            throw new InvalidOperationException("Only a seated player can toggle ready.");
        }

        if (ready)
        {
            _readyPlayerIds.Add(playerId);
        }
        else
        {
            _readyPlayerIds.Remove(playerId);
        }
    }

    public bool IsReady(string playerId) => _readyPlayerIds.Contains(playerId);

    public void SetReadyIfSeated(string playerId, bool ready)
    {
        if (IsSeated(playerId))
        {
            SetReady(playerId, ready);
        }
    }

    public IReadOnlyCollection<string> DistinctPlayerIds => _seats.Values.Distinct().ToList();

    public bool AllActiveRolesClaimedAndReady() =>
        ActiveRoles.Count > 0 && ActiveRoles.All(role => OccupantOf(role) is { } playerId && IsReady(playerId));
}
