namespace CrowdFunding.BuildingBlocks.Application.Audit;

/// <summary>
/// One immutable, forensic record of an administrative action (TICKET-039). Persisted to an
/// append-only table (<c>system.audit_records</c> — see <c>AuditDbContext</c>) that the
/// application's own database role is granted only <c>SELECT</c>/<c>INSERT</c> on, so even a
/// compromised application process cannot alter or erase history it already wrote.
/// </summary>
public sealed record AuditRecord(
    Guid Id,
    Guid ActorId,
    string ActorEmail,
    string Action,
    string CommandType,
    string PayloadJson,
    string IpAddress,
    string UserAgent,
    DateTime TimestampUtc);
