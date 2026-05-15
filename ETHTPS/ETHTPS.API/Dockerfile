FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ETHTPS.API/ETHTPS.API.csproj", "ETHTPS.API/"]
RUN dotnet restore "ETHTPS.API/ETHTPS.API.csproj"
COPY . .
WORKDIR "/src/ETHTPS.API"
RUN dotnet publish "ETHTPS.API.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ETHTPS.API.dll"]
