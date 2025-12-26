FROM mcr.microsoft.com/dotnet/sdk:10.0-preview-alpine AS build
WORKDIR /src

COPY src/Shared/Shared.csproj src/Shared/
COPY src/Api/Api.csproj src/Api/
RUN dotnet restore src/Api/Api.csproj

COPY src/Shared/ src/Shared/
COPY src/Api/ src/Api/
RUN dotnet publish src/Api/Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview-alpine
WORKDIR /app
COPY --from=build /app .
EXPOSE 3001
ENTRYPOINT ["dotnet", "Api.dll"]
