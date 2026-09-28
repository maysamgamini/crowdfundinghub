using CrowdFunding.API.Controllers;

namespace CrowdFunding.ArchitectureTests;

/// <summary>
/// The production host ships production code only.
/// </summary>
public sealed class ApiHostDependencyTests
{
    [Fact]
    public void Api_ShouldNotReference_AnyTeachingSample()
    {
        var sampleReferences = typeof(IdentityController).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("CrowdFunding.Samples", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(sampleReferences);
    }
}
