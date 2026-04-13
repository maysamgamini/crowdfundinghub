# Crowd Funding Architecture Tests

## Purpose
Contains executable architecture rules that verify module boundaries and dependency direction.

## Files
- `CampaignsModuleDependencyTests.cs`: Automated tests that verify behavior or architectural boundaries for the surrounding component.
- `ContributionsModuleDependencyTests.cs`: Automated tests that verify behavior or architectural boundaries for the surrounding component.
- `CrowdFunding.ArchitectureTests.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.
- `IdentityModuleDependencyTests.cs`: Automated tests that verify behavior or architectural boundaries for the surrounding component.
- `ModerationModuleDependencyTests.cs`: Automated tests that verify behavior or architectural boundaries for the surrounding component.

## Child Folders
- None.

## Notes
These tests act as executable documentation for dependency direction and should evolve whenever module boundaries change.
