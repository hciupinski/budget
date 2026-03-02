FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY api/src/Budget.Api/Budget.Api.csproj api/src/Budget.Api/
RUN dotnet restore api/src/Budget.Api/Budget.Api.csproj

COPY api/src/Budget.Api/ api/src/Budget.Api/
RUN dotnet publish api/src/Budget.Api/Budget.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Budget.Api.dll"]
