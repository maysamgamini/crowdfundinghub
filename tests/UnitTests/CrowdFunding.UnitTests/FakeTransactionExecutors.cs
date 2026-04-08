using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Transactions;

namespace CrowdFunding.UnitTests;

internal sealed class FakeCampaignTransactionExecutor : ICampaignTransactionExecutor
{
    public int InvocationCount { get; private set; }

    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        InvocationCount++;
        return action(cancellationToken);
    }
}

internal sealed class FakeContributionTransactionExecutor : IContributionTransactionExecutor
{
    public int InvocationCount { get; private set; }

    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        InvocationCount++;
        return action(cancellationToken);
    }
}

internal sealed class FakeModerationTransactionExecutor : IModerationTransactionExecutor
{
    public int InvocationCount { get; private set; }

    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        InvocationCount++;
        return action(cancellationToken);
    }
}
