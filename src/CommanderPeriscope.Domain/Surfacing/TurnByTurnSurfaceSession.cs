using CommanderPeriscope.Domain.Common;

namespace CommanderPeriscope.Domain.Surfacing;

/// <summary>
/// Turn-by-Turn surfacing: the enemy gets three bonus turns (course + system activation each) while
/// the surfacing team repairs. If the enemy also surfaces mid-bonus, it forfeits its remaining bonus actions
/// and the original surfacing team takes its own three bonus rounds instead.
/// </summary>
public sealed class TurnByTurnSurfaceSession(TeamColor surfacingTeam)
{
    public const int BonusActionsPerSurface = 3;

    public TeamColor SurfacingTeam { get; private set; } = surfacingTeam;
    public int BonusActionsRemaining { get; private set; } = BonusActionsPerSurface;
    public bool IsComplete => BonusActionsRemaining <= 0;

    public void ConsumeBonusAction()
    {
        if (IsComplete)
        {
            throw new InvalidOperationException("No bonus actions remain.");
        }

        BonusActionsRemaining--;
    }

    /// <summary>The team currently receiving bonus actions surfaces instead of using them — it forfeits the rest, and the other team now gets its own three bonus rounds.</summary>
    public void ForfeitAndSwap()
    {
        SurfacingTeam = SurfacingTeam.Opponent();
        BonusActionsRemaining = BonusActionsPerSurface;
    }
}
