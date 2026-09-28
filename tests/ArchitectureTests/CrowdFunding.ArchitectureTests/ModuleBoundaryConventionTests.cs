using System.Reflection;
using System.Text.RegularExpressions;

namespace CrowdFunding.ArchitectureTests;

/// <summary>
/// Convention-based rules applied to every module at once, discovered from the compiled
/// <c>CrowdFunding.Modules.{Module}.{Layer}</c> assemblies. A module added later is covered
/// without writing a new test class. These are the rules that keep any module extractable into
/// its own service: nothing outside a module can see more of it than its Contracts.
/// </summary>
public sealed partial class ModuleBoundaryConventionTests
{
    private static readonly string[] Layers = ["Contracts", "Domain", "Application", "Infrastructure"];

    private static readonly IReadOnlyList<ModuleAssembly> ModuleAssemblies = DiscoverModuleAssemblies();

    [Fact]
    public void EveryModule_ShouldHaveAllFourLayers()
    {
        var modules = ModuleAssemblies.GroupBy(assembly => assembly.Module).ToList();

        Assert.NotEmpty(modules);
        foreach (var module in modules)
        {
            Assert.Equal(Layers.Order(), module.Select(assembly => assembly.Layer).Order());
        }
    }

    [Fact]
    public void Modules_ShouldReferenceOtherModules_OnlyThroughTheirContracts()
    {
        var violations = ModuleAssemblies
            .SelectMany(source => ReferencedModuleAssemblies(source.Assembly)
                .Where(target => target.Module != source.Module && target.Layer != "Contracts")
                .Select(target => $"{source.Name} -> {target.Name}"))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void DomainAssemblies_ShouldReferenceNoOtherCrowdFundingAssembly_ThanBuildingBlocksDomain()
    {
        var violations = ModuleAssemblies
            .Where(assembly => assembly.Layer == "Domain")
            .SelectMany(domain => domain.Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(name => name.StartsWith("CrowdFunding.", StringComparison.Ordinal)
                               && name != "CrowdFunding.BuildingBlocks.Domain")
                .Select(name => $"{domain.Name} -> {name}"))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void ContractsAssemblies_ShouldNotReferenceAnyModuleInternals()
    {
        var violations = ModuleAssemblies
            .Where(assembly => assembly.Layer == "Contracts")
            .SelectMany(contracts => ReferencedModuleAssemblies(contracts.Assembly)
                .Where(target => target.Layer != "Contracts")
                .Select(target => $"{contracts.Name} -> {target.Name}"))
            .ToList();

        Assert.Empty(violations);
    }

    private static IEnumerable<ModuleAssembly> ReferencedModuleAssemblies(Assembly assembly)
        => assembly.GetReferencedAssemblies()
            .Select(reference => Parse(reference.Name!))
            .OfType<ModuleAssembly>();

    private static IReadOnlyList<ModuleAssembly> DiscoverModuleAssemblies()
        => Directory.GetFiles(AppContext.BaseDirectory, "CrowdFunding.Modules.*.dll")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Select(Parse)
            .OfType<ModuleAssembly>()
            .ToList();

    private static ModuleAssembly? Parse(string assemblyName)
    {
        var match = ModuleAssemblyName().Match(assemblyName);
        return match.Success && Layers.Contains(match.Groups["layer"].Value)
            ? new ModuleAssembly(assemblyName, match.Groups["module"].Value, match.Groups["layer"].Value)
            : null;
    }

    [GeneratedRegex(@"^CrowdFunding\.Modules\.(?<module>\w+)\.(?<layer>\w+)$")]
    private static partial Regex ModuleAssemblyName();

    private sealed record ModuleAssembly(string Name, string Module, string Layer)
    {
        public Assembly Assembly => Assembly.Load(Name);
    }
}
