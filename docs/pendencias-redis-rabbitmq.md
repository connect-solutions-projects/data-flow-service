# Pendências - Integração Redis e RabbitMQ

## ✅ Implementado

1. ✅ Rate limiting com Redis
2. ✅ Mutex com Redis RedLock
3. ✅ Eventos RabbitMQ (BatchCreated, BatchReady)
4. ✅ Cache de Client e ClientPolicy no Redis
5. ✅ Worker consome eventos BatchReady
6. ✅ Polling mantido como fallback

## ⚠️ Pendências

### 1. Configuração BatchLock nos appsettings.json

**Status**: ⚠️ Faltando

**Ação necessária**: Adicionar configuração em `appsettings.json` e `appsettings.Development.json`:

```json
{
  "BatchLock": {
    "Provider": "Redis",  // ou "SqlServer"
    "RedisLockTimeout": "00:30:00"
  }
}
```

**Arquivos afetados**:
- `src/apps/DataFlow.Api/appsettings.json`
- `src/apps/DataFlow.Api/appsettings.Development.json`
- `src/apps/DataFlow.Worker/appsettings.json`
- `src/apps/DataFlow.Worker/appsettings.Development.json`

### 2. Configuração Redis Connection String

**Status**: ⚠️ Verificar

**Ação necessária**: Garantir que Redis está configurado corretamente:

```json
{
  "ConnectionStrings": {
    "Redis": "redis:6379"  // ou "localhost:6379" para desenvolvimento
  },
  "Redis": {
    "Host": "redis",
    "Port": "6379"
  }
}
```

### 3. Configuração RabbitMQ

**Status**: ✅ Já configurado (verificar se está correto)

**Verificar**: RabbitMQ está configurado em:
- `src/apps/DataFlow.Api/Program.cs`
- `src/apps/DataFlow.Worker/Program.cs`

### 4. Documentação de Uso

**Status**: ⚠️ Pendente

**Ação necessária**: Criar documentação explicando:
- Como configurar Redis vs SQL Server para locks
- Como os eventos RabbitMQ funcionam
- Como monitorar rate limiting
- Troubleshooting comum

### 5. Testes

**Status**: ⚠️ Pendente (conforme solicitado pelo usuário)

**Nota**: Testes foram deixados para depois conforme instrução do usuário.

## 🔧 Correções Necessárias

### Adicionar configuração BatchLock

```bash
# Adicionar em appsettings.json de API e Worker
```

### Verificar Redis está acessível

```bash
# Testar conexão Redis
redis-cli -h redis -p 6379 ping
```

### Verificar RabbitMQ está acessível

```bash
# Testar conexão RabbitMQ
# Acessar http://localhost:15672 (Management UI)
```

## 📝 Próximos Passos

1. ✅ Adicionar configuração BatchLock nos appsettings
2. ⚠️ Testar integração end-to-end
3. ⚠️ Documentar uso e troubleshooting
4. ⚠️ Monitorar métricas de rate limiting
5. ⚠️ Validar locks distribuídos funcionando

