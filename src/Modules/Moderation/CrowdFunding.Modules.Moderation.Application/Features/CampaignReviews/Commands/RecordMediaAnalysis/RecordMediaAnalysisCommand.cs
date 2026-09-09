namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.RecordMediaAnalysis;

/// <summary>
/// The result of an asynchronous, serverless media-safety analysis for a campaign, delivered via
/// a signed webhook from the analyzing Cloud Function. See TICKET-032.
/// </summary>
public sealed record RecordMediaAnalysisCommand(
    Guid CampaignId,
    bool PassedSafetyCheck,
    decimal ToxicityScore,
    decimal AdultContentScore);
