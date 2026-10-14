# Сборка
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY BookClub.csproj .
RUN dotnet restore BookClub.csproj
COPY . .
RUN dotnet publish BookClub.csproj -c Release -o /app --no-restore

# Запуск
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "BookClub.dll"]
