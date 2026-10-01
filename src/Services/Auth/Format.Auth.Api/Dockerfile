FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/Shared/Format.Security/Format.Security.csproj src/Shared/Format.Security/
COPY src/Shared/Format.ServiceDefaults/ src/Shared/Format.ServiceDefaults
COPY src/Shared/Format.ServiceDefaults/Format.ServiceDefaults.csproj src/Shared/Format.ServiceDefaults/
COPY src/Services/Auth/Format.Auth.Api/Format.Auth.Api.csproj src/Services/Auth/Format.Auth.Api/
RUN dotnet restore src/Services/Auth/Format.Auth.Api/Format.Auth.Api.csproj

COPY src/Shared/Format.Security/ src/Shared/Format.Security/
COPY src/Services/Auth/Format.Auth.Api/ src/Services/Auth/Format.Auth.Api/
RUN dotnet publish src/Services/Auth/Format.Auth.Api/Format.Auth.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Format.Auth.Api.dll"]