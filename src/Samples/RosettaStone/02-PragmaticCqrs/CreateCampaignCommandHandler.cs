using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Samples.RosettaStone.Persistence;

namespace CrowdFunding.Samples.RosettaStone.PragmaticCqrs;

/// <summary>
/// Direct DbContext projection: no repository interface, no aggregate factory, no domain
/// events. Validation already happened in the endpoint before dispatch, so this handler is a
/// pure mapping + persistence step.
/// </summary>
public sealed class CreateCampaignCommandHandler(RosettaStoneDbContext db)
    : ICommandHandler<CreateCampaignCommand, CreateCampaignResult>
{
    public async Task<CreateCampaignResult> Handle(CreateCampaignCommand command, CancellationToken cancellationToken)
    {
        var record = new CampaignRecord
        {
            Id = Guid.NewGuid(),
            Title = command.Title,
            Story = command.Story,
            TargetAmount = command.TargetAmount,
            Currency = command.Currency,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.CampaignRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateCampaignResult(record.Id);
    }
}
