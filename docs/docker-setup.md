# Configuração Docker para DataFlow

Este documento descreve a configuração Docker atualizada para o DataFlow com SQL Server.

## Arquivos Docker

### `docker-compose.infrastructure.yml`
Infraestrutura base (PostgreSQL, SQL Server, Redis, RabbitMQ, Prometheus, Grafana, Alertmanager).

**Serviços:**
- `postgres`: PostgreSQL 16 (porta 5432)
- `sqlserver`: SQL Server 2022 (porta 1433)
- `redis`: Redis 7 (porta 6379)
- `rabbitmq`: RabbitMQ com Management UI (portas 5672, 15672)
- `prometheus`: Prometheus (porta 9090)
- `grafana`: Grafana (porta 3000)
- `alertmanager`: Alertmanager (porta 9093)
- `redis-exporter`: Exporter do Redis para Prometheus (porta 9121)
- `postgres-exporter`: Exporter do PostgreSQL para Prometheus (porta 9187)

### `docker-compose.yml`
Aplicações DataFlow (API, Worker, Reporting).

**Serviços:**
- `data-flow-api`: API principal (porta 8080)
- `data-flow-worker`: Worker de processamento
- `data-flow-reporting`: Serviço de relatórios (porta 8080)
- `proxy`: Nginx reverse proxy (portas 8080, 8443)

## Configurações Importantes

### SQL Server

**Connection String:**
```
Data Source=sqlserver,1433;Initial Catalog=DataFlowDev;User ID=sa;Password=DataFlow@Dolar$;Encrypt=True;TrustServerCertificate=True
```

**Credenciais:**
- SA Password: `DataFlow@Dolar$`
- Database: `DataFlowDev`
- User: `user_data_flow_db` (criado automaticamente)

### Volumes

- `dataflow_imports`: Volume compartilhado para arquivos temporários de importação
  - Path no container: `/var/dataflow/imports`
  - Compartilhado entre API e Worker

### Variáveis de Ambiente

**API e Worker:**
- `ConnectionStrings__DataFlow`: Connection string do SQL Server
- `ImportStorage__BasePath`: `/var/dataflow/imports`
- `OmniFlow__BaseUrl`: URL do OmniFlow (default: `http://omniflow-api:5000`)
- `ImportBatch__ChunkSize`: Tamanho do lote (default: 100)
- `ImportBatch__PollIntervalSeconds`: Intervalo de polling (default: 30)
- `ImportBatch__LockTimeoutMinutes`: Timeout do lock (default: 30)
- `ImportBatch__MaxRetries`: Máximo de retries (default: 3)
- `APPLY_MIGRATIONS_ON_STARTUP`: `true` para aplicar migrations automaticamente

## Como Usar

### 1. Iniciar Infraestrutura

```bash
docker-compose -f docker-compose.infrastructure.yml --profile infra up -d
```

Isso inicia:
- PostgreSQL
- SQL Server
- Redis
- RabbitMQ
- Prometheus
- Grafana
- Alertmanager
- Exporters (Redis e PostgreSQL)

### 2. Aguardar SQL Server

O SQL Server precisa de alguns segundos para inicializar. Verifique os logs:

```bash
docker logs sqlserver
```

### 3. Iniciar Aplicações

```bash
docker-compose --profile api --profile worker up -d
```

Ou iniciar tudo de uma vez:

```bash
docker-compose --profile api --profile worker --profile proxy up -d
```

### 4. Aplicar Migrations

As migrations são aplicadas automaticamente se `APPLY_MIGRATIONS_ON_STARTUP=true`.

Para aplicar manualmente:

```bash
docker exec -it api dotnet ef database update -c IngestionDbContext -p /app/DataFlow.Infrastructure.dll
```

### 5. Verificar Logs

```bash
# API
docker logs -f api

# Worker
docker logs -f worker

# SQL Server
docker logs -f sqlserver
```

## Acessos

- **API**: http://localhost:8080
- **Swagger**: http://localhost:8080/swagger
- **Prometheus**: http://localhost:9090
- **Grafana**: http://localhost:3000 (admin/admin)
- **Alertmanager**: http://localhost:9093
- **RabbitMQ Management**: http://localhost:15672 (admin/supersecret_admin)

## Troubleshooting

### SQL Server não inicia

```bash
# Verificar logs
docker logs sqlserver

# Verificar se porta está livre
netstat -an | findstr 1433
```

### Migrations não aplicam

```bash
# Verificar connection string
docker exec -it api env | grep ConnectionStrings

# Aplicar manualmente
docker exec -it api dotnet ef database update
```

### Worker não processa batches

```bash
# Verificar logs
docker logs -f worker

# Verificar se consegue conectar ao SQL Server
docker exec -it worker ping sqlserver
```

## Próximos Passos

1. Configurar variáveis de ambiente via `.env`
2. Adicionar health checks
3. Configurar backup automático do SQL Server
4. Adicionar monitoring e alertas

