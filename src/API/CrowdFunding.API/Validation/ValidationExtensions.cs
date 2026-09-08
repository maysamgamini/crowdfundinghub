using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrowdFunding.API.Validation;

/// <summary>
/// Adds FluentValidation errors to ASP.NET Core model state. Every controller depends on this;
/// it previously lived tucked inside CampaignsController.cs, so renaming or deleting that file
/// would have silently broken every other controller's build.
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Copies FluentValidation errors into the ASP.NET Core <see cref="ModelStateDictionary"/>.
    /// </summary>
    /// <param name="validationResult">The FluentValidation result to transfer.</param>
    /// <param name="modelState">The target model state dictionary.</param>
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
