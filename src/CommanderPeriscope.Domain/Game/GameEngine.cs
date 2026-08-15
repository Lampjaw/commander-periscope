using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Engineering;
using CommanderPeriscope.Domain.Maps;
using CommanderPeriscope.Domain.Submarines;
using CommanderPeriscope.Domain.Surfacing;
using CommanderPeriscope.Domain.Systems;
using CommanderPeriscope.Domain.Weapons;

namespace CommanderPeriscope.Domain.Game;

public sealed record TorpedoResult(ImpactResult Impact, IReadOnlyList<DamageEvent> DamageEvents, WinResult WinResult);

public sealed record MineResult(ImpactResult Impact, IReadOnlyList<DamageEvent> DamageEvents, WinResult WinResult);

/// <summary>
/// The authoritative, server-side simulation of one game: two submarines on a shared map, resolving intents
/// into state changes and events. Deliberately has no knowledge of networking or per-role visibility — that's
/// a separate layer on top of this one.
/// </summary>
public sealed class GameEngine
{
    public GameMap Map { get; }
    public GameOptions Options { get; }
    public Submarine Red { get; }
    public Submarine Blue { get; }
    public WinResult Result { get; private set; } = WinResult.None;
    public bool IsOver => Result != WinResult.None;

    public RealTimeRepairSession? ActiveRealTimeRepair { get; private set; }
    public TurnByTurnSurfaceSession? ActiveTurnByTurnSurface { get; private set; }

    public GameEngine(GameMap map, GameOptions options)
    {
        Map = map;
        Options = options;
        var damageCapacity = options.Mode == GameMode.Hunt ? 4 : 1;
        Red = new Submarine(TeamColor.Red, damageCapacity);
        Blue = new Submarine(TeamColor.Blue, damageCapacity);
    }

    public Submarine SubFor(TeamColor team) => team == TeamColor.Red ? Red : Blue;

    public void SetStartingPosition(TeamColor team, Coordinate start)
    {
        if (!Map.IsWater(start))
        {
            throw new InvalidOperationException("Starting position must be an in-bounds water dot.");
        }

        SubFor(team).PlaceStartingPosition(start);
    }

    public void Move(TeamColor team, Direction direction)
    {
        EnsureNotOver();
        var sub = SubFor(team);
        EnsureNotSurfaced(sub);
        RequireNoPendingDamageReport(sub);

        var next = GameMap.Step(RequirePosition(sub), direction);
        if (!Map.CanMoveTo(next, sub.Trail))
        {
            throw new InvalidOperationException($"Illegal move to {next} — off the map, on land, or already visited this leg.");
        }

        sub.MoveTo(next);

        if (Options.MandatoryDamageReportEnabled)
        {
            sub.SetPendingDamageReport();
        }
    }

    /// <summary>Activates Silence: up to 4 unannounced moves in a single straight line, then clears the gauge.</summary>
    public void ActivateSilence(TeamColor team, Direction direction, int steps)
    {
        EnsureNotOver();
        if (steps is < 0 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(steps), steps, "Silence moves 0 to 4 dots.");
        }

        var sub = SubFor(team);
        EnsureNotSurfaced(sub);
        RequireActivationAllowed(sub, SystemType.Silence);

        var position = RequirePosition(sub);
        for (var i = 0; i < steps; i++)
        {
            position = GameMap.Step(position, direction);
            if (!Map.CanMoveTo(position, sub.Trail))
            {
                throw new InvalidOperationException($"Illegal silent move to {position} — off the map, on land, or already visited this leg.");
            }

            sub.MoveTo(position);
        }

        sub.ActivateSystem(SystemType.Silence);
    }

    public void PlaceMine(TeamColor team, Coordinate target)
    {
        EnsureNotOver();
        var sub = SubFor(team);
        EnsureNotSurfaced(sub);
        RequireActivationAllowed(sub, SystemType.Mine);

        var position = RequirePosition(sub);
        if (!WeaponResolution.IsCardinallyAdjacent(position, target))
        {
            throw new InvalidOperationException("A mine must be placed on a dot cardinally adjacent to your own sub.");
        }

        if (sub.Trail.Contains(target))
        {
            throw new InvalidOperationException("Cannot place a mine on your own already-drawn route.");
        }

        sub.ActivateSystem(SystemType.Mine);
        sub.PlaceMine(target);
    }

    /// <summary>Detonating a placed mine is not a system activation and doesn't touch the gauge — but still requires the Mine (red) breakdown to be clear.</summary>
    public MineResult DetonateMine(TeamColor detonatingTeam, Coordinate mineLocation)
    {
        EnsureNotOver();
        var sub = SubFor(detonatingTeam);
        var enemy = SubFor(detonatingTeam.Opponent());

        if (!sub.Mines.Contains(mineLocation))
        {
            throw new InvalidOperationException($"No mine placed at {mineLocation}.");
        }

        if (sub.Engineer.IsSystemBlocked(SystemType.Mine))
        {
            throw new InvalidOperationException("Mine is blocked by a breakdown — repair first.");
        }

        sub.DetonateMine(mineLocation);

        var events = new List<DamageEvent>();
        ResolveWeaponImpact(sub, enemy, mineLocation, DamageCause.MineSelf, DamageCause.MineDirect, DamageCause.MineIndirect, events);
        ResolveChainReactions(sub, enemy, mineLocation, events);

        CheckWinConditions();
        var impact = WeaponResolution.Classify(mineLocation, RequirePosition(enemy));
        return new MineResult(impact, events, Result);
    }

    public TorpedoResult FireTorpedo(TeamColor firingTeam, Coordinate impact)
    {
        EnsureNotOver();
        var sub = SubFor(firingTeam);
        var enemy = SubFor(firingTeam.Opponent());
        EnsureNotSurfaced(sub);
        RequireActivationAllowed(sub, SystemType.Torpedo);

        if (!WeaponResolution.IsTorpedoInRange(RequirePosition(sub), impact))
        {
            throw new InvalidOperationException("Torpedo impact point is out of range (more than 4 dots away).");
        }

        sub.ActivateSystem(SystemType.Torpedo);

        var events = new List<DamageEvent>();
        ResolveWeaponImpact(sub, enemy, impact, DamageCause.TorpedoSelf, DamageCause.TorpedoDirect, DamageCause.TorpedoIndirect, events);

        // A torpedo landing on a mine destroys it outright (no detonation); landing adjacent chain-detonates it.
        sub.Mines.Remove(impact);
        enemy.Mines.Remove(impact);
        ResolveChainReactions(sub, enemy, impact, events);

        CheckWinConditions();
        var classification = WeaponResolution.Classify(impact, RequirePosition(enemy));
        return new TorpedoResult(classification, events, Result);
    }

    /// <summary>The First Mate's response to every Captain order: marks one empty box on a gauge of their choice.</summary>
    public void MarkGauge(TeamColor team, SystemType type)
    {
        EnsureNotOver();
        SubFor(team).Gauges.Mark(type);
    }

    public void RequestSonar(TeamColor team)
    {
        EnsureNotOver();
        var sub = SubFor(team);
        RequireActivationAllowed(sub, SystemType.Sonar);
        sub.ActivateSystem(SystemType.Sonar);
    }

    public void RequestDrone(TeamColor team)
    {
        EnsureNotOver();
        var sub = SubFor(team);
        RequireActivationAllowed(sub, SystemType.Drone);
        sub.ActivateSystem(SystemType.Drone);
    }

    public void MarkBreakdown(TeamColor team, BreakdownItem breakdownItem)
    {
        EnsureNotOver();
        var sub = SubFor(team);
        var result = sub.Engineer.MarkBreakdown(breakdownItem);
        sub.ClearPendingDamageReport();

        switch (result)
        {
            case BreakdownResult.RadiationFull:
                if (Options.AutomaticDamageVerification)
                {
                    sub.Engineer.ClearAllBreakdowns();
                    sub.Damage.ApplyDamage(1);
                    CheckWinConditions();
                }
                break;
            case BreakdownResult.CompleteAreaBreakdown:
                if (Options.AutomaticDamageVerification)
                {
                    sub.Engineer.ClearColumnBreakdowns(breakdownItem.Column);
                    sub.Damage.ApplyDamage(1);
                    CheckWinConditions();
                }
                break;
            case BreakdownResult.CanSelfRepairCircuit:
                var circuitId = (breakdownItem as SystemBreakdown)!.CircuitGroupId!.Value;
                sub.Engineer.ClearCircuitBreakdowns(circuitId);
                break;
        }
    }

    /// <summary>Begins surfacing: reveals the sector, stops the sub, and opens the pacing-appropriate repair session.</summary>
    public void Surface(TeamColor team)
    {
        EnsureNotOver();
        var sub = SubFor(team);
        sub.Surface();

        if (Options.Pacing == PacingMode.RealTime)
        {
            ActiveRealTimeRepair = new RealTimeRepairSession();
            return;
        }

        if (ActiveTurnByTurnSurface is { IsComplete: false } existing && existing.SurfacingTeam.Opponent() == team)
        {
            // The team taking bonus actions has chosen to surface instead — it forfeits the rest of them.
            existing.ForfeitAndSwap();
        }
        else
        {
            ActiveTurnByTurnSurface = new TurnByTurnSurfaceSession(team);
        }
    }

    public void SecureRealTimeRepairSection(TeamColor team, CrewRole role)
    {
        if (ActiveRealTimeRepair is null)
        {
            throw new InvalidOperationException("No real-time repair is in progress.");
        }

        ActiveRealTimeRepair.SecureSection(role);
        if (ActiveRealTimeRepair.IsComplete)
        {
            SubFor(team).CompleteRepair();
            ActiveRealTimeRepair = null;
        }
    }

    public void ConsumeTurnByTurnBonusAction()
    {
        if (ActiveTurnByTurnSurface is null)
        {
            throw new InvalidOperationException("No turn-by-turn surface bonus round is in progress.");
        }

        ActiveTurnByTurnSurface.ConsumeBonusAction();
        if (ActiveTurnByTurnSurface.IsComplete)
        {
            SubFor(ActiveTurnByTurnSurface.SurfacingTeam).CompleteRepair();
            ActiveTurnByTurnSurface = null;
        }
    }

    public int? SectorFor(Submarine sub)
    {
        if (!sub.Position.HasValue)
        {
            return null;
        }

        return sub.Position.Value.Col / GameMap.SectorSize + 1 + sub.Position.Value.Row / GameMap.SectorSize * Map.SectorsPerSide;
    }

    private static void ResolveWeaponImpact(
        Submarine firer,
        Submarine target,
        Coordinate impact,
        DamageCause selfCause,
        DamageCause directCause,
        DamageCause indirectCause,
        List<DamageEvent> events)
    {
        if (WeaponResolution.CausesSelfDamage(RequirePosition(firer), impact))
        {
            firer.Damage.ApplyDamage(1);
            events.Add(new DamageEvent(firer.Team, 1, selfCause, impact));
        }

        var classification = WeaponResolution.Classify(impact, RequirePosition(target));
        var damage = WeaponResolution.DamageFor(classification);
        if (damage <= 0)
        {
            return;
        }

        target.Damage.ApplyDamage(damage);
        var cause = classification == ImpactResult.Direct ? directCause : indirectCause;
        events.Add(new DamageEvent(target.Team, damage, cause, impact));
    }

    /// <summary>A mine adjacent to a weapon's impact point explodes too, resolved after the triggering weapon's own damage.</summary>
    private static void ResolveChainReactions(Submarine a, Submarine b, Coordinate impact, List<DamageEvent> events)
    {
        foreach (var owner in new[] { a, b })
        {
            foreach (var mine in owner.Mines.Where(m => GameMap.ChebyshevDistance(m, impact) == 1).ToList())
            {
                owner.Mines.Remove(mine);
                var other = owner == a ? b : a;
                ResolveWeaponImpact(owner, other, mine, DamageCause.MineSelf, DamageCause.MineChainDirect, DamageCause.MineChainIndirect, events);
            }
        }
    }

    private void CheckWinConditions()
    {
        if (Result != WinResult.None)
        {
            return;
        }

        var redOut = Red.Damage.IsNeutralized;
        var blueOut = Blue.Damage.IsNeutralized;

        Result = (redOut, blueOut) switch
        {
            (true, true) => WinResult.Tie,
            (true, false) => WinResult.BlueWins,
            (false, true) => WinResult.RedWins,
            _ => WinResult.None,
        };
    }

    private static void RequireActivationAllowed(Submarine sub, SystemType type)
    {
        if (!sub.CanActivateSystem)
        {
            throw new InvalidOperationException("Cannot activate two systems back-to-back — a move must happen first.");
        }

        if (!sub.Gauges.IsReady(type))
        {
            throw new InvalidOperationException($"{type} is not charged yet.");
        }

        if (sub.Engineer.IsSystemBlocked(type))
        {
            throw new InvalidOperationException($"{type} is blocked by a breakdown — repair first.");
        }
    }

    private static Coordinate RequirePosition(Submarine sub) =>
        sub.Position ?? throw new InvalidOperationException("Submarine has no starting position set yet.");

    private static void RequireNoPendingDamageReport(Submarine sub)
    {
        if (sub.PendingDamageReport)
        {
            throw new InvalidOperationException("Move blocked — the Engineer must mark the current damage report first.");
        }
    }

    private static void EnsureNotSurfaced(Submarine sub)
    {
        if (sub.Surfaced)
        {
            throw new InvalidOperationException("Cannot act while surfaced.");
        }
    }

    private void EnsureNotOver()
    {
        if (IsOver)
        {
            throw new InvalidOperationException($"Game is already over ({Result}).");
        }
    }
}
