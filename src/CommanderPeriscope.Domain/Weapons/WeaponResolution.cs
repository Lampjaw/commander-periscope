using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Maps;

namespace CommanderPeriscope.Domain.Weapons;

public enum ImpactResult
{
    Clear,
    Indirect,
    Direct,
}

/// <summary>Pure classification rules shared by Torpedo and Mine resolution.</summary>
public static class WeaponResolution
{
    public static ImpactResult Classify(Coordinate impact, Coordinate targetPosition)
    {
        if (impact == targetPosition)
        {
            return ImpactResult.Direct;
        }

        return GameMap.ChebyshevDistance(impact, targetPosition) == 1 ? ImpactResult.Indirect : ImpactResult.Clear;
    }

    public static int DamageFor(ImpactResult result) => result switch
    {
        ImpactResult.Direct => 2,
        ImpactResult.Indirect => 1,
        ImpactResult.Clear => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(result)),
    };

    /// <summary>Torpedo range: up to 4 dots away in a straight line or right-angle steps (never diagonal) — equivalent to Manhattan distance ≤ 4.</summary>
    public static bool IsTorpedoInRange(Coordinate origin, Coordinate impact) => GameMap.ManhattanDistance(origin, impact) <= 4;

    /// <summary>Firing/detonating within 1 dot of your own sub costs you 1 damage too.</summary>
    public static bool CausesSelfDamage(Coordinate ownPosition, Coordinate impact) => GameMap.ChebyshevDistance(ownPosition, impact) == 1;

    /// <summary>A mine must be placed on a cardinally-adjacent water dot to the placing sub.</summary>
    public static bool IsCardinallyAdjacent(Coordinate from, Coordinate to) =>
        Enum.GetValues<Direction>().Any(d => GameMap.Step(from, d) == to);
}
