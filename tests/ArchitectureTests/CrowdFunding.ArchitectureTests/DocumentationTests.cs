using System.Text.RegularExpressions;

namespace CrowdFunding.ArchitectureTests;

/// <summary>
/// Learners read these documents, so a wrong one teaches the wrong thing. These tests fail the build when
/// a document points at something that does not exist, names a test that does not exist, shows an
/// unfinished diagram fence, or carries maintenance details (commit ids) that do not belong in learner text.
/// </summary>
public sealed partial class DocumentationTests
{
    private static readonly string Root = FindRepositoryRoot();

    // Notes that are working files for backers, not learner text.
    private static readonly string[] NotLearnerText = ["last_state.md", ".agents/", "qa-tickets/"];

    // Path abbreviations that design-to-code-mapping-round1.md defines in its second paragraph
    // ("`CAMP` = src/Modules/Campaigns/CrowdFunding.Modules.Campaigns", and so on). The check expands
    // the abbreviation and then looks for the real path, so a wrong file name behind an abbreviation
    // is still reported. The expansions are kept here, in one place, so they cannot drift unnoticed:
    // if the mapping document changes them, the expanded paths stop existing and the test fails.
    private static readonly (string Prefix, string Expansion)[] PathAbbreviations =
    [
        ("CAMP.", "src/Modules/Campaigns/CrowdFunding.Modules.Campaigns."),
        ("CONT.", "src/Modules/Pledges/CrowdFunding.Modules.Pledges."),
        ("NOTIF.", "src/Modules/Notifications/CrowdFunding.Modules.Notifications."),
        ("BB.", "src/BuildingBlocks/CrowdFunding.BuildingBlocks."),
    ];

    // Files that are named on purpose but are not in this repository.
    // Each entry says why; add a new entry only with a reason of the same kind.
    private static readonly string[] ExternalPathPrefixes =
    [
        // The design source that design-to-code-mapping-round1.md maps onto this code. It belongs
        // to a separate course repository.
        "hispeedsystem/",
    ];

    private static readonly HashSet<string> ProposedFileNames = new(StringComparer.Ordinal)
    {
        // PLATFORM_SHORTCOMINGS.md lists CI systems this repository does NOT use ("No .gitlab-ci.yml,
        // azure-pipelines.yml or Jenkinsfile"). The real pipeline is .github/workflows/ci.yml.
        ".gitlab-ci.yml",
        "azure-pipelines.yml",
    };

    // improvement.md is a historical audit of the pre-hardening template (it says so in its banner).
    // It names files that no longer exist here, or that exist only in the external reference project
    // it adapts from, because that is exactly what it describes. Allowed for that one document.
    private static readonly Dictionary<string, HashSet<string>> HistoricalFileNames = new(StringComparer.Ordinal)
    {
        ["improvement.md"] = new(StringComparer.Ordinal)
        {
            "OutboxProcessorBackgroundService.cs", // the removed central outbox worker
            "GlobalExceptionMiddleware.cs",        // replaced by GlobalExceptionHandler
            "UnitTest1.cs",                        // the placeholder test of the template
            "DeadLetterHandler.cs",                // from the external reference project (apps/backend)
            "CoachingHub.cs",                      // from the external reference project (apps/backend)
            "CrowdFundingWebAppFactory.cs",        // the target name the audit proposes
            "logs/hitechies-.json",                // a log file name from the external reference project
        },
    };

    private static IEnumerable<string> MarkdownFiles() =>
        Directory.EnumerateFiles(Root, "*.md", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(Root, path).Replace('\\', '/'))
            .Where(path => !path.Split('/').Any(part => part is "bin" or "obj" or "node_modules" or ".git"))
            .Where(path => !NotLearnerText.Any(skip => path.StartsWith(skip, StringComparison.Ordinal)))
            .OrderBy(path => path, StringComparer.Ordinal);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrowdFunding.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("CrowdFunding.slnx was not found above the test output.");
    }

    /// <summary>The text of a document with fenced code blocks removed, so examples cannot trip the checks.</summary>
    private static string ProseOf(string relativePath) =>
        FenceRegex().Replace(File.ReadAllText(Path.Combine(Root, relativePath)), string.Empty);

    [GeneratedRegex(@"^```.*?^```[ \t]*$", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex FenceRegex();

    [GeneratedRegex(@"(?<!!)\[[^\]]*\]\((?<target>[^)\s]+)(?:\s+""[^""]*"")?\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"`(?<span>[^`\n]+)`")]
    private static partial Regex CodeSpanRegex();

    [GeneratedRegex(@"\bcommit\s+`?[0-9a-f]{7,40}`?", RegexOptions.IgnoreCase)]
    private static partial Regex CommitIdRegex();

    private static string Resolve(string documentPath, string target)
    {
        var withoutFragment = Uri.UnescapeDataString(target.Split('#')[0].Split('?')[0]);
        var directory = Path.GetDirectoryName(Path.Combine(Root, documentPath))!;
        return Path.GetFullPath(Path.Combine(directory, withoutFragment));
    }

    [Fact]
    public void EveryRelativeLink_PointsAtSomethingThatExists()
    {
        var broken = new List<string>();

        foreach (var document in MarkdownFiles())
        {
            foreach (Match link in LinkRegex().Matches(ProseOf(document)))
            {
                var target = link.Groups["target"].Value;
                if (target.StartsWith('#') || target.Contains("://") || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var path = Resolve(document, target);
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    broken.Add($"{document}: {target}");
                }
            }
        }

        Assert.True(broken.Count == 0, "Links that point at nothing:\n" + string.Join('\n', broken));
    }

    [Fact]
    public void EveryFileNamedInBackticks_ExistsInTheRepository()
    {
        var allFiles = Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
            .Select(path => Path.GetRelativePath(Root, path).Replace('\\', '/'))
            .ToList();
        var names = allFiles.Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);
        var named = new Regex(@"^[A-Za-z0-9_.\-/]+\.(cs|csproj|json|yml|yaml|sh|md)$");
        var missing = new List<string>();

        foreach (var document in MarkdownFiles())
        {
            foreach (Match span in CodeSpanRegex().Matches(ProseOf(document)))
            {
                var value = span.Groups["span"].Value.Trim();
                if (!named.IsMatch(value) || value.Contains('*') || value.Contains("..") || value.StartsWith("./"))
                {
                    continue;
                }

                // A leading slash marks an HTTP route such as /.well-known/jwks.json, not a file.
                if (value.StartsWith('/'))
                {
                    continue;
                }

                if (ProposedFileNames.Contains(value)
                    || ExternalPathPrefixes.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal))
                    || (HistoricalFileNames.TryGetValue(document, out var historical) && historical.Contains(value)))
                {
                    continue;
                }

                foreach (var (prefix, expansion) in PathAbbreviations)
                {
                    if (value.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        value = expansion + value[prefix.Length..];
                        break;
                    }
                }

                var exists = value.Contains('/')
                    ? allFiles.Any(file => file.EndsWith(value, StringComparison.Ordinal))
                    : names.Contains(value);

                if (!exists)
                {
                    missing.Add($"{document}: `{value}`");
                }
            }
        }

        Assert.True(missing.Count == 0, "Files named in backticks that are not in the repository:\n" + string.Join('\n', missing));
    }

    [Fact]
    public void EveryTestClassNamedInBackticks_ExistsInTheTestProjects()
    {
        var classes = Directory.EnumerateFiles(Path.Combine(Root, "tests"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"\bclass\s+(?<name>\w+)").Select(match => match.Groups["name"].Value))
            .ToHashSet(StringComparer.Ordinal);
        var testName = new Regex(@"^[A-Z]\w*Tests$");

        // UnitTests, ArchitectureTests and IntegrationTests are the names of the test project folders
        // (tests/UnitTests and so on), which documents mention when they describe the layout.
        var folderNames = Directory.EnumerateDirectories(Path.Combine(Root, "tests"))
            .Select(path => Path.GetFileName(path))
            .ToHashSet(StringComparer.Ordinal);
        var missing = new List<string>();

        foreach (var document in MarkdownFiles())
        {
            foreach (Match span in CodeSpanRegex().Matches(ProseOf(document)))
            {
                var value = span.Groups["span"].Value.Trim();
                if (testName.IsMatch(value) && !classes.Contains(value) && !folderNames.Contains(value))
                {
                    missing.Add($"{document}: `{value}`");
                }
            }
        }

        Assert.True(missing.Count == 0, "Test classes named in documents that do not exist:\n" + string.Join('\n', missing));
    }

    [Fact]
    public void NoDocumentMentionsACommitId()
    {
        var offenders = MarkdownFiles()
            .Where(document => CommitIdRegex().IsMatch(ProseOf(document)))
            .ToList();

        Assert.True(offenders.Count == 0, "Commit ids do not belong in learner text (git history keeps them):\n" + string.Join('\n', offenders));
    }

    [Fact]
    public void EveryCodeFenceIsClosed()
    {
        var unbalanced = MarkdownFiles()
            .Where(document => File.ReadAllLines(Path.Combine(Root, document)).Count(line => line.TrimStart().StartsWith("```", StringComparison.Ordinal)) % 2 != 0)
            .ToList();

        Assert.True(unbalanced.Count == 0, "Documents with an unclosed code fence:\n" + string.Join('\n', unbalanced));
    }

    [Fact]
    public void NoDocumentDrawsADiagramWithBoxCharacters()
    {
        var offenders = MarkdownFiles()
            .Where(document => File.ReadAllText(Path.Combine(Root, document)).IndexOfAny(['┌', '└', '├', '┐', '┘', '┤', '╔', '╚']) >= 0)
            .ToList();

        Assert.True(offenders.Count == 0, "Draw diagrams with Mermaid, not box characters:\n" + string.Join('\n', offenders));
    }
}
