using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.ArchitectureTests;

/// <summary>
/// Data ownership, checked on the EF Core model of every module DbContext (discovered from the
/// compiled <c>CrowdFunding.Modules.{Module}.Infrastructure</c> assemblies, so a module added later
/// is covered automatically): each module owns exactly one PostgreSQL schema, named after the
/// module, and maps every table it has into that schema and no other. Building the model needs no
/// database connection. See docs/data-ownership.md.
/// </summary>
public sealed class ModuleSchemaOwnershipTests
{
    private static readonly IReadOnlyList<ModuleModel> ModuleModels = DiscoverModuleModels();

    [Fact]
    public void EveryModule_ShouldHaveExactlyOneDbContext()
    {
        var modules = Directory.GetFiles(AppContext.BaseDirectory, "CrowdFunding.Modules.*.Infrastructure.dll")
            .Select(path => ModuleName(Path.GetFileNameWithoutExtension(path)))
            .ToList();

        Assert.NotEmpty(modules);
        foreach (var module in modules)
        {
            Assert.Single(ModuleModels, model => model.Module == module);
        }
    }

    [Fact]
    public void EveryModuleDbContext_ShouldDeclareItsOwnDefaultSchema_NamedAfterTheModule()
    {
        var violations = ModuleModels
            .Where(model => model.Schema != model.Module.ToLowerInvariant())
            .Select(model => $"{model.ContextName}: default schema is '{model.Schema ?? "(none, so public)"}', expected '{model.Module.ToLowerInvariant()}'")
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void NoTwoModules_ShouldShareASchema()
    {
        var shared = ModuleModels
            .GroupBy(model => model.Schema ?? "public")
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(model => model.ContextName))}")
            .ToList();

        Assert.Empty(shared);
    }

    [Fact]
    public void EveryTableOfAModule_ShouldLiveInThatModulesSchema()
    {
        var violations = ModuleModels
            .SelectMany(model => model.Tables
                .Where(table => table.Schema != model.Schema)
                .Select(table => $"{model.ContextName} maps {table.Entity} to {table.Schema ?? "public"}.{table.Name}, outside its schema '{model.Schema}'"))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void NoModule_ShouldMapATableIntoAnotherModulesSchema()
    {
        var schemaOwners = ModuleModels
            .Where(model => model.Schema is not null)
            .ToDictionary(model => model.Schema!, model => model.Module);

        var violations = ModuleModels
            .SelectMany(model => model.Tables
                .Where(table => table.Schema is not null
                                && schemaOwners.TryGetValue(table.Schema, out var owner)
                                && owner != model.Module)
                .Select(table => $"{model.ContextName} maps {table.Entity} into {schemaOwners[table.Schema!]}'s schema '{table.Schema}'"))
            .ToList();

        Assert.Empty(violations);
    }

    private static string ModuleName(string infrastructureAssemblyName)
        => infrastructureAssemblyName["CrowdFunding.Modules.".Length..^".Infrastructure".Length];

    private static IReadOnlyList<ModuleModel> DiscoverModuleModels()
        => Directory.GetFiles(AppContext.BaseDirectory, "CrowdFunding.Modules.*.Infrastructure.dll")
            .Select(path => Assembly.Load(Path.GetFileNameWithoutExtension(path)))
            .SelectMany(assembly => assembly.GetTypes()
                .Where(type => type is { IsAbstract: false } && typeof(DbContext).IsAssignableFrom(type))
                .Select(type => BuildModel(ModuleName(assembly.GetName().Name!), type)))
            .ToList();

    private static ModuleModel BuildModel(string module, Type contextType)
    {
        var optionsBuilder = (DbContextOptionsBuilder)Activator.CreateInstance(
            typeof(DbContextOptionsBuilder<>).MakeGenericType(contextType))!;

        // The model is built from the mapping alone; this connection string is never opened.
        optionsBuilder.UseNpgsql("Host=model-only;Database=model-only");

        using var context = (DbContext)Activator.CreateInstance(contextType, optionsBuilder.Options)!;
        var model = context.Model;

        var tables = model.GetEntityTypes()
            .Where(entityType => entityType.GetTableName() is not null)
            .Select(entityType => new MappedTable(entityType.DisplayName(), entityType.GetSchema(), entityType.GetTableName()!))
            .ToList();

        return new ModuleModel(module, contextType.Name, model.GetDefaultSchema(), tables);
    }

    private sealed record ModuleModel(string Module, string ContextName, string? Schema, IReadOnlyList<MappedTable> Tables);

    private sealed record MappedTable(string Entity, string? Schema, string Name);
}
