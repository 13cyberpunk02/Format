FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/Shared/Format.Layout/Format.Layout.csproj src/Shared/Format.Layout/
COPY src/Shared/Format.Security/Format.Security.csproj src/Shared/Format.Security/
COPY src/Shared/Format.ServiceDefaults/Format.ServiceDefaults.csproj src/Shared/Format.ServiceDefaults/
COPY src/Services/Print/Format.Print.Api/Format.Print.Api.csproj src/Services/Print/Format.Print.Api/
RUN dotnet restore src/Services/Print/Format.Print.Api/Format.Print.Api.csproj

COPY src/Shared/ src/Shared/
COPY src/Services/Print/Format.Print.Api/ src/Services/Print/Format.Print.Api/
RUN dotnet publish src/Services/Print/Format.Print.Api/Format.Print.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app \
    /p:UseAppHost=false

RUN mkdir -p /spool

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app

COPY --from=build /app .
COPY --from=build --chown=1654:1654 /spool /var/spool/format

ENV Spool__RootPath=/var/spool/format

ENTRYPOINT ["dotnet", "Format.Print.Api.dll"]