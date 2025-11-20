# Comandos Docker - Guia Rápido

## Iniciar Infraestrutura

```bash
docker-compose -f docker-compose.infrastructure.yml --profile infra up -d
```

## Iniciar API

**IMPORTANTE:** A infraestrutura (SQL Server, Redis, etc.) deve estar rodando primeiro!

```bash
# 1. Certifique-se que a infraestrutura está rodando
docker-compose -f docker-compose.infrastructure.yml --profile infra up -d

# 2. Aguarde SQL Server inicializar
docker logs -f sqlserver
# Pressione Ctrl+C quando estiver pronto

# 3. Inicie a API
docker-compose --profile api up -d

# OU usar o nome do serviço diretamente:
docker-compose up -d data-flow-api
```

## Iniciar Worker

**IMPORTANTE:** A infraestrutura (SQL Server, Redis, etc.) deve estar rodando primeiro!

```bash
# 1. Certifique-se que a infraestrutura está rodando
docker-compose -f docker-compose.infrastructure.yml --profile infra up -d

# 2. Aguarde SQL Server inicializar
docker logs -f sqlserver
# Pressione Ctrl+C quando estiver pronto

# 3. Inicie o Worker
docker-compose --profile worker up -d

# OU usar o nome do serviço diretamente:
docker-compose up -d data-flow-worker
```

## Iniciar API + Worker

```bash
docker-compose --profile api --profile worker up -d
```

## Iniciar Tudo (Infra + API + Worker + Proxy)

```bash
# 1. Infraestrutura
docker-compose -f docker-compose.infrastructure.yml --profile infra up -d

# 2. Aguardar SQL Server (verificar logs)
docker logs -f sqlserver
# Pressione Ctrl+C quando estiver pronto

# 3. Aplicações
docker-compose --profile api --profile worker --profile proxy up -d
```

## Verificar Status

```bash
# Ver containers rodando
docker-compose ps

# Ver logs da API
docker logs -f api

# Ver logs do Worker
docker logs -f worker

# Ver logs do SQL Server
docker logs -f sqlserver
```

## Parar Serviços

```bash
# Parar aplicações
docker-compose --profile api --profile worker down

# Parar infraestrutura
docker-compose -f docker-compose.infrastructure.yml --profile infra down

# Parar tudo
docker-compose --profile api --profile worker down
docker-compose -f docker-compose.infrastructure.yml --profile infra down
```

## Rebuild

```bash
# Rebuild da API
docker-compose --profile api build --no-cache

# Rebuild do Worker
docker-compose --profile worker build --no-cache

# Rebuild e subir
docker-compose --profile api build --no-cache
docker-compose --profile api up -d
```

## URLs de Acesso

### API DataFlow
- **API Principal**: http://localhost:8080
- **Swagger/OpenAPI**: http://localhost:8080/swagger
- **Health Check**: http://localhost:8080/health
- **Métricas Prometheus**: http://localhost:8080/metrics
- **Endpoint de Imports**: http://localhost:8080/imports
- **Status de Import**: http://localhost:8080/imports/{batchId}

### Infraestrutura
- **Prometheus**: http://localhost:9090
  - Métricas: http://localhost:9090/metrics
  - Targets: http://localhost:9090/targets
  - Graph: http://localhost:9090/graph
  - Status: http://localhost:9090/status
  - Alerts: http://localhost:9090/alerts
  - Config: http://localhost:9090/config
- **Grafana**: http://localhost:3000
  - Login: http://localhost:3000/login
  - Login padrão: `admin` / `admin`
  - Dashboards: http://localhost:3000/dashboards
  - Data Sources: http://localhost:3000/connections/datasources
  - Explore: http://localhost:3000/explore
- **Alertmanager**: http://localhost:9093
  - Status: http://localhost:9093/#/status
  - Alertas: http://localhost:9093/#/alerts
  - Silences: http://localhost:9093/#/silences
- **RabbitMQ Management**: http://localhost:15672
  - Login: `admin` / `supersecret_admin`
  - Queues: http://localhost:15672/#/queues
  - Connections: http://localhost:15672/#/connections

### Exporters (Métricas)
- **Redis Exporter**: http://localhost:9121/metrics
- **PostgreSQL Exporter**: http://localhost:9187/metrics

### Métricas das Aplicações (Endpoints Prometheus)
- **DataFlow API Metrics**: http://localhost:8080/metrics
  - Métricas OpenTelemetry expostas diretamente
  - Coletadas pelo Prometheus via `api:9090/metrics` (dentro da rede Docker)
- **DataFlow Worker Metrics**: `http://worker:9090/metrics` (dentro da rede Docker)
  - Métricas OpenTelemetry expostas diretamente
  - Coletadas pelo Prometheus via `worker:9090/metrics` (dentro da rede Docker)
  - Para acessar do host: `docker exec worker curl http://localhost:9090/metrics`

### Banco de Dados
- **SQL Server**: `localhost:1433`
  - User: `sa`
  - Password: `DataFlow@Dolar$`
  - Database: `DataFlowDev`
- **PostgreSQL**: `localhost:5432`
  - User: `postgres`
  - Password: `supersecret_admin`
  - Database: `postgres`

## Verificar Métricas

### Testar endpoint de métricas da API
```bash
curl http://localhost:8080/metrics
```

### Verificar targets no Prometheus
Acesse http://localhost:9090/targets e verifique se todos os targets estão "UP":
- `dataflow-api` (api:9090)
- `dataflow-worker` (worker:9090)
- `redis` (redis-exporter:9121)
- `postgres` (postgres-exporter:9187)

### Consultar métricas no Prometheus
Acesse http://localhost:9090/graph e teste queries como:
- `dataflow_batch_total` - Total de batches processados
- `dataflow_batches_processing` - Batches em processamento
- `dataflow_batch_duration_seconds` - Duração do processamento
- `http_server_request_duration_seconds_count{job="dataflow-api"}` - Requisições HTTP
- `dataflow_chunk_total` - Total de chunks enviados
- `dataflow_webhook_deliveries_total` - Webhooks entregues

### Acessar Grafana
1. Acesse http://localhost:3000
2. Login: `admin` / `admin`
3. O datasource Prometheus já está configurado automaticamente
4. Dashboard "DataFlow - Overview" já está disponível em http://localhost:3000/dashboards

