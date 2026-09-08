namespace CrowdFunding.BuildingBlocks.Domain.Common;

/// <summary>
/// Derives a PostgreSQL advisory-lock key (a signed 64-bit integer) from a <see cref="Guid"/>.
///
/// <see cref="Guid.GetHashCode"/> only produces 32 bits of entropy; sign-extending it into a
/// <c>long</c> does not add any — two unrelated GUIDs collide on it far more often than the
/// 64-bit key space would suggest (a 50% collision probability after only ~77,000 values, versus
/// ~5.1 billion for a true 64-bit key). A collision here doesn't corrupt data, but it does
/// serialize two unrelated aggregates (e.g. two different campaigns) against the same
/// <c>pg_advisory_xact_lock</c>, causing unexplained cross-entity contention under load.
/// This XORs the two 64-bit halves of the GUID's own 128 bits instead, using all of its entropy.
/// </summary>
public static class AdvisoryLockKey
{
    public static long FromGuid(Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes);

        var high = BitConverter.ToInt64(bytes[..8]);
        var low = BitConverter.ToInt64(bytes[8..]);

        return high ^ low;
    }
}
