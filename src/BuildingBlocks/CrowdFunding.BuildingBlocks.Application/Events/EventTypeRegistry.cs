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

    /// <summary>
    /// Registers an application event type with its associated discriminator name and version.
    /// </summary>
    /// <param name="eventType">The application event type deriving from <see cref="BaseApplicationEvent"/>.</param>
    /// <exception cref="ArgumentException">Thrown when eventType does not derive from <see cref="BaseApplicationEvent"/>.</exception>
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

    /// <summary>
    /// Attempts to resolve a CLR type matching the given discriminator and version.
    /// </summary>
    /// <param name="eventType">The discriminator string name of the event.</param>
    /// <param name="version">The schema version integer of the event.</param>
    /// <param name="resolvedType">The resolved CLR type if found, otherwise null.</param>
    /// <returns>True if a matching type is registered; otherwise false.</returns>
    public bool TryResolve(string eventType, int version, out Type? resolvedType)
        => _types.TryGetValue((eventType, version), out resolvedType);

    /// <summary>
    /// Returns the discriminator name string for a given event CLR type.
    /// </summary>
    /// <param name="eventType">The event CLR type.</param>
    /// <returns>The simple name of the type.</returns>
    public static string GetDiscriminator(Type eventType) => eventType.Name;

    /// <summary>
    /// Returns the version integer configured on the event type via <see cref="EventVersionAttribute"/>, defaulting to 1.
    /// </summary>
    /// <param name="eventType">The event CLR type.</param>
    /// <returns>The version integer.</returns>
    public static int GetVersion(Type eventType) => eventType.GetCustomAttribute<EventVersionAttribute>()?.Version ?? 1;
}
