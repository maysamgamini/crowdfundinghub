using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrowdFunding.Moderation.Service.Validation;

/// <summary>
/// Copies FluentValidation errors into ASP.NET Core's <see cref="ModelStateDictionary"/>. A
/// deliberate, small duplicate of <c>CrowdFunding.API.Validation.ValidationExtensions</c> —
/// referencing the monolith's API project from an "extracted microservice" would defeat the
/// point of this proof-of-concept, and this is presentation-layer glue, not domain logic.
/// </summary>
public static class ValidationExtensions
{
    public static void AddToModelState(
        this FluentValidation.Results.ValidationResult validationResult,
        ModelStateDictionary modelState)
    {
        foreach (var error in validationResult.Errors)
        {
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }
    }
}
