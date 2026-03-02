FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY worker/src/Budget.Worker/Budget.Worker.csproj worker/src/Budget.Worker/
RUN dotnet restore worker/src/Budget.Worker/Budget.Worker.csproj

COPY worker/src/Budget.Worker/ worker/src/Budget.Worker/
RUN dotnet publish worker/src/Budget.Worker/Budget.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8082
EXPOSE 8082

ENTRYPOINT ["dotnet", "Budget.Worker.dll"]
