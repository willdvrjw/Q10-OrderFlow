# Inventory se compila de forma autónoma, sin depender de Orders.Api.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /workspace
COPY src/OrderFlow.Contracts/OrderFlow.Contracts.csproj src/OrderFlow.Contracts/
COPY src/OrderFlow.Messaging/OrderFlow.Messaging.csproj src/OrderFlow.Messaging/
COPY src/Inventory.Worker/Inventory.Worker.csproj src/Inventory.Worker/
RUN dotnet restore src/Inventory.Worker/Inventory.Worker.csproj
COPY src/ src/
COPY seed/ seed/
RUN dotnet publish src/Inventory.Worker/Inventory.Worker.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# El Worker también expone una API HTTP interna para gestionar existencias.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish/ ./
ENV ASPNETCORE_URLS=http://+:8081
EXPOSE 8081
USER $APP_UID
ENTRYPOINT ["dotnet", "Inventory.Worker.dll"]
