# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
# Removed Playwright OS dependencies install
# USER root
# RUN apt-get update && apt-get install -y --no-install-recommends ... && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
# Removed PowerShell install
# RUN apt-get update && apt-get install -y powershell && rm -rf /var/lib/apt/lists/*
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Tsintra.Api/Tsintra.Api.csproj", "Tsintra.Api/"]
# Copy all project files for restore
COPY ["Tsintra.Application/Tsintra.App.csproj", "Tsintra.Application/"]
COPY ["Tsintra.Domain/Tsintra.Core.csproj", "Tsintra.Domain/"]
COPY ["Tsintra.Integrations/Tsintra.Integrations.csproj", "Tsintra.Integrations/"]
COPY ["Tsintra.MarketplaceAgent/Tsintra.MarketplaceAgent.csproj", "Tsintra.MarketplaceAgent/"]
COPY ["Tsintra.Persistence/Tsintra.Persistence.csproj", "Tsintra.Persistence/"]
COPY ["Tsintra.sln", "Tsintra.sln"]
RUN dotnet restore "./Tsintra.sln"
# Copy the rest of the codebase
COPY . .

# Removed Playwright browser install step
# WORKDIR "/src/Tsintra.Api"
# RUN pwsh -Command "playwright install --with-deps chromium"

# Build the project
# Ensure working directory is correct if Playwright install step was removed
WORKDIR "/src/Tsintra.Api" 
RUN dotnet build "./Tsintra.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Tsintra.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Tsintra.Api.dll"]