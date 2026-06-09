FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ETHTPS.Processing/ETHTPS.Processing.csproj", "ETHTPS.Processing/"]
RUN dotnet restore "ETHTPS.Processing/ETHTPS.Processing.csproj"
COPY . .
WORKDIR "/src/ETHTPS.Processing"
RUN dotnet publish "ETHTPS.Processing.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ETHTPS.Processing.dll"]
