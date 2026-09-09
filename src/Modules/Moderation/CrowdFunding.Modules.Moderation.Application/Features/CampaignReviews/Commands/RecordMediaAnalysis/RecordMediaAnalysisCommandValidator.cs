using FluentValidation;

namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.RecordMediaAnalysis;

/// <summary>
/// Validates Record Media Analysis Command instances before they reach the handler.
/// </summary>
public sealed class RecordMediaAnalysisCommandValidator : AbstractValidator<RecordMediaAnalysisCommand>
{
    public RecordMediaAnalysisCommandValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.ToxicityScore).InclusiveBetween(0m, 1m);
        RuleFor(x => x.AdultContentScore).InclusiveBetween(0m, 1m);
    }
}
