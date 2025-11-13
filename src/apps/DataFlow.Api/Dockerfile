# ========= RUNTIME BASE =========
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

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
COPY src/apps/DataFlow.Api/DataFlow.Api.csproj ./apps/DataFlow.Api/DataFlow.Api.csproj

# Restore apenas com os csproj
RUN dotnet restore ./apps/DataFlow.Api/DataFlow.Api.csproj

# (2) Agora copia o restante do código
COPY src/libs/ ./libs/
COPY src/apps/ ./apps/

# Publish sem restaurar de novo
RUN dotnet publish ./apps/DataFlow.Api/DataFlow.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ========= FINAL =========
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
## Instala wget para o script de espera do RabbitMQ
RUN apt-get update && apt-get install -y --no-install-recommends wget ca-certificates && rm -rf /var/lib/apt/lists/*

## copia e registra o script
COPY src/apps/DataFlow.Api/wait-for-rabbit.sh /usr/local/bin/wait-for-rabbit.sh
RUN chmod +x /usr/local/bin/wait-for-rabbit.sh

ENTRYPOINT ["/usr/local/bin/wait-for-rabbit.sh"]
