namespace CrowdFunding.BuildingBlocks.Application.Events;

/// <summary>
/// Overrides an application event's outbox schema version (default 1) when its payload shape
/// changes in a way old persisted/in-flight outbox rows can't be deserialized into — bump this
/// rather than renaming the class, so <see cref="EventTypeRegistry"/> can keep both versions
/// registered while old rows drain.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class EventVersionAttribute(int version) : Attribute
{
    public int Version { get; } = version;
}
