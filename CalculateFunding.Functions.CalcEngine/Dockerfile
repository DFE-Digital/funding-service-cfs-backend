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
COPY ["CalculateFunding.Functions.CalcEngine/CalculateFunding.Functions.CalcEngine.csproj", "CalculateFunding.Functions.CalcEngine/"]
COPY ["CalculateFunding.Services.CalcEngine/CalculateFunding.Services.CalcEngine.csproj", "CalculateFunding.Services.CalcEngine/"]
COPY ["CalculateFunding.Models.Calcs/CalculateFunding.Models.Calcs.csproj", "CalculateFunding.Models.Calcs/"]
COPY ["CalculateFunding.Models.Datasets/CalculateFunding.Models.Datasets.csproj", "CalculateFunding.Models.Datasets/"]
COPY ["CalculateFunding.Models.Search/CalculateFunding.Models.Search.csproj", "CalculateFunding.Models.Search/"]
COPY ["CalculateFunding.Models.Versioning/CalculateFunding.Models.Versioning.csproj", "CalculateFunding.Models.Versioning/"]
COPY ["CalculateFunding.Models.ProviderLegacy/CalculateFunding.Models.ProviderLegacy.csproj", "CalculateFunding.Models.ProviderLegacy/"]
COPY ["CalculateFunding.Repositories.Common.EFCore/CalculateFunding.Repositories.Common.EFCore.csproj", "CalculateFunding.Repositories.Common.EFCore/"]
COPY ["CalculateFunding.Services.CodeGeneration.VisualBasic.Type/CalculateFunding.Services.CodeGeneration.VisualBasic.Type.csproj", "CalculateFunding.Services.CodeGeneration.VisualBasic.Type/"]
COPY ["CalculateFunding.Services.CodeGeneration.VisualBasic/CalculateFunding.Services.CodeGeneration.VisualBasic.csproj", "CalculateFunding.Services.CodeGeneration.VisualBasic/"]
COPY ["CalculateFunding.Models.Publishing/CalculateFunding.Models.Publishing.csproj", "CalculateFunding.Models.Publishing/"]
COPY ["CalculateFunding.Services.Core/CalculateFunding.Services.Core.csproj", "CalculateFunding.Services.Core/"]
COPY ["CalculateFunding.Repositories.Common.Search/CalculateFunding.Repositories.Common.Search.csproj", "CalculateFunding.Repositories.Common.Search/"]
COPY ["CalculateFunding.Services.CodeGeneration/CalculateFunding.Services.CodeGeneration.csproj", "CalculateFunding.Services.CodeGeneration/"]
COPY ["CalculateFunding.Services.CodeMetadataGenerator/CalculateFunding.Services.CodeMetadataGenerator.csproj", "CalculateFunding.Services.CodeMetadataGenerator/"]
COPY ["CalculateFunding.Models.Code/CalculateFunding.Models.Code.csproj", "CalculateFunding.Models.Code/"]
COPY ["CalculateFunding.Services.Processing/CalculateFunding.Services.Processing.csproj", "CalculateFunding.Services.Processing/"]
COPY ["CalculateFunding.Services.Calcs/CalculateFunding.Services.Calcs.csproj", "CalculateFunding.Services.Calcs/"]
COPY ["CalculateFunding.Models.Messages/CalculateFunding.Models.Messages.csproj", "CalculateFunding.Models.Messages/"]
COPY ["CalculateFunding.Services.Compiler/CalculateFunding.Services.Compiler.csproj", "CalculateFunding.Services.Compiler/"]
COPY ["CalculateFunding.Models.Graph/CalculateFunding.Models.Graph.csproj", "CalculateFunding.Models.Graph/"]
COPY ["CalculateFunding.Services.Core.Functions/CalculateFunding.Services.Core.Functions.csproj", "CalculateFunding.Services.Core.Functions/"]

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
RUN dotnet restore "./CalculateFunding.Functions.CalcEngine/CalculateFunding.Functions.CalcEngine.csproj"
COPY . .
WORKDIR "/src/CalculateFunding.Functions.CalcEngine"
RUN dotnet build "./CalculateFunding.Functions.CalcEngine.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CalculateFunding.Functions.CalcEngine.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /home/site/wwwroot
COPY --from=publish /app/publish .
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true