using CrowdFunding.Modules.Identity.Domain.Aggregates;

namespace CrowdFunding.Modules.Identity.Application.Abstractions.Services;

public interface IAccessTokenProvider
{
    AccessToken Create(User user, IReadOnlyCollection<string> permissions);
}
