FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/FIAP.CloudGames.Jogo.API.csproj", "src/"]
RUN dotnet restore "src/FIAP.CloudGames.Jogo.API.csproj"
COPY . .
WORKDIR "/src/src"
RUN dotnet build "FIAP.CloudGames.Jogo.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FIAP.CloudGames.Jogo.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FIAP.CloudGames.Jogo.API.dll"]
