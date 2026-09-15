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
COPY ["CalculateFunding.Functions.Providers/CalculateFunding.Functions.Providers.csproj", "CalculateFunding.Functions.Providers/"]
COPY ["CalculateFunding.Repositories.Common.EFCore/CalculateFunding.Repositories.Common.EFCore.csproj", "CalculateFunding.Repositories.Common.EFCore/"]
COPY ["CalculateFunding.Services.Core.Functions/CalculateFunding.Services.Core.Functions.csproj", "CalculateFunding.Services.Core.Functions/"]
COPY ["CalculateFunding.Services.Processing/CalculateFunding.Services.Processing.csproj", "CalculateFunding.Services.Processing/"]
COPY ["CalculateFunding.Services.Core/CalculateFunding.Services.Core.csproj", "CalculateFunding.Services.Core/"]
COPY ["CalculateFunding.Models.Versioning/CalculateFunding.Models.Versioning.csproj", "CalculateFunding.Models.Versioning/"]
COPY ["CalculateFunding.Repositories.Common.Search/CalculateFunding.Repositories.Common.Search.csproj", "CalculateFunding.Repositories.Common.Search/"]
COPY ["CalculateFunding.Models.Search/CalculateFunding.Models.Search.csproj", "CalculateFunding.Models.Search/"]
COPY ["CalculateFunding.Services.Providers/CalculateFunding.Services.Providers.csproj", "CalculateFunding.Services.Providers/"]
COPY ["CalculateFunding.Models.ProviderLegacy/CalculateFunding.Models.ProviderLegacy.csproj", "CalculateFunding.Models.ProviderLegacy/"]
COPY ["CalculateFunding.Models.Providers/CalculateFunding.Models.Providers.csproj", "CalculateFunding.Models.Providers/"]

RUN dotnet nuget list source

RUN dotnet nuget remove source "https://dfe-gov-uk.pkgs.visualstudio.com/_packaging/CalculateFundingService/nuget/v3/index.json" || true
RUN dotnet nuget remove source "https://dfe-gov-uk.pkgs.visualstudio.com/_packaging/ecf37bc0-472b-47bf-91e3-6774809fd194/nuget/v3/index.json" || true

# ✅ ADD BOTH FEEDS cleanly
RUN dotnet nuget add source \
    "https://dfe-gov-uk.pkgs.visualstudio.com/_packaging/CalculateFundingService/nuget/v3/index.json" \
    --name "cf-feed" \
    --username "AzureDevOps" \
    --password  "$FEED_PAT" \
    --store-password-in-clear-text

RUN dotnet nuget add source \
    "https://dfe-gov-uk.pkgs.visualstudio.com/_packaging/ecf37bc0-472b-47bf-91e3-6774809fd194/nuget/v3/index.json" \
    --name "cf-feed-2" \
    --username "AzureDevOps" \
    --password  "$FEED_PAT" \
    --store-password-in-clear-text
# ✅ Restore
RUN dotnet restore "./CalculateFunding.Functions.Providers/CalculateFunding.Functions.Providers.csproj"
COPY . .
WORKDIR "/src/CalculateFunding.Functions.Providers"
RUN dotnet build "./CalculateFunding.Functions.Providers.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CalculateFunding.Functions.Providers.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /home/site/wwwroot
COPY --from=publish /app/publish .
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true