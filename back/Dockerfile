FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY back/FamilyExpenses.Api.csproj back/
RUN dotnet restore back/FamilyExpenses.Api.csproj

COPY back/ back/
RUN dotnet publish back/FamilyExpenses.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "FamilyExpenses.Api.dll"]
