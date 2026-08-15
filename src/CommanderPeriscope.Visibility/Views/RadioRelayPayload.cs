using CommanderPeriscope.Domain.Common;

namespace CommanderPeriscope.Visibility.Views;

/// <summary>
/// The optional "Radio Relay" toast: the enemy Captain's heading, and nothing else. Deliberately shaped to
/// hold only a <see cref="Direction"/> so it can never carry position or any other derived information,
/// even by accident later.
/// </summary>
public sealed record RadioRelayPayload(Direction Heading);
