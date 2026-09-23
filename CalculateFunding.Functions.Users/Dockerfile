# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0 AS base
WORKDIR /home/site/wwwroot
EXPOSE 8080


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG FEED_PAT
WORKDIR /src
COPY ["CalculateFunding.Functions.Users/CalculateFunding.Functions.Users.csproj", "CalculateFunding.Functions.Users/"]
COPY ["CalculateFunding.Services.Core.Functions/CalculateFunding.Services.Core.Functions.csproj", "CalculateFunding.Services.Core.Functions/"]
COPY ["CalculateFunding.Services.Core/CalculateFunding.Services.Core.csproj", "CalculateFunding.Services.Core/"]
COPY ["CalculateFunding.Models.Versioning/CalculateFunding.Models.Versioning.csproj", "CalculateFunding.Models.Versioning/"]
COPY ["CalculateFunding.Repositories.Common.Search/CalculateFunding.Repositories.Common.Search.csproj", "CalculateFunding.Repositories.Common.Search/"]
COPY ["CalculateFunding.Models.Search/CalculateFunding.Models.Search.csproj", "CalculateFunding.Models.Search/"]
COPY ["CalculateFunding.Services.Processing/CalculateFunding.Services.Processing.csproj", "CalculateFunding.Services.Processing/"]
COPY ["CalculateFunding.Services.Users/CalculateFunding.Services.Users.csproj", "CalculateFunding.Services.Users/"]
COPY ["CalculateFunding.Models.Messages/CalculateFunding.Models.Messages.csproj", "CalculateFunding.Models.Messages/"]
COPY ["CalculateFunding.Models.Users/CalculateFunding.Models.Users.csproj", "CalculateFunding.Models.Users/"]

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

# Restore
RUN dotnet restore "./CalculateFunding.Functions.Users/CalculateFunding.Functions.Users.csproj"
COPY . .
WORKDIR "/src/CalculateFunding.Functions.Users"
RUN dotnet build "./CalculateFunding.Functions.Users.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CalculateFunding.Functions.Users.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /home/site/wwwroot
COPY --from=publish /app/publish .
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true