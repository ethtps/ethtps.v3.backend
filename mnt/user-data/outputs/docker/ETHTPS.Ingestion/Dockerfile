FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ETHTPS.Ingestion/ETHTPS.Ingestion.csproj", "ETHTPS.Ingestion/"]
RUN dotnet restore "ETHTPS.Ingestion/ETHTPS.Ingestion.csproj"
COPY . .
WORKDIR "/src/ETHTPS.Ingestion"
RUN dotnet publish "ETHTPS.Ingestion.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ETHTPS.Ingestion.dll"]
