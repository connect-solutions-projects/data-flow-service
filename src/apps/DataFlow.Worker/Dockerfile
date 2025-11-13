# ========= RUNTIME BASE (ASP.NET para incluir Microsoft.AspNetCore.App) =========
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

# ========= BUILD =========
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# (1) Copia apenas os .csproj para maximizar cache do restore
COPY src/DataFlow.sln ./DataFlow.sln
COPY src/libs/DataFlow.Core.Domain/DataFlow.Core.Domain.csproj ./libs/DataFlow.Core.Domain/DataFlow.Core.Domain.csproj
COPY src/libs/DataFlow.Core.Application/DataFlow.Core.Application.csproj ./libs/DataFlow.Core.Application/DataFlow.Core.Application.csproj
COPY src/libs/DataFlow.Infrastructure/DataFlow.Infrastructure.csproj ./libs/DataFlow.Infrastructure/DataFlow.Infrastructure.csproj
COPY src/libs/DataFlow.Observability/DataFlow.Observability.csproj ./libs/DataFlow.Observability/DataFlow.Observability.csproj
COPY src/libs/DataFlow.Shared/DataFlow.Shared.csproj ./libs/DataFlow.Shared/DataFlow.Shared.csproj
COPY src/apps/DataFlow.Worker/DataFlow.Worker.csproj ./apps/DataFlow.Worker/DataFlow.Worker.csproj

# Restore apenas com os csproj
RUN dotnet restore ./apps/DataFlow.Worker/DataFlow.Worker.csproj

# (2) Agora copia o restante do código
COPY src/libs/ ./libs/
COPY src/apps/ ./apps/

# Publish sem restaurar de novo
RUN dotnet publish ./apps/DataFlow.Worker/DataFlow.Worker.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ========= FINAL =========
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DataFlow.Worker.dll"]
