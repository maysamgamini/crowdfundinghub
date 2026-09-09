namespace CrowdFunding.API.Contracts.Webhooks;

/// <summary>
/// The callback payload a serverless Cloud Function posts back after asynchronously analyzing a
/// campaign's media for safety/toxicity. See TICKET-032.
/// </summary>
public sealed record MediaAnalysisResultWebhook(
    Guid CampaignId,
    bool PassedSafetyCheck,
    decimal ToxicityScore,
    decimal AdultContentScore,
    string[] DetectedLabels,
    DateTime AnalyzedAtUtc);
