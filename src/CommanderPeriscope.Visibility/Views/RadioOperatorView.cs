namespace CommanderPeriscope.Visibility.Views;

/// <summary>
/// The Radio Operator's deduction grid is entirely private, client-side bookkeeping — the server holds no
/// hidden state for this role at all. This marker exists only so the role has a symmetrical scoped group;
/// it carries no fields to leak.
/// </summary>
public sealed record RadioOperatorView;
