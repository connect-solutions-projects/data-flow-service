# Plano de Integração Redis e RabbitMQ - DataFlow

## Situação Atual

### Redis (Já Implementado)
- ✅ **Rate Limiting**: `IRedisRateLimiter` (mas middleware usa MemoryCache)
- ✅ **Locks**: `IRedisLockService` (não usado no batch processing)
- ✅ **Deduplicação**: `IChecksumDedupService` para checksums
- ✅ **Cache Distribuído**: `IDistributedCache` via StackExchange.Redis

### RabbitMQ (Já Implementado)
- ✅ **MassTransit**: Configurado no Worker e API
- ✅ **Consumer**: `ProcessJobConsumer` no Worker
- ⚠️ **Não usado para notificar batches prontos**

### Problemas Atuais
1. **Rate Limiting**: Middleware usa `MemoryCache` em vez de Redis (não compartilhado entre instâncias)
2. **Mutex de Batch**: Usa SQL Server `BatchLocks` em vez de Redis (mais lento, não distribuído)
3. **Worker Polling**: Faz polling no SQL Server a cada 30s em vez de receber eventos via RabbitMQ
4. **Notificações**: Não há eventos publicados quando batch é criado/processado

## Melhorias Propostas

### 1. Migrar Rate Limiting para Redis

**Objetivo**: Rate limiting compartilhado entre múltiplas instâncias da API

**Implementação**:
- Substituir `ClientRateLimitMiddleware` (MemoryCache) por `RedisRateLimiter`
- Usar `IRedisRateLimiter` que já existe
- Chave: `ratelimit:client:{clientId}:{window}`

**Benefícios**:
- Rate limit compartilhado entre instâncias
- Mais preciso em ambiente distribuído
- Melhor para escalabilidade horizontal

### 2. Migrar Mutex de Batch para Redis (RedLock)

**Objetivo**: Lock distribuído mais rápido e escalável

**Implementação**:
- Criar `RedisBatchLockRepository` usando RedLock
- Manter `SqlBatchLockRepository` como fallback
- Abstrair em `IBatchLockRepository` com estratégia configurável

**Benefícios**:
- Mais rápido que SQL Server
- Suporta múltiplos workers em diferentes regiões
- Timeout automático via TTL do Redis

### 3. Publicar Eventos no RabbitMQ

**Objetivo**: Worker recebe notificações imediatas em vez de polling

**Eventos a Publicar**:
- `BatchCreated` - Quando novo batch é criado via POST /imports
- `BatchReady` - Quando batch está pronto para processar (após avaliação de policies)
- `BatchCompleted` - Quando batch é finalizado (opcional, já temos webhooks)

**Implementação**:
- Usar MassTransit para publicar eventos
- Worker consome `BatchReady` e processa imediatamente
- Manter polling como fallback se RabbitMQ estiver indisponível

**Benefícios**:
- Processamento quase instantâneo (sem esperar polling)
- Menos carga no SQL Server
- Melhor latência

### 4. Cache de Clientes e Policies no Redis

**Objetivo**: Reduzir consultas ao SQL Server

**Implementação**:
- Cache de `Client` por `ClientIdentifier` (TTL: 5min)
- Cache de `ClientPolicy` por `ClientId` (TTL: 10min)
- Invalidar cache quando cliente/policy for atualizado

**Benefícios**:
- Menos carga no SQL Server
- Resposta mais rápida na validação de credenciais
- Melhor performance geral

## Arquitetura Proposta

```
┌─────────────┐
│   API 1-N   │
└──────┬──────┘
       │
       ├─► Redis (Rate Limit, Cache, Locks)
       │
       └─► RabbitMQ (BatchCreated, BatchReady)
              │
              ▼
       ┌─────────────┐
       │ Worker 1-N  │
       └──────┬──────┘
              │
              ├─► Redis (Locks)
              │
              └─► SQL Server (Persistência)
```

## Implementação por Fase

### Fase 1: Rate Limiting com Redis (Prioritário)
- [ ] Substituir `ClientRateLimitMiddleware` para usar `IRedisRateLimiter`
- [ ] Remover `MemoryCache` do rate limiting
- [ ] Testar com múltiplas instâncias

### Fase 2: Mutex com Redis RedLock
- [ ] Implementar `RedisBatchLockRepository` com RedLock
- [ ] Criar estratégia configurável (Redis ou SQL)
- [ ] Migrar Worker para usar Redis

### Fase 3: Eventos RabbitMQ
- [ ] Definir contratos de mensagens (`BatchCreated`, `BatchReady`)
- [ ] Publicar `BatchCreated` no POST /imports
- [ ] Publicar `BatchReady` após avaliação de policies
- [ ] Worker consome `BatchReady` e processa
- [ ] Manter polling como fallback

### Fase 4: Cache no Redis
- [ ] Cache de `Client` e `ClientPolicy`
- [ ] Invalidar cache quando necessário
- [ ] Monitorar hit rate

## Configurações Necessárias

### appsettings.json
```json
{
  "BatchLock": {
    "Provider": "Redis", // ou "SqlServer"
    "RedisLockTimeout": "00:30:00"
  },
  "RabbitMq": {
    "PublishEvents": true,
    "BatchReadyQueue": "dataflow.batch.ready"
  },
  "Cache": {
    "ClientCacheTtl": "00:05:00",
    "PolicyCacheTtl": "00:10:00"
  }
}
```

## Dependências

- ✅ Redis já configurado
- ✅ RabbitMQ já configurado
- ⚠️ Precisa: `RedLock.net` para RedLock distribuído
- ⚠️ Precisa: Configurar filas no RabbitMQ

## Próximos Passos

1. **Imediato**: Corrigir problemas atuais (Worker DI, API script)
2. **Fase 1**: Migrar rate limiting para Redis
3. **Fase 2**: Implementar RedLock para mutex
4. **Fase 3**: Adicionar eventos RabbitMQ
5. **Fase 4**: Cache de clientes/policies

