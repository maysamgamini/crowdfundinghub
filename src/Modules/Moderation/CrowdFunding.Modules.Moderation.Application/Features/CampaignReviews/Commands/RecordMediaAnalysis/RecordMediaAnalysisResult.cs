namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.RecordMediaAnalysis;

/// <summary>
/// Represents the outcome of recording an automated media-safety analysis result.
/// </summary>
public sealed record RecordMediaAnalysisResult(Guid CampaignId, string Status);
