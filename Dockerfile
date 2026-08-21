# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files (no solution file needed)
# Central package/build settings — must be present before restore
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Cinema.Domain/Cinema.Domain.csproj src/Cinema.Domain/
COPY src/Cinema.Application/Cinema.Application.csproj src/Cinema.Application/
COPY src/Cinema.Contracts/Cinema.Contracts.csproj src/Cinema.Contracts/
COPY src/Cinema.Persistence.Read/Cinema.Persistence.Read.csproj src/Cinema.Persistence.Read/
COPY src/Cinema.ServiceDiscovery/Cinema.ServiceDiscovery.csproj src/Cinema.ServiceDiscovery/
COPY src/Cinema.Infrastructure/Cinema.Infrastructure.csproj src/Cinema.Infrastructure/
COPY src/Cinema.Api/Cinema.Api.csproj src/Cinema.Api/

# Restore dependencies for Cinema.Api project (this will restore all dependencies)
RUN dotnet restore src/Cinema.Api/Cinema.Api.csproj

# Copy everything else
COPY src/ src/

# Build the application
WORKDIR /src/src/Cinema.Api
RUN dotnet build Cinema.Api.csproj -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
WORKDIR /src/src/Cinema.Api
RUN dotnet publish Cinema.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# curl is not present in the aspnet runtime image; HEALTHCHECK below needs it
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Copy published files
COPY --from=publish /app/publish .

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl --fail http://localhost:8080/health || exit 1

# Run the application
ENTRYPOINT ["dotnet", "Cinema.Api.dll"]
