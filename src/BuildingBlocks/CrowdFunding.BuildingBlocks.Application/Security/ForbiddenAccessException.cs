namespace CrowdFunding.BuildingBlocks.Application.Security;

/// <summary>
/// Represents Forbidden Access Exception.
/// </summary>
public sealed class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message)
        : base(message)
    {
    }
}
