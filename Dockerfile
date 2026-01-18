# ============================================
# Stage 1: Build
# ============================================
# Use the .NET 8 SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set working directory inside the container
WORKDIR /src

# Copy only the project files needed for the API (not tests)
# Docker caches layers - if these files don't change, restore is skipped
COPY src/Hemglass.ETA.Api/Hemglass.ETA.Api.csproj src/Hemglass.ETA.Api/
COPY src/Hemglass.ETA.Core/Hemglass.ETA.Core.csproj src/Hemglass.ETA.Core/
COPY src/Hemglass.ETA.Infrastructure/Hemglass.ETA.Infrastructure.csproj src/Hemglass.ETA.Infrastructure/

# Restore NuGet packages for the API project (and its dependencies)
RUN dotnet restore src/Hemglass.ETA.Api/Hemglass.ETA.Api.csproj

# Copy the rest of the source code
COPY src/ src/

# Build and publish the API in Release mode
RUN dotnet publish src/Hemglass.ETA.Api/Hemglass.ETA.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ============================================
# Stage 2: Runtime
# ============================================
# Use a smaller runtime-only image (no SDK, ~5x smaller)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Set working directory
WORKDIR /app

# Copy published output from build stage
COPY --from=build /app/publish .

# Expose port 8080 (Azure Container Apps default)
EXPOSE 8080

# Set ASP.NET to listen on port 8080
ENV ASPNETCORE_URLS=http://+:8080

# Start the application
ENTRYPOINT ["dotnet", "Hemglass.ETA.Api.dll"]
