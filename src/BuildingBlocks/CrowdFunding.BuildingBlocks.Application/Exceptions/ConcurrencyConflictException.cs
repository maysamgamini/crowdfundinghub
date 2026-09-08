namespace CrowdFunding.BuildingBlocks.Application.Exceptions;

/// <summary>
/// Thrown by a transaction executor when an optimistic concurrency token (e.g. PostgreSQL
/// <c>xmin</c>) detects that the aggregate was modified by another writer since it was loaded.
/// Kept infrastructure-agnostic so application-layer command handlers can catch and retry
/// without depending on Entity Framework Core.
/// </summary>
public sealed class ConcurrencyConflictException(string message, Exception innerException)
    : Exception(message, innerException);
