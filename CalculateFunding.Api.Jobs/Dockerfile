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
RUN wget -qO- https://aka.ms/install-artifacts-credprovider.sh | bash


ARG FEED_PAT

WORKDIR /src
COPY ["CalculateFunding.Api.Jobs/CalculateFunding.Api.Jobs.csproj", "CalculateFunding.Api.Jobs/"]
COPY ["CalculateFunding.Services.Core.AspNet/CalculateFunding.Services.Core.AspNet.csproj", "CalculateFunding.Services.Core.AspNet/"]
COPY ["CalculateFunding.Services.Jobs/CalculateFunding.Services.Jobs.csproj", "CalculateFunding.Services.Jobs/"]
COPY ["CalculateFunding.Models.Jobs/CalculateFunding.Models.Jobs.csproj", "CalculateFunding.Models.Jobs/"]
COPY ["CalculateFunding.Models.Messages/CalculateFunding.Models.Messages.csproj", "CalculateFunding.Models.Messages/"]
COPY ["CalculateFunding.Models.Versioning/CalculateFunding.Models.Versioning.csproj", "CalculateFunding.Models.Versioning/"]
COPY ["CalculateFunding.Services.Core/CalculateFunding.Services.Core.csproj", "CalculateFunding.Services.Core/"]
COPY ["CalculateFunding.Repositories.Common.Search/CalculateFunding.Repositories.Common.Search.csproj", "CalculateFunding.Repositories.Common.Search/"]
COPY ["CalculateFunding.Models.Search/CalculateFunding.Models.Search.csproj", "CalculateFunding.Models.Search/"]
COPY ["CalculateFunding.Services.Processing/CalculateFunding.Services.Processing.csproj", "CalculateFunding.Services.Processing/"]
RUN dotnet nuget list source

RUN dotnet nuget remove source "https://dfe-gov-uk.pkgs.visualstudio.com/_packaging/CalculateFundingService/nuget/v3/index.json" || true
RUN dotnet nuget remove source "https://dfe-gov-uk.pkgs.visualstudio.com/_packaging/ecf37bc0-472b-47bf-91e3-6774809fd194/nuget/v3/index.json" || true

# ✅ ADD BOTH FEEDS cleanly
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

# ✅ Restore
RUN dotnet restore "./CalculateFunding.Api.Jobs/CalculateFunding.Api.Jobs.csproj"
COPY . .
WORKDIR "/src/CalculateFunding.Api.Jobs"
RUN dotnet build "./CalculateFunding.Api.Jobs.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CalculateFunding.Api.Jobs.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CalculateFunding.Api.Jobs.dll"]