# Crowd Funding Building Blocks Application

## Purpose
Provides reusable application-layer primitives such as dispatchers, pagination models, events, and security abstractions.

## Files
- `CrowdFunding.BuildingBlocks.Application.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.

## Child Folders
- `Events`: Contains shared abstractions for publishing and handling application events.
- `Messaging`: Contains the command and query dispatcher abstractions and their shared registration helpers.
- `Pagination`: Contains paging request and result models shared by list-style queries.
- `Security`: Contains shared security contracts used by handlers to inspect the current user and enforce authorization.
