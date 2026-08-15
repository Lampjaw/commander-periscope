using CommanderPeriscope.Domain.Game;

namespace CommanderPeriscope.Visibility.Views;

/// <summary>
/// One entry in the global toast/event feed (torpedo fired, mine detonated, sub surfaced, ...). Only ever
/// built from information the tabletop rules already require players to announce aloud — see the
/// "Mapping rules to digital" notes on what's intentionally public.
/// </summary>
public sealed record GameToastPayload(string Message);

/// <summary>The game-over announcement — who won, or that it was a tie (simultaneous destruction).</summary>
public sealed record GameOverPayload(WinResult Result);
