# Ajustes Finais no DataFlow

Este documento descreve os ajustes finais necessários para colocar o DataFlow em produção após a implementação da Fase 2.

## 1. Configurações do Worker (DataFlow.Worker)

### Arquivos de Configuração

**`src/apps/DataFlow.Worker/appsettings.json`** (base)
- `ConnectionStrings:DataFlow` - Connection string do SQL Server
- `ImportBatch:ChunkSize` - Tamanho do lote (default: 100)
- `ImportBatch:PollIntervalSeconds` - Intervalo de polling (default: 30s)
- `ImportBatch:LockTimeoutMinutes` - Timeout do lock (default: 30min)
- `OmniFlow:BaseUrl` - URL base do OmniFlow
- `ImportStorage:BasePath` - Caminho para armazenamento temporário

**`src/apps/DataFlow.Worker/appsettings.Development.json`** (desenvolvimento)
- Connection string apontando para `DataFlowDev`
- `ImportStorage:BasePath` ajustado para Windows (`C:\temp\dataflow\imports`)
- `OmniFlow:BaseUrl` apontando para localhost

### Validações Necessárias

1. **Connection String**
   - Verificar que `ConnectionStrings:DataFlow` está correta
   - Testar conectividade com o SQL Server
   - Confirmar que o usuário `user_data_flow_db` tem permissões adequadas

2. **OmniFlow:BaseUrl**
   - URL deve ser acessível do Worker
   - Formato: `http://host:port` ou `https://host:port`
   - Exemplo: `http://omniflow-api:5000` ou `https://api.omniflow.com`

3. **ImportStorage:BasePath**
   - Diretório deve existir e ser gravável
   - Em Linux: `/var/dataflow/imports`
   - Em Windows: `C:\temp\dataflow\imports` ou equivalente
   - Verificar permissões de escrita

## 2. Configurações da API (DataFlow.Api)

### Arquivos de Configuração

**`src/apps/DataFlow.Api/appsettings.json`** (base)
- `ConnectionStrings:DataFlow` - Connection string do SQL Server
- `ImportStorage:BasePath` - Caminho para uploads temporários
- `ClientSeed:Clients` - Lista de clientes para seed inicial

**`src/apps/DataFlow.Api/appsettings.Development.json`** (desenvolvimento)
- Connection string de desenvolvimento
- `ClientSeed` com cliente OmniFlow de exemplo
- `OmniFlow:BaseUrl` para referência

### Validações Necessárias

1. **ApplyMigrationsOnStartup**
   - Configurar `ApplyMigrationsOnStartup=true` no primeiro deploy
   - Ou executar `dotnet ef database update` manualmente
   - Verificar que todas as tabelas foram criadas

2. **ClientSeed**
   - Configurar pelo menos o cliente OmniFlow
   - `ClientIdentifier` deve ser único (ex: `client-omni-flow`)
   - `Secret` será hasheado automaticamente via PBKDF2
   - Após primeiro seed, o secret pode ser rotacionado via SQL

3. **ImportStorage:BasePath**
   - Mesmo diretório ou compatível com o Worker
   - Garantir que a API tem permissão de escrita
   - Considerar usar caminho absoluto

## 3. Seed do Cliente OmniFlow

### Opção 1: Via appsettings (Recomendado para Dev)

```json
{
  "ClientSeed": {
    "Clients": [
      {
        "Name": "OmniFlow",
        "ClientIdentifier": "client-omni-flow",
        "Secret": "$2a$12$vzIWMYbiLEI4RvZ/J1dfpOay6FTs4dqfe8RxGsQBmJ9ihI9Ct9nju"
      }
    ]
  }
}
```

O seed roda automaticamente no startup se `ClientSeeder` estiver registrado.

### Opção 2: Via SQL (Recomendado para Produção)

Execute o script `scripts/insert-client-omniflow.sql` após aplicar as migrations.

**Vantagens:**
- Secret não fica em texto no appsettings
- Mais seguro para produção
- Permite rotacionar secrets sem redeploy

## 4. Verificações de Startup

### DataFlow.Api

1. **Migrations aplicadas**
   ```bash
   # Verificar tabelas no SQL Server
   SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES 
   WHERE TABLE_SCHEMA = 'dbo' 
   AND TABLE_NAME IN ('Clients', 'ImportBatches', 'ImportItems', 'BatchLocks')
   ```

2. **Cliente seedado**
   ```sql
   SELECT Id, Name, ClientIdentifier, Status, CreatedAt 
   FROM Clients 
   WHERE ClientIdentifier = 'client-omni-flow'
   ```

3. **BatchLocks inicializado**
   ```sql
   SELECT * FROM BatchLocks WHERE Id = 1
   -- Deve retornar: Id=1, IsLocked=0, LockOwnerBatchId=NULL, LockedAt=NULL
   ```

### DataFlow.Worker

1. **Logs de startup**
   - Verificar "ImportBatchWorkerService started"
   - Verificar conectividade com SQL Server
   - Verificar se consegue resolver `OmniFlow:BaseUrl`

2. **Watchdog ativo**
   - Worker deve iniciar task de watchdog
   - Executa a cada 5 minutos para liberar locks expirados

3. **Polling funcionando**
   - Worker busca batches `Pending` a cada 30 segundos (configurável)
   - Logs devem mostrar "No pending batches" quando não há trabalho

## 5. Testes de Integração Básicos

### Teste 1: Upload de Batch

```bash
# POST /imports com arquivo JSON
curl -X POST http://localhost:5000/imports \
  -H "X-Client-Id: client-omni-flow" \
  -H "X-Client-Secret: $2a$12$vzIWMYbiLEI4RvZ/J1dfpOay6FTs4dqfe8RxGsQBmJ9ihI9Ct9nju" \
  -F "file=@leads.json" \
  -F "fileName=leads.json" \
  -F "contentType=application/json" \
  -F "originDefault=TestOrigin"
```

**Verificações:**
- Retorna `202 Accepted` com `batchId`
- Batch criado no banco com status `Pending`
- Arquivo salvo em `ImportStorage:BasePath/{batchId}/`

### Teste 2: Consulta de Status

```bash
# GET /imports/{batchId}
curl -X GET http://localhost:5000/imports/{batchId} \
  -H "X-Client-Id: client-omni-flow" \
  -H "X-Client-Secret: $2a$12$..."
```

**Verificações:**
- Retorna status do batch
- Progresso atualizado (`processedRecords`, `totalRecords`)

### Teste 3: Worker Processando

1. Fazer upload de um batch pequeno (ex: 10 leads)
2. Verificar logs do Worker
3. Verificar que batch muda para `Processing` → `Completed`
4. Verificar que arquivo temporário foi removido
5. Verificar que `ImportItems` foram criados no banco

## 6. Configurações de Produção

### Variáveis de Ambiente Recomendadas

```bash
# Connection String
ConnectionStrings__DataFlow="Data Source=server,1433;Initial Catalog=DataFlowProd;..."

# OmniFlow
OmniFlow__BaseUrl="https://api.omniflow.com"

# Import Storage
ImportStorage__BasePath="/var/dataflow/imports"

# Import Batch
ImportBatch__ChunkSize=100
ImportBatch__PollIntervalSeconds=30
ImportBatch__LockTimeoutMinutes=30

# Apply Migrations (apenas no primeiro deploy)
APPLY_MIGRATIONS_ON_STARTUP=true
```

### Docker Compose (Exemplo)

```yaml
services:
  dataflow-api:
    image: dataflow-api:latest
    environment:
      - ConnectionStrings__DataFlow=${SQL_CONNECTION_STRING}
      - OmniFlow__BaseUrl=${OMNIFLOW_URL}
      - ImportStorage__BasePath=/var/dataflow/imports
    volumes:
      - ./imports:/var/dataflow/imports
  
  dataflow-worker:
    image: dataflow-worker:latest
    environment:
      - ConnectionStrings__DataFlow=${SQL_CONNECTION_STRING}
      - OmniFlow__BaseUrl=${OMNIFLOW_URL}
      - ImportStorage__BasePath=/var/dataflow/imports
    volumes:
      - ./imports:/var/dataflow/imports
    depends_on:
      - dataflow-api
```

## 7. Checklist Final

- [ ] Migrations aplicadas no SQL Server
- [ ] Tabelas criadas (`Clients`, `ImportBatches`, `ImportItems`, `BatchLocks`)
- [ ] Cliente OmniFlow seedado ou inserido via SQL
- [ ] `OmniFlow:BaseUrl` configurado e acessível
- [ ] `ImportStorage:BasePath` existe e é gravável
- [ ] Worker inicia sem erros
- [ ] API inicia sem erros
- [ ] Upload de batch funciona (POST /imports)
- [ ] Consulta de status funciona (GET /imports/{batchId})
- [ ] Worker processa batches pendentes
- [ ] Arquivos temporários são removidos após processamento
- [ ] Logs estruturados funcionando
- [ ] OmniFlow recebe lotes corretamente

## 8. Próximos Passos

Após validar os ajustes finais:

1. **Fase 3**: Implementar webhooks e observabilidade avançada
2. **Políticas**: Implementar `ClientPolicies` para controle de horários e tamanhos
3. **Agendamento**: Implementar processamento agendado para arquivos grandes
4. **Monitoramento**: Configurar dashboards Prometheus/Grafana
5. **Retry Logic**: Melhorar lógica de retry e deduplicação

