FROM mcr.microsoft.com/dotnet/sdk:10.0-preview-alpine AS build
WORKDIR /src

COPY src/Shared/Shared.csproj src/Shared/
COPY src/Collector/Collector.csproj src/Collector/
RUN dotnet restore src/Collector/Collector.csproj

COPY src/Shared/ src/Shared/
COPY src/Collector/ src/Collector/
RUN dotnet publish src/Collector/Collector.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview-alpine
WORKDIR /app
COPY --from=build /app .
EXPOSE 3000
ENTRYPOINT ["dotnet", "Collector.dll"]
