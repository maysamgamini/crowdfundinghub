# Crowd Funding Unit Tests

## Purpose
Contains unit tests for domain rules, command handlers, query handlers, and authorization behavior.

## Files
- `CampaignsTests.cs`: Automated tests that verify behavior or architectural boundaries for the surrounding component.
- `ContributionsTests.cs`: Automated tests that verify behavior or architectural boundaries for the surrounding component.
- `CrowdFunding.UnitTests.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.
- `FakeTransactionExecutors.cs`: Supporting source file for Fake Transaction Executors.
- `IdentityTests.cs`: Automated tests that verify behavior or architectural boundaries for the surrounding component.
- `ModerationTests.cs`: Automated tests that verify behavior or architectural boundaries for the surrounding component.
- `TestCurrentUser.cs`: Supporting source file for Test Current User.

## Child Folders
- None.

## Notes
Most tests exercise domain rules and handler orchestration using in-memory fakes instead of infrastructure dependencies.
