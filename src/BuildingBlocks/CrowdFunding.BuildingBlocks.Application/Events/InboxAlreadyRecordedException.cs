namespace CrowdFunding.BuildingBlocks.Application.Events;

/// <summary>
/// Another worker recorded this (message, consumer) pair first, so this handler run must not
/// commit. The publisher treats it as "already handled" and moves on.
/// </summary>
public sealed class InboxAlreadyRecordedException : Exception
{
    public InboxAlreadyRecordedException()
        : base("The inbox already holds this message for this consumer; the handler's work was rolled back.")
    {
    }
}
