FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY MyPortfolioApi/PersonalApi.csproj MyPortfolioApi/
RUN dotnet restore MyPortfolioApi/PersonalApi.csproj
COPY . .
RUN dotnet publish MyPortfolioApi/PersonalApi.csproj -c Release -o /app/out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out .
USER app
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} exec dotnet PersonalApi.dll"]