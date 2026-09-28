using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.UnitTests;

/// <summary>
/// Exercises <see cref="ValidationPipelineBehavior{TCommand,TResult}"/> through the real
/// <see cref="CommandDispatcher"/>, the same path every endpoint and event handler uses.
/// </summary>
public sealed class ValidationPipelineBehaviorTests
{
    [Fact]
    public async Task SendAsync_ShouldThrowRequestValidationException_AndSkipTheHandler_WhenValidationFails()
    {
        var handler = new RenameHandler();
        await using var provider = BuildProvider(handler, withValidator: true);
        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();

        var action = () => dispatcher.SendAsync<string>(new RenameCommand(""), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(action);
        Assert.Equal(["Name is required."], exception.Errors[nameof(RenameCommand.Name)]);
        Assert.False(handler.WasCalled);
    }

    [Fact]
    public async Task SendAsync_ShouldRunTheHandler_WhenValidationPasses()
    {
        var handler = new RenameHandler();
        await using var provider = BuildProvider(handler, withValidator: true);
        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync<string>(new RenameCommand("Solar Kettle"), CancellationToken.None);

        Assert.Equal("Solar Kettle", result);
        Assert.True(handler.WasCalled);
    }

    [Fact]
    public async Task SendAsync_ShouldPassThrough_WhenNoValidatorIsRegistered()
    {
        var handler = new RenameHandler();
        await using var provider = BuildProvider(handler, withValidator: false);
        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync<string>(new RenameCommand(""), CancellationToken.None);

        Assert.Equal("", result);
        Assert.True(handler.WasCalled);
    }

    private static ServiceProvider BuildProvider(RenameHandler handler, bool withValidator)
    {
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<RenameCommand, string>>(_ => handler);
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();
        services.AddCommandValidation();

        if (withValidator)
        {
            services.AddScoped<IValidator<RenameCommand>, RenameCommandValidator>();
        }

        return services.BuildServiceProvider();
    }

    private sealed record RenameCommand(string Name);

    private sealed class RenameCommandValidator : AbstractValidator<RenameCommand>
    {
        public RenameCommandValidator()
        {
            RuleFor(command => command.Name).NotEmpty().WithMessage("Name is required.");
        }
    }

    private sealed class RenameHandler : ICommandHandler<RenameCommand, string>
    {
        public bool WasCalled { get; private set; }

        public Task<string> Handle(RenameCommand command, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(command.Name);
        }
    }
}
