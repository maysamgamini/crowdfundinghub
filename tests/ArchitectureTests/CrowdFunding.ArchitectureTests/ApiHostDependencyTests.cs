using CrowdFunding.API.Controllers;
using CrowdFunding.API.Endpoints;
using Microsoft.AspNetCore.Mvc;

namespace CrowdFunding.ArchitectureTests;

/// <summary>
/// Rules for the production API host.
/// </summary>
public sealed class ApiHostDependencyTests
{
    [Fact]
    public void Api_ShouldNotReference_AnyTeachingSample()
    {
        var sampleReferences = typeof(EndpointRegistration).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("CrowdFunding.Samples", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(sampleReferences);
    }

    [Fact]
    public void Api_ShouldExposeMinimalApis_WithExactlyOneControllerKeptForComparison()
    {
        // Endpoints are Minimal APIs (Endpoints/). ModerationController is the single MVC controller
        // kept deliberately for side-by-side comparison; a new controller should be a conscious
        // decision that updates this test and docs/minimal-apis-vs-controllers.md.
        var controllers = typeof(EndpointRegistration).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(type))
            .ToList();

        Assert.Equal([typeof(ModerationController)], controllers);
    }
}
