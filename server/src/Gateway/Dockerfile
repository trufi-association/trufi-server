FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy solution and project files
COPY server/Gateway.sln .
COPY server/src/Gateway/Gateway.csproj src/Gateway/
COPY server/src/Gateway.Analytics/Gateway.Analytics.csproj src/Gateway.Analytics/
COPY server/src/Gateway.Middleware/Gateway.Middleware.csproj src/Gateway.Middleware/
COPY server/src/Gateway.Shared/Gateway.Shared.csproj src/Gateway.Shared/

# Restore
RUN dotnet restore

# Copy source
COPY server/src/ src/

# Build
WORKDIR /src/src/Gateway
RUN dotnet publish -c Release -o /app

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview
WORKDIR /app
COPY --from=build /app .
EXPOSE 80 443
ENTRYPOINT ["dotnet", "Gateway.dll"]
