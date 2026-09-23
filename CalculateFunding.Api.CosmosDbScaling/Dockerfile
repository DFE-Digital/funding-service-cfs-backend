# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ARG BUILD_CONFIGURATION=Release

# Install Azure DevOps Artifacts Credential Provider
RUN wget -qO- https://aka.ms/install-artifacts-credprovider.sh | bash

ARG FEED_PAT

WORKDIR /src

COPY ["CalculateFunding.Api.CosmosDbScaling/CalculateFunding.Api.CosmosDbScaling.csproj", "CalculateFunding.Api.CosmosDbScaling/"]
COPY ["CalculateFunding.Services.Core.AspNet/CalculateFunding.Services.Core.AspNet.csproj", "CalculateFunding.Services.Core.AspNet/"]
COPY ["CalculateFunding.Services.CosmosDbScaling/CalculateFunding.Services.CosmosDbScaling.csproj", "CalculateFunding.Services.CosmosDbScaling/"]
COPY ["CalculateFunding.Models.CosmosDbScaling/CalculateFunding.Models.CosmosDbScaling.csproj", "CalculateFunding.Models.CosmosDbScaling/"]
COPY ["CalculateFunding.Services.Core/CalculateFunding.Services.Core.csproj", "CalculateFunding.Services.Core/"]
COPY ["CalculateFunding.Models.Versioning/CalculateFunding.Models.Versioning.csproj", "CalculateFunding.Models.Versioning/"]
COPY ["CalculateFunding.Repositories.Common.Search/CalculateFunding.Repositories.Common.Search.csproj", "CalculateFunding.Repositories.Common.Search/"]
COPY ["CalculateFunding.Models.Search/CalculateFunding.Models.Search.csproj", "CalculateFunding.Models.Search/"]
COPY ["CalculateFunding.Services.Processing/CalculateFunding.Services.Processing.csproj", "CalculateFunding.Services.Processing/"]

RUN dotnet nuget remove source "https://dfe-gov-uk.pkgs.visualstudio.com/_packaging/CalculateFundingService/nuget/v3/index.json" || true
RUN dotnet nuget remove source "https://dfe-gov-uk.pkgs.visualstudio.com/_packaging/ecf37bc0-472b-47bf-91e3-6774809fd194/nuget/v3/index.json" || true

RUN dotnet nuget add source \
    "https://dfe-gov-uk.pkgs.visualstudio.com/_packaging/CalculateFundingService/nuget/v3/index.json" \
    --name "cf-feed" \
    --username "AzureDevOps" \
    --password "$FEED_PAT" \
    --store-password-in-clear-text

RUN dotnet nuget add source \
    "https://dfe-gov-uk.pkgs.visualstudio.com/_packaging/ecf37bc0-472b-47bf-91e3-6774809fd194/nuget/v3/index.json" \
    --name "cf-feed-2" \
    --username "AzureDevOps" \
    --password "$FEED_PAT" \
    --store-password-in-clear-text

RUN dotnet nuget list source

RUN dotnet restore "./CalculateFunding.Api.CosmosDbScaling/CalculateFunding.Api.CosmosDbScaling.csproj"

COPY . .

WORKDIR "/src/CalculateFunding.Api.CosmosDbScaling"

RUN dotnet build "./CalculateFunding.Api.CosmosDbScaling.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build


# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish

ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "./CalculateFunding.Api.CosmosDbScaling.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false


# This stage is used in production or when running from VS in regular mode
# (Default when not using the Debug configuration)
FROM base AS final

WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "CalculateFunding.Api.CosmosDbScaling.dll"]
