FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /build

COPY src/BuildingBlocks ./src/BuildingBlocks
COPY src/Modules/Moderation ./src/Modules/Moderation
COPY src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Contracts ./src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Contracts
COPY src/Modules/Identity/CrowdFunding.Modules.Identity.Contracts ./src/Modules/Identity/CrowdFunding.Modules.Identity.Contracts
COPY services/CrowdFunding.Moderation.Service ./services/CrowdFunding.Moderation.Service

WORKDIR /build/services/CrowdFunding.Moderation.Service
RUN dotnet restore
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "CrowdFunding.Moderation.Service.dll"]
