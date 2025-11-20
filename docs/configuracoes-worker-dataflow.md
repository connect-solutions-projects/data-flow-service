# Configurações do Worker (DataFlow.Worker)

Este documento descreve **apenas** as configurações do Worker que precisam ser validadas.

## Arquivos de Configuração

### `src/apps/DataFlow.Worker/appsettings.json` (Base)

```json
{
  "ConnectionStrings": {
    "DataFlow": ""
  },
  "ImportBatch": {
    "ChunkSize": 100,
    "PollIntervalSeconds": 30,
    "LockTimeoutMinutes": 30
  },
  "OmniFlow": {
    "BaseUrl": "http://localhost:5000"
  },
  "ImportStorage": {
    "BasePath": "/var/dataflow/imports"
  }
}
```

### `src/apps/DataFlow.Worker/appsettings.Development.json` (Desenvolvimento)

```json
{
  "ConnectionStrings": {
    "DataFlow": "Data Source=191.252.214.134,1433;Initial Catalog=DataFlowDev;User ID=user_data_flow_db;Password=DataFlow@Dolar$;Encrypt=True;TrustServerCertificate=True"
  },
  "ImportBatch": {
    "ChunkSize": 100,
    "PollIntervalSeconds": 30,
    "LockTimeoutMinutes": 30
  },
  "OmniFlow": {
    "BaseUrl": "http://localhost:5000"
  },
  "ImportStorage": {
    "BasePath": "C:\\temp\\dataflow\\imports"
  }
}
```

## Parâmetros de Configuração

### 1. `ImportBatch:ChunkSize` (default: 100)
- **Descrição**: Quantidade de registros por lote enviado ao OmniFlow
- **Uso**: `ImportBatchProcessor` divide o arquivo em chunks deste tamanho
- **Recomendação**: 
  - 50-100 para arquivos pequenos/médios
  - 100-200 para arquivos grandes (se OmniFlow suportar)
  - Ajustar conforme capacidade do OmniFlow

### 2. `ImportBatch:PollIntervalSeconds` (default: 30)
- **Descrição**: Intervalo em segundos entre buscas de batches pendentes
- **Uso**: `ImportBatchWorkerService` executa polling a cada X segundos
- **Recomendação**:
  - 30s para desenvolvimento
  - 10-15s para produção (se houver muitos batches)
  - Não deixar muito baixo para evitar carga desnecessária no banco

### 3. `ImportBatch:LockTimeoutMinutes` (default: 30)
- **Descrição**: Tempo máximo que um lock pode ficar ativo antes de ser considerado expirado
- **Uso**: Watchdog libera locks expirados automaticamente
- **Recomendação**:
  - 30min para arquivos médios
  - 60min+ para arquivos muito grandes
  - Considerar tempo médio de processamento do maior arquivo esperado

### 4. `OmniFlow:BaseUrl` (obrigatório)
- **Descrição**: URL base do OmniFlow para envio de lotes
- **Uso**: `ImportBatchProcessor` envia chunks via HTTP POST
- **Formato**: `http://host:port` ou `https://host:port`
- **Exemplos**:
  - Dev: `http://localhost:5000`
  - Prod: `https://api.omniflow.com` ou `http://omniflow-api:5000` (Docker)
- **Validação**: URL deve ser acessível do Worker

### 5. `ImportStorage:BasePath` (obrigatório)
- **Descrição**: Caminho onde arquivos temporários são salvos
- **Uso**: Worker lê arquivos de `{BasePath}/{batchId}/` e remove após processamento
- **Exemplos**:
  - Linux: `/var/dataflow/imports`
  - Windows: `C:\temp\dataflow\imports`
  - Docker: Volume montado em `/var/dataflow/imports`
- **Validação**: 
  - Diretório deve existir
  - Worker deve ter permissão de leitura e escrita
  - Mesmo caminho (ou compatível) usado pela API

## Como o Worker Usa Essas Configurações

### `ImportBatchWorkerService`
- Lê `ImportBatch:PollIntervalSeconds` → define intervalo de polling
- Lê `ImportBatch:LockTimeoutMinutes` → define timeout do watchdog
- Busca batches `Pending`/`Scheduled` a cada polling
- Adquire lock antes de processar
- Watchdog libera locks expirados a cada 5 minutos

### `ImportBatchProcessor`
- Lê `ImportBatch:ChunkSize` → divide arquivo em chunks
- Lê `OmniFlow:BaseUrl` → envia HTTP POST para OmniFlow
- Lê `ImportStorage:BasePath` → localiza arquivo para leitura
- Processa arquivo, envia chunks, persiste `ImportItems`
- Worker remove arquivo após processamento

## Validações Necessárias

### 1. Connection String
```bash
# Verificar que ConnectionStrings:DataFlow está configurada
# Testar conectividade com SQL Server
```

### 2. OmniFlow:BaseUrl
```bash
# Testar se URL é acessível
curl http://localhost:5000/health
# ou
ping omniflow-api
```

### 3. ImportStorage:BasePath
```bash
# Windows
dir C:\temp\dataflow\imports
# ou criar se não existir
mkdir C:\temp\dataflow\imports

# Linux
ls -la /var/dataflow/imports
# ou criar com permissões
mkdir -p /var/dataflow/imports
chmod 755 /var/dataflow/imports
```

### 4. Logs de Startup
Ao iniciar o Worker, verificar logs:
```
ImportBatchWorkerService started
ImportBatch:PollIntervalSeconds = 30
ImportBatch:LockTimeoutMinutes = 30
OmniFlow:BaseUrl = http://localhost:5000
ImportStorage:BasePath = C:\temp\dataflow\imports
```

## Próximo Passo

Após validar essas configurações do Worker, o próximo passo é **aplicar migrations no SQL Server**.

