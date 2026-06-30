# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY MarketplaceApi.csproj .
RUN dotnet restore

# Copy the rest of the source code
COPY . .

# Build and publish (no --no-restore so it resolves on Linux)
RUN dotnet publish MarketplaceApi.csproj -c Release -o /app/publish

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "MarketplaceApi.dll"]
