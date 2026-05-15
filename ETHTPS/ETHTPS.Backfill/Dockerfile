FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ETHTPS.Backfill/ETHTPS.Backfill.csproj", "ETHTPS.Backfill/"]
RUN dotnet restore "ETHTPS.Backfill/ETHTPS.Backfill.csproj"
COPY . .
WORKDIR "/src/ETHTPS.Backfill"
RUN dotnet publish "ETHTPS.Backfill.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ETHTPS.Backfill.dll"]
