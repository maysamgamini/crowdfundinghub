using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Identity.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Transactions;

namespace CrowdFunding.UnitTests;

/// <summary>
/// In-memory test double for <see cref="ICampaignTransactionExecutor"/>.
/// </summary>
internal sealed class FakeCampaignTransactionExecutor : ICampaignTransactionExecutor
{
    public int InvocationCount { get; private set; }
    public long? LastAdvisoryLockKey { get; private set; }

    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        InvocationCount++;
        return action(cancellationToken);
    }

    public Task<T> ExecuteAsync<T>(long advisoryLockKey, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        InvocationCount++;
        LastAdvisoryLockKey = advisoryLockKey;
        return action(cancellationToken);
    }

    public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        InvocationCount++;
        return action(cancellationToken);
    }

    public Task ExecuteAsync(long advisoryLockKey, Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        InvocationCount++;
        LastAdvisoryLockKey = advisoryLockKey;
        return action(cancellationToken);
    }
}

/// <summary>
/// In-memory test double for <see cref="IContributionTransactionExecutor"/>.
/// </summary>
internal sealed class FakeContributionTransactionExecutor : IContributionTransactionExecutor
{
    public int InvocationCount { get; private set; }

    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        InvocationCount++;
        return action(cancellationToken);
    }

    public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        InvocationCount++;
        return action(cancellationToken);
    }
}

/// <summary>
/// In-memory test double for <see cref="IModerationTransactionExecutor"/>.
/// </summary>
internal sealed class FakeModerationTransactionExecutor : IModerationTransactionExecutor
{
    public int InvocationCount { get; private set; }

    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        InvocationCount++;
        return action(cancellationToken);
    }

    public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        InvocationCount++;
        return action(cancellationToken);
    }
}

/// <summary>
/// In-memory test double for <see cref="IIdentityTransactionExecutor"/>.
/// </summary>
internal sealed class FakeIdentityTransactionExecutor : IIdentityTransactionExecutor
{
    public int InvocationCount { get; private set; }

    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        InvocationCount++;
        return action(cancellationToken);
    }

    public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        InvocationCount++;
        return action(cancellationToken);
    }
}
