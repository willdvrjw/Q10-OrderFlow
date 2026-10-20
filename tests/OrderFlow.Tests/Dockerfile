# Contenedor de pruebas independiente, ejecutado solo bajo el perfil "tests".
# Usa la misma solución .NET 10, sin instalar el SDK en el equipo del evaluador.
FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /workspace
COPY src/OrderFlow.Contracts/OrderFlow.Contracts.csproj src/OrderFlow.Contracts/
COPY src/OrderFlow.Messaging/OrderFlow.Messaging.csproj src/OrderFlow.Messaging/
COPY src/Orders.Api/Orders.Api.csproj src/Orders.Api/
COPY src/Inventory.Worker/Inventory.Worker.csproj src/Inventory.Worker/
COPY tests/OrderFlow.Tests/OrderFlow.Tests.csproj tests/OrderFlow.Tests/
RUN dotnet restore tests/OrderFlow.Tests/OrderFlow.Tests.csproj
COPY src/ src/
COPY tests/ tests/
COPY seed/ seed/
ENTRYPOINT ["dotnet", "test", "tests/OrderFlow.Tests/OrderFlow.Tests.csproj", "-c", "Release", "--no-restore", "--logger", "console;verbosity=normal"]
