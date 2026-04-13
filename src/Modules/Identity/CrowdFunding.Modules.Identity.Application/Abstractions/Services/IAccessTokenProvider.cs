using CrowdFunding.Modules.Identity.Domain.Aggregates;

namespace CrowdFunding.Modules.Identity.Application.Abstractions.Services;

/// <summary>
/// Creates access tokens for authenticated users.
/// </summary>
public interface IAccessTokenProvider
{
    AccessToken Create(User user, IReadOnlyCollection<string> permissions);
}
