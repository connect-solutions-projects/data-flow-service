# Guia de Uso dos Docker Compose Files

Este projeto utiliza dois arquivos Docker Compose separados para organizar melhor a infraestrutura e as aplicações.

## 📁 Arquivos

1. **`docker-compose.infrastructure.yml`** - Infraestrutura (PostgreSQL, Redis, RabbitMQ, Prometheus, Grafana, Exporters)
2. **`docker-compose.yml`** - Aplicações (API, Worker, Reporting, Proxy)

## 🚀 Como Usar

### Opção 1: Subir Tudo Separadamente

#### 1. Primeiro, suba a infraestrutura:

```bash
docker compose -f docker-compose.infrastructure.yml --profile infra up -d
```

Isso criará:
- PostgreSQL (porta 5432)
- Redis (porta 6379)
- RabbitMQ (portas 5672, 15672)
- Prometheus (porta 9090)
- Grafana (porta 3000)
- Redis Exporter (porta 9121)
- PostgreSQL Exporter (porta 9187)
- Rede `dev_net`

#### 2. Depois, suba as aplicações:

```bash
docker compose --profile proxy --profile api --profile worker --profile reporting up -d
```

Isso criará:
- DataFlow API
- DataFlow Worker
- DataFlow Reporting Service
- Nginx Proxy

### Opção 2: Subir Tudo de Uma Vez

```bash
# Subir infraestrutura
docker compose -f docker-compose.infrastructure.yml --profile infra up -d

# Subir aplicações
docker compose --profile proxy --profile api --profile worker --profile reporting up -d
```

### Opção 3: Usar Arquivo Único (Futuro)

Você pode combinar os dois arquivos em um único `docker-compose.yml` se preferir.

## 📋 Comandos Úteis

### Ver logs

```bash
# Logs da infraestrutura
docker compose -f docker-compose.infrastructure.yml logs -f

# Logs das aplicações
docker compose logs -f

# Logs de um serviço específico
docker compose logs -f data-flow-api
```

### Parar serviços

```bash
# Parar aplicações
docker compose --profile proxy --profile api --profile worker --profile reporting down

# Parar infraestrutura
docker compose -f docker-compose.infrastructure.yml --profile infra down

# Parar tudo (incluindo volumes)
docker compose -f docker-compose.infrastructure.yml --profile infra down -v
docker compose --profile proxy --profile api --profile worker --profile reporting down
```

### Rebuild

```bash
# Rebuild das aplicações
docker compose --profile proxy --profile api --profile worker --profile reporting up -d --build
```

### Status dos containers

```bash
docker compose ps
docker compose -f docker-compose.infrastructure.yml ps
```

## 🔧 Configuração da Rede

A rede `dev_net` é criada pelo arquivo de infraestrutura e compartilhada com as aplicações. Ambos os arquivos usam a mesma rede para comunicação entre serviços.

## ⚠️ Ordem de Inicialização

**Importante**: Sempre suba a infraestrutura primeiro, pois as aplicações dependem dela:

1. ✅ Infraestrutura (PostgreSQL, Redis, RabbitMQ, etc.)
2. ✅ Aplicações (API, Worker, Reporting)

## 📊 Acessos

Após subir tudo:

- **API Swagger**: https://api.local:8443/swagger
- **Reporting Swagger**: https://reporting.local:8444/swagger
- **Grafana**: http://localhost:3000
- **Prometheus**: http://localhost:9090
- **RabbitMQ Management**: http://localhost:15672 (admin/supersecret_admin)

## 🐛 Troubleshooting

### Erro: "network dev_net not found"

Execute primeiro:
```bash
docker compose -f docker-compose.infrastructure.yml --profile infra up -d
```

### Erro: "port already in use"

Verifique se algum serviço já está usando a porta:
```bash
netstat -ano | findstr :5432
netstat -ano | findstr :6379
```

### Limpar tudo e recomeçar

```bash
# Parar e remover tudo
docker compose -f docker-compose.infrastructure.yml --profile infra down -v
docker compose --profile proxy --profile api --profile worker --profile reporting down

# Remover rede manualmente se necessário
docker network rm dev_net

# Recriar tudo
docker compose -f docker-compose.infrastructure.yml --profile infra up -d
docker compose --profile proxy --profile api --profile worker --profile reporting up -d
```

