using System.Collections.Concurrent;
using System.Reflection;

namespace CrowdFunding.BuildingBlocks.Application.Events;

/// <summary>
/// Resolves a stable (EventType, Version) discriminator to the CLR type of a
/// <see cref="BaseApplicationEvent"/>, for outbox deserialization.
///
/// Replaces resolving <c>Type.GetType(AssemblyQualifiedName)</c> (improvement.md §2.3): an
/// assembly-qualified name embeds namespace, assembly name/version and public key token, so
/// moving a class, renaming a namespace, or bumping the assembly version breaks deserialization
/// of every already-persisted outbox row referencing that type — permanently poisoning it. The
/// discriminator here is just the event class's simple name, so namespace/folder refactors and
/// assembly version bumps no longer break anything; only an actual rename requires bumping
/// <c>Version</c> (via <see cref="EventVersionAttribute"/>) and keeping the old name registered
/// until every outstanding outbox row referencing it has drained.
/// </summary>
public sealed class EventTypeRegistry
{
    private readonly ConcurrentDictionary<(string EventType, int Version), Type> _types = new();

    public void Register(Type eventType)
    {
        if (!typeof(BaseApplicationEvent).IsAssignableFrom(eventType))
        {
            throw new ArgumentException(
                $"'{eventType.Name}' does not derive from {nameof(BaseApplicationEvent)}.", nameof(eventType));
        }

        var version = eventType.GetCustomAttribute<EventVersionAttribute>()?.Version ?? 1;
        _types[(eventType.Name, version)] = eventType;
    }

    public bool TryResolve(string eventType, int version, out Type? resolvedType)
        => _types.TryGetValue((eventType, version), out resolvedType);

    public static string GetDiscriminator(Type eventType) => eventType.Name;

    public static int GetVersion(Type eventType) => eventType.GetCustomAttribute<EventVersionAttribute>()?.Version ?? 1;
}
