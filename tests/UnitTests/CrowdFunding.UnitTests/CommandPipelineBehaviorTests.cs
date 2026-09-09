using CrowdFunding.BuildingBlocks.Application.Audit;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.UnitTests;

/// <summary>
/// TICKET-039: proves the command dispatcher actually chains registered
/// <see cref="ICommandPipelineBehavior{TCommand,TResult}"/> instances around a handler — using a
/// real <see cref="ServiceCollection"/>/<see cref="CommandDispatcher"/>, not a hand-rolled fake,
/// since the whole mechanism is reflection-driven generic type resolution
/// (<c>DispatcherInvoker</c>) that a fake dispatcher couldn't exercise.
/// </summary>
public sealed class CommandDispatcherPipelineTests
{
    [Fact]
    public async Task SendAsync_ShouldRunBehaviorsInRegistrationOrder_OutermostFirst_ThenTheHandler()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<PingCommand, string>>(_ => new PingCommandHandler(log));
        services.AddScoped(typeof(ICommandPipelineBehavior<,>), typeof(FirstLoggingBehavior<,>));
        services.AddScoped(typeof(ICommandPipelineBehavior<,>), typeof(SecondLoggingBehavior<,>));
        services.AddSingleton(log);
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();

        await using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync<string>(new PingCommand(), CancellationToken.None);

        Assert.Equal("pong", result);
        Assert.Equal(["first:before", "second:before", "handler", "second:after", "first:after"], log);
    }

    [Fact]
    public async Task SendAsync_ShouldCallHandlerDirectly_WhenNoBehaviorsAreRegistered()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<PingCommand, string>>(_ => new PingCommandHandler(log));
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();

        await using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync<string>(new PingCommand(), CancellationToken.None);

        Assert.Equal("pong", result);
        Assert.Equal(["handler"], log);
    }

    private sealed record PingCommand;

    private sealed class PingCommandHandler : ICommandHandler<PingCommand, string>
    {
        private readonly List<string> _log;

        public PingCommandHandler(List<string> log) => _log = log;

        public Task<string> Handle(PingCommand command, CancellationToken cancellationToken)
        {
            _log.Add("handler");
            return Task.FromResult("pong");
        }
    }

    private sealed class FirstLoggingBehavior<TCommand, TResult> : ICommandPipelineBehavior<TCommand, TResult>
        where TCommand : class
    {
        private readonly List<string> _log;

        public FirstLoggingBehavior(List<string> log) => _log = log;

        public async Task<TResult> HandleAsync(TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken cancellationToken)
        {
            _log.Add("first:before");
            var result = await next();
            _log.Add("first:after");
            return result;
        }
    }

    private sealed class SecondLoggingBehavior<TCommand, TResult> : ICommandPipelineBehavior<TCommand, TResult>
        where TCommand : class
    {
        private readonly List<string> _log;

        public SecondLoggingBehavior(List<string> log) => _log = log;

        public async Task<TResult> HandleAsync(TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken cancellationToken)
        {
            _log.Add("second:before");
            var result = await next();
            _log.Add("second:after");
            return result;
        }
    }
}

public sealed class AuditLoggingPipelineBehaviorTests
{
    [Fact]
    public async Task HandleAsync_ShouldRecordAuditEntry_ForAnAuditableActionCommand_EvenForANonAdminCaller()
    {
        var currentUser = new TestCurrentUser { UserId = Guid.NewGuid(), Email = "owner@example.com", Roles = ["Creator"] };
        var requestContext = new FakeRequestContext("203.0.113.5", "Mozilla/5.0");
        var auditStore = new FakeAuditStore();
        var behavior = new AuditLoggingPipelineBehavior<AuditableTestCommand, string>(currentUser, requestContext, auditStore);

        var result = await behavior.HandleAsync(
            new AuditableTestCommand("target-1"),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.NotNull(auditStore.LastRecord);
        Assert.Equal("Test.AuditableAction", auditStore.LastRecord!.Action);
        Assert.Equal(currentUser.UserId, auditStore.LastRecord.ActorId);
        Assert.Equal("owner@example.com", auditStore.LastRecord.ActorEmail);
        Assert.Equal("203.0.113.5", auditStore.LastRecord.IpAddress);
        Assert.Equal("Mozilla/5.0", auditStore.LastRecord.UserAgent);
        Assert.Contains("target-1", auditStore.LastRecord.PayloadJson);
    }

    [Fact]
    public async Task HandleAsync_ShouldRecordAuditEntry_ForAnyCommand_WhenCallerIsAdmin()
    {
        var currentUser = new TestCurrentUser { UserId = Guid.NewGuid(), Email = "admin@example.com", Roles = ["Admin"] };
        var auditStore = new FakeAuditStore();
        var behavior = new AuditLoggingPipelineBehavior<PlainTestCommand, string>(
            currentUser, new FakeRequestContext(null, null), auditStore);

        await behavior.HandleAsync(new PlainTestCommand(), () => Task.FromResult("ok"), CancellationToken.None);

        Assert.NotNull(auditStore.LastRecord);
        Assert.Equal(nameof(PlainTestCommand), auditStore.LastRecord!.Action);
        Assert.Equal("unknown", auditStore.LastRecord.IpAddress);
        Assert.Equal("unknown", auditStore.LastRecord.UserAgent);
    }

    [Fact]
    public async Task HandleAsync_ShouldNotRecordAnything_ForAPlainCommandFromANonAdmin()
    {
        var currentUser = new TestCurrentUser { UserId = Guid.NewGuid(), Roles = ["Creator"] };
        var auditStore = new FakeAuditStore();
        var behavior = new AuditLoggingPipelineBehavior<PlainTestCommand, string>(
            currentUser, new FakeRequestContext(null, null), auditStore);

        await behavior.HandleAsync(new PlainTestCommand(), () => Task.FromResult("ok"), CancellationToken.None);

        Assert.Null(auditStore.LastRecord);
    }

    [Fact]
    public async Task HandleAsync_ShouldNotRecordAnything_WhenTheInnerHandlerThrows()
    {
        var currentUser = new TestCurrentUser { UserId = Guid.NewGuid(), Roles = ["Admin"] };
        var auditStore = new FakeAuditStore();
        var behavior = new AuditLoggingPipelineBehavior<PlainTestCommand, string>(
            currentUser, new FakeRequestContext(null, null), auditStore);

        var action = async () => await behavior.HandleAsync(
            new PlainTestCommand(),
            () => throw new InvalidOperationException("handler failed"),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Null(auditStore.LastRecord);
    }

    [AuditableAction("Test.AuditableAction")]
    private sealed record AuditableTestCommand(string TargetId);

    private sealed record PlainTestCommand;
}

internal sealed class FakeRequestContext : IRequestContext
{
    public FakeRequestContext(string? ipAddress, string? userAgent)
    {
        IpAddress = ipAddress;
        UserAgent = userAgent;
    }

    public string? IpAddress { get; }
    public string? UserAgent { get; }
}

internal sealed class FakeAuditStore : IAuditStore
{
    public AuditRecord? LastRecord { get; private set; }

    public Task RecordAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        LastRecord = record;
        return Task.CompletedTask;
    }
}
