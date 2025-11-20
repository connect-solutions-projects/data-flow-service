# Comandos Docker - Nomes dos Serviços

## ⚠️ IMPORTANTE: Nomes dos Serviços vs Container Names

No `docker-compose.yml`, temos:
- **Nome do serviço**: `data-flow-api` (usado nos comandos docker-compose)
- **Container name**: `api` (usado nos comandos docker diretos)

## Comandos por Serviço

### API (DataFlow.Api)

**Nome do serviço**: `data-flow-api`  
**Container name**: `api`

```bash
# Build
docker-compose build data-flow-api

# Subir
docker-compose up -d data-flow-api
# OU
docker-compose --profile api up -d

# Logs
docker-compose logs -f data-flow-api
# OU (usando container name)
docker logs -f api

# Parar
docker-compose stop data-flow-api
# OU
docker stop api

# Rebuild sem cache
docker-compose build --no-cache data-flow-api
```

### Worker (DataFlow.Worker)

**Nome do serviço**: `data-flow-worker`  
**Container name**: `worker`

```bash
# Build
docker-compose build data-flow-worker

# Subir
docker-compose up -d data-flow-worker
# OU
docker-compose --profile worker up -d

# Logs
docker-compose logs -f data-flow-worker
# OU (usando container name)
docker logs -f worker

# Parar
docker-compose stop data-flow-worker
# OU
docker stop worker

# Rebuild sem cache
docker-compose build --no-cache data-flow-worker
```

### Subir API + Worker juntos

```bash
# Usando profiles
docker-compose --profile api --profile worker up -d

# Usando nomes dos serviços
docker-compose up -d data-flow-api data-flow-worker
```

### Rebuild ambos

```bash
docker-compose build --no-cache data-flow-api data-flow-worker
docker-compose up -d data-flow-api data-flow-worker
```

## Listar Serviços Disponíveis

```bash
# Ver todos os serviços definidos
docker-compose config --services

# Ver containers rodando
docker-compose ps

# Ver todos os containers (incluindo parados)
docker-compose ps -a
```

## Troubleshooting

### Erro: "no such service: api"

**Causa**: Tentou usar o container name em vez do service name.

**Solução**: Use `data-flow-api` em comandos `docker-compose`:

```bash
# ❌ ERRADO
docker-compose logs api

# ✅ CORRETO
docker-compose logs data-flow-api
# OU use o container name diretamente com docker
docker logs api
```

### Verificar se serviço está rodando

```bash
# Ver status
docker-compose ps data-flow-api

# Ver logs
docker-compose logs --tail=50 data-flow-api

# Ver logs em tempo real
docker-compose logs -f data-flow-api
```

