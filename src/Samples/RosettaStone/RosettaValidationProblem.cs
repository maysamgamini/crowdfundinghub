using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace CrowdFunding.Samples.RosettaStone;

/// <summary>
/// Builds the same RFC 9457 <c>application/problem+json</c> validation shape
/// (<c>Results.ValidationProblem</c>) that ASP.NET Core's MVC pipeline produces for Tier 3, so
/// all three Rosetta Stone tiers return byte-for-byte comparable error responses for invalid
/// input despite Tier 1 and Tier 2 bypassing MVC's automatic model-state validation.
/// </summary>
internal static class RosettaValidationProblem
{
    public static IResult FromFluentValidation(ValidationResult validationResult) =>
        Microsoft.AspNetCore.Http.Results.ValidationProblem(validationResult.ToDictionary());
}
