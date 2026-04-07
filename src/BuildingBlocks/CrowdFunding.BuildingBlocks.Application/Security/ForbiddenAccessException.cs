namespace CrowdFunding.BuildingBlocks.Application.Security;

public sealed class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message)
        : base(message)
    {
    }
}
