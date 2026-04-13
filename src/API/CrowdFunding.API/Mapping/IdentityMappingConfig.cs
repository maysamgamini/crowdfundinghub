using CrowdFunding.API.Contracts.Identity;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.AssignRoleToUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.GrantPermissionToUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.LoginUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RegisterUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Queries.GetCurrentUser;
using Mapster;

namespace CrowdFunding.API.Mapping;

/// <summary>
/// Registers Mapster mappings for Identity.
/// </summary>
public static class IdentityMappingConfig
{
    public static void Register(TypeAdapterConfig config)
    {
        config.NewConfig<RegisterUserRequest, RegisterUserCommand>();
        config.NewConfig<RegisterUserResult, RegisterUserResponse>();
        config.NewConfig<LoginUserRequest, LoginUserCommand>();
        config.NewConfig<LoginUserResult, LoginUserResponse>();
        config.NewConfig<AssignRoleToUserResult, AssignRoleToUserResponse>();
        config.NewConfig<GrantPermissionToUserResult, GrantPermissionToUserResponse>();
        config.NewConfig<GetCurrentUserResult, CurrentUserResponse>();
    }
}
