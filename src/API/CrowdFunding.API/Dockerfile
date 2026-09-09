FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /build

COPY src ./src

WORKDIR /build/src/API/CrowdFunding.API
RUN dotnet restore
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "CrowdFunding.API.dll"]
