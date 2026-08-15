using CommanderPeriscope.Domain.Common;

namespace CommanderPeriscope.Domain.Surfacing;

/// <summary>
/// Real-Time repair: each of the 4 crew secures one hull section. Once all four are secured the
/// repair completes — this session just tracks who's finished; it doesn't validate the trace/tap minigame
/// itself (that's a UI/anti-cheat concern for later phases).
/// </summary>
public sealed class RealTimeRepairSession
{
    private readonly HashSet<CrewRole> _secured = [];

    public IReadOnlySet<CrewRole> SecuredRoles => _secured;
    public bool IsComplete => _secured.Count == 4;

    public void SecureSection(CrewRole role)
    {
        if (IsComplete)
        {
            throw new InvalidOperationException("Repair is already complete.");
        }

        _secured.Add(role);
    }
}
