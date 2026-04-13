using CrowdFunding.BuildingBlocks.Application.Messaging;

namespace CrowdFunding.UnitTests;

internal sealed class RecordingCommandDispatcher : ICommandDispatcher
{
    private readonly object? _result;

    public RecordingCommandDispatcher(object? result = null)
    {
        _result = result;
    }

    public object? LastCommand { get; private set; }

    public int InvocationCount { get; private set; }

    public Task<TResult> SendAsync<TResult>(object command, CancellationToken cancellationToken)
    {
        InvocationCount++;
        LastCommand = command;

        if (_result is null)
        {
            return Task.FromResult(default(TResult)!);
        }

        return Task.FromResult((TResult)_result);
    }
}
