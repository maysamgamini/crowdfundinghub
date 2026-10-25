using CrowdFunding.BuildingBlocks.Application.Events;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Called by a module's transaction executor just before it commits. When the running handler asked
/// for it (<see cref="IRecordsInboxInTransaction"/>), writes the inbox row inside that transaction.
/// </summary>
public sealed class InboxRecorder<TDbContext>
    where TDbContext : DbContext
{
    private readonly InboxMessageContext _context;
    private readonly EfInbox<TDbContext> _inbox;
    private readonly TDbContext _dbContext;

    public InboxRecorder(InboxMessageContext context, EfInbox<TDbContext> inbox, TDbContext dbContext)
    {
        _context = context;
        _inbox = inbox;
        _dbContext = dbContext;
    }

    public async Task RecordIfRequestedAsync(CancellationToken cancellationToken)
    {
        if (_context.MessageId is not { } messageId
            || _context.ConsumerToRecordInTransaction is not { } consumer
            || _context.RecordedInTransaction)
        {
            return;
        }

        if (!await _inbox.TryRecordAsync(messageId, consumer, DateTime.UtcNow, cancellationToken))
        {
            // Another worker handled this message first. Throwing rolls this transaction back.
            _dbContext.ChangeTracker.Clear();
            throw new InboxAlreadyRecordedException();
        }

        _context.RecordedInTransaction = true;
    }
}
