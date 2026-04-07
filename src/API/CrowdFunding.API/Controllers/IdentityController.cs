using CrowdFunding.API.Contracts.Identity;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.AssignRoleToUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.GrantPermissionToUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.LoginUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RegisterUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Queries.GetCurrentUser;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using FluentValidation;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrowdFunding.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class IdentityController : ControllerBase
{
    private readonly AssignRoleToUserCommandHandler _assignRoleToUserCommandHandler;
    private readonly GetCurrentUserQueryHandler _getCurrentUserQueryHandler;
    private readonly GrantPermissionToUserCommandHandler _grantPermissionToUserCommandHandler;
    private readonly LoginUserCommandHandler _loginUserCommandHandler;
    private readonly RegisterUserCommandHandler _registerUserCommandHandler;
    private readonly IMapper _mapper;
    private readonly IValidator<AssignRoleToUserCommand> _assignRoleValidator;
    private readonly IValidator<GrantPermissionToUserCommand> _grantPermissionValidator;
    private readonly IValidator<LoginUserCommand> _loginValidator;
    private readonly IValidator<RegisterUserCommand> _registerValidator;

    public IdentityController(
        AssignRoleToUserCommandHandler assignRoleToUserCommandHandler,
        GetCurrentUserQueryHandler getCurrentUserQueryHandler,
        GrantPermissionToUserCommandHandler grantPermissionToUserCommandHandler,
        LoginUserCommandHandler loginUserCommandHandler,
        RegisterUserCommandHandler registerUserCommandHandler,
        IMapper mapper,
        IValidator<AssignRoleToUserCommand> assignRoleValidator,
        IValidator<GrantPermissionToUserCommand> grantPermissionValidator,
        IValidator<LoginUserCommand> loginValidator,
        IValidator<RegisterUserCommand> registerValidator)
    {
        _assignRoleToUserCommandHandler = assignRoleToUserCommandHandler;
        _getCurrentUserQueryHandler = getCurrentUserQueryHandler;
        _grantPermissionToUserCommandHandler = grantPermissionToUserCommandHandler;
        _loginUserCommandHandler = loginUserCommandHandler;
        _registerUserCommandHandler = registerUserCommandHandler;
        _mapper = mapper;
        _assignRoleValidator = assignRoleValidator;
        _grantPermissionValidator = grantPermissionValidator;
        _loginValidator = loginValidator;
        _registerValidator = registerValidator;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RegisterUserResponse>> Register(
        [FromBody] RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = _mapper.Map<RegisterUserCommand>(request);
        var validationResult = await _registerValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _registerUserCommandHandler.Handle(command, cancellationToken);
        var response = _mapper.Map<RegisterUserResponse>(result);

        return CreatedAtAction(nameof(Me), response);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginUserResponse>> Login(
        [FromBody] LoginUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = _mapper.Map<LoginUserCommand>(request);
        var validationResult = await _loginValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _loginUserCommandHandler.Handle(command, cancellationToken);
        return Ok(_mapper.Map<LoginUserResponse>(result));
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken cancellationToken)
    {
        var result = await _getCurrentUserQueryHandler.Handle(new GetCurrentUserQuery(), cancellationToken);
        return Ok(_mapper.Map<CurrentUserResponse>(result));
    }

    [Authorize(Policy = PermissionConstants.IdentityRolesAssign)]
    [HttpPost("users/{userId:guid}/roles")]
    [ProducesResponseType(typeof(AssignRoleToUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AssignRoleToUserResponse>> AssignRole(
        Guid userId,
        [FromBody] AssignRoleToUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AssignRoleToUserCommand(userId, request.Role);
        var validationResult = await _assignRoleValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _assignRoleToUserCommandHandler.Handle(command, cancellationToken);
        return Ok(_mapper.Map<AssignRoleToUserResponse>(result));
    }

    [Authorize(Policy = PermissionConstants.IdentityPermissionsGrant)]
    [HttpPost("users/{userId:guid}/permissions")]
    [ProducesResponseType(typeof(GrantPermissionToUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GrantPermissionToUserResponse>> GrantPermission(
        Guid userId,
        [FromBody] GrantPermissionToUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = new GrantPermissionToUserCommand(userId, request.Permission);
        var validationResult = await _grantPermissionValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _grantPermissionToUserCommandHandler.Handle(command, cancellationToken);
        return Ok(_mapper.Map<GrantPermissionToUserResponse>(result));
    }
}
