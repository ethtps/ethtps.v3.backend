FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ETHTPS.ChainRegistry/ETHTPS.ChainRegistry.csproj", "ETHTPS.ChainRegistry/"]
RUN dotnet restore "ETHTPS.ChainRegistry/ETHTPS.ChainRegistry.csproj"
COPY . .
WORKDIR "/src/ETHTPS.ChainRegistry"
RUN dotnet publish "ETHTPS.ChainRegistry.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ETHTPS.ChainRegistry.dll"]
