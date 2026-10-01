FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/Shared/Format.Layout/Format.Layout.csproj src/Shared/Format.Layout/
COPY src/Shared/Format.Security/Format.Security.csproj src/Shared/Format.Security/
COPY src/Shared/Format.ServiceDefaults/Format.ServiceDefaults.csproj src/Shared/Format.ServiceDefaults/
COPY src/Services/Storage/Format.Storage.Api/Format.Storage.Api.csproj src/Services/Storage/Format.Storage.Api/
RUN dotnet restore src/Services/Storage/Format.Storage.Api/Format.Storage.Api.csproj

COPY src/Shared/ src/Shared/
COPY src/Services/Storage/Format.Storage.Api/ src/Services/Storage/Format.Storage.Api/
RUN dotnet publish src/Services/Storage/Format.Storage.Api/Format.Storage.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Format.Storage.Api.dll"]