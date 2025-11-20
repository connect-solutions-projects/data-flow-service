## DataFlow – Fase 1 (Fundamentos)

### Objetivos
- Migrar o núcleo do DataFlow para SQL Server compartilhado com o universo Store.
- Estabelecer autenticação baseada em `clientId`/`clientSecret` para múltiplas aplicações.
- Publicar os contratos mínimos da API (`POST /imports`, `GET /imports/{batchId}`) para permitir o piloto controlado.

---

### Esquema SQL Server Proposto

**Clients**
- `Id (uniqueidentifier, PK)`
- `Name (nvarchar(120))`
- `ClientIdentifier (varchar(64), unique)` → usado como `clientId`
- `SecretHash (varbinary)` + `SecretSalt (varbinary)` – armazenar segredo com PBKDF2/Argon2
- `Status (tinyint)` → Ativo / Suspenso
- `CreatedAt`, `LastSeenAt`

**ClientPolicies**
- `Id (uniqueidentifier, PK)`
- `ClientId (FK Clients)`
- `MaxFileSizeMb (int)`
- `MaxBatchPerDay (int)`
- `AllowedStartHour (tinyint)` / `AllowedEndHour (tinyint)` – janela em UTC
- `RequireSchedulingForLarge (bit)`
- `LargeThresholdMb (int)`
- `CreatedAt`

**WebhookSubscriptions**
- `Id (uniqueidentifier, PK)`
- `ClientId (FK Clients)`
- `Url (nvarchar(512))`
- `Secret (nvarchar(128))` – usado para assinar payload
- `IsActive (bit)`
- `CreatedAt`

**ImportBatch**
- `Id (uniqueidentifier, PK)` → `batchId`
- `ClientId (FK Clients)`
- `Status (tinyint)` → Pending, Processing, Scheduled, Completed, CompletedWithErrors, Failed
- `FileName (nvarchar(260))`
- `FileType (tinyint)` → JSON/Excel
- `FileSizeBytes (bigint)`
- `Checksum (char(64))`
- `UploadPath (nvarchar(400))` – `/var/dataflow/imports/{batchId}` ou storage temporário externo
- `PolicyDecision (nvarchar(100))` – registro se entrou em modo agendado/negado
- `CreatedAt`, `StartedAt`, `CompletedAt`
- `TotalRecords`, `ProcessedRecords`
- `ErrorSummary (nvarchar(max))`

**ImportItem**
- `Id (bigint, identity, PK)`
- `BatchId (FK ImportBatch)`
- `Sequence (int)` – posição no arquivo/lote
- `PayloadJson (nvarchar(max))` – snapshot enviado ao OmniFlow
- `Status (tinyint)` → Imported, Error, Skipped
- `ErrorMessage (nvarchar(1000))`
- `CreatedAt`

**BatchLocks**
- `Id (int, PK = 1)` – tabela singleton
- `IsLocked (bit)`
- `LockOwnerBatchId (uniqueidentifier, nullable)`
- `LockedAt`
> Usaremos transação `SELECT ... WITH (UPDLOCK, HOLDLOCK)` para garantir mutex global. Evoluções futuras podem migrar para Redis/filas se necessário.

---

### Autenticação `clientId/clientSecret`
1. **Cadastro**: Admin registra cliente via console/seed → gera `clientId` (GUID) e `clientSecret` (string longa). Secret é apresentado apenas uma vez e gravado como hash.
2. **Fluxo de requisição**: cada chamada HTTP inclui cabeçalhos:
   - `X-Client-Id`
   - `X-Client-Secret`
3. **Middleware**:
   - Busca o cliente por `ClientIdentifier`.
   - Valida o hash do segredo. Opcional: cache em memória por 5 minutos.
   - Verifica `Status` e aplica policies básicas (ex.: rate limit).
4. **Rotação**:
   - Criar endpoint interno (admin) para gerar novo secret e invalidar o anterior.
5. **Observabilidade**:
   - Registrar `clientId`, `batchId` e IP em logs estruturados.
   - Métrica `dataflow_requests_total` por cliente para suportar rate limiting futuro.

---

### Endpoints Iniciais

#### POST `/imports`
**Autenticação**: obrigatória.  
**Request (multipart ou JSON base64)**:
```json
{
  "originDefault": "LandingPageX",
  "requestedBy": "user@client.com",
  "metadata": {
    "correlationId": "opcional",
    "notes": "texto livre"
  },
  "file": "<arquivo JSON/Excel>",
  "fileName": "leads.xlsx",
  "contentType": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
}
```
**Validações**:
- Tipo permitido (JSON/Excel).
- Limites de tamanho conforme `ClientPolicies`.
- Se violar policy → status `Scheduled` ou resposta `409` com instrução (depende da regra).

**Response** (`202 Accepted`):
```json
{
  "batchId": "GUID",
  "status": "Pending",
  "policyDecision": "Immediate|Scheduled",
  "message": "Importação enfileirada"
}
```

#### GET `/imports/{batchId}`
**Autenticação**: mesma do upload, validando se o batch pertence ao cliente.  
**Response**:
```json
{
  "batchId": "GUID",
  "status": "Processing",
  "fileName": "leads.xlsx",
  "createdAt": "2025-11-20T12:00:00Z",
  "startedAt": "2025-11-20T12:01:00Z",
  "completedAt": null,
  "totalRecords": 1000,
  "processedRecords": 400,
  "lastError": "Detalhe opcional",
  "webhookNotified": false
}
```
> Esse endpoint é usado pela interface web (polling ou SignalR) e por qualquer aplicação cliente que precise observar o progresso.

---

### Próximos Passos Dentro da Fase 1
1. Criar migrations EF Core para o esquema acima no SQL Server.
2. Implementar seeding inicial de clientes (ex.: OmniFlow) e geração de secrets.
3. Adicionar middleware de autenticação + filtros reutilizáveis para endpoints.
4. Validar upload básico salvando arquivos em `/var/dataflow/imports/{batchId}` e registrando o batch como `Pending`.
5. Entregar documentação dos contratos (OpenAPI) alinhada com este plano.

Com isso, encerramos a Fase 1 e podemos seguir para o pipeline de processamento (Fase 2).

---

## DataFlow – Fase 2 (Pipeline de Processamento)

### Objetivos
- Tornar o DataFlow capaz de processar batches pendentes de forma segura (mutex global) e resiliente.
- Implementar a divisão do arquivo em lotes, handshake com o OmniFlow e registro detalhado por item.
- Garantir armazenamento temporário efêmero e limpeza automática após cada batch.

### Worker + Mutex
- Serviço `DataFlow.Worker` roda continuamente, buscando batches `Pending`/`Scheduled` ordenados por `CreatedAt`.
- Para iniciar processamento:
  1. Abra transação.
  2. Consulte `BatchLocks` com `UPDLOCK HOLDLOCK`. Se `IsLocked = 1`, retorne e aguarde backoff.
  3. Atualize `BatchLocks` para `IsLocked = 1`, `LockOwnerBatchId = batch.Id`, `LockedAt = GETUTCDATE()`.
  4. Atualize o batch para `Processing`, registre `StartedAt` e `ProcessingHost`.
  5. Commit.
- Em caso de crash, um watchdog libera o lock se `LockedAt` estiver acima de `LockTimeoutMinutes`.

### Pipeline de Processamento
1. **Preparação**
   - Baixar o arquivo do storage temporário (disco local ou blob).
   - Validar tipo declarado (`FileType`), checar checksum.
2. **Parsing**
   - JSON: usar streaming (`System.Text.Json`) para evitar carregar tudo na memória.
   - Excel: `ClosedXML`, convertendo cada linha em DTO padronizado.
3. **Normalização**
   - Sanitizar campos (`PhoneNumber`, `Origin`, `PropertyContextJson`).
   - Montar payload final (`LeadContextJson`) conforme contrato comum.
4. **Lotes**
   - Config `BatchSize` por cliente (default 100 registros).
   - Cada lote gera payload:
     ```json
     {
       "batchId": "...",
       "chunkId": 5,
       "offset": 400,
       "items": [ ... até BatchSize ... ]
     }
     ```
   - Envio via HTTP para `/api/leads/import` (OmniFlow) com headers de autenticação técnica.
5. **Handshake**
   - Após envio, DataFlow aguarda resposta:
     - `202 Accepted` + `ackId`: OmniFlow processará e enviará `POST /imports/{batchId}/acks`.
     - Ou fluxo síncrono (`200 OK`) confirmando processamento imediato.
   - Quando o OmniFlow responder `ACK`, DataFlow registra `ImportItem` como `Imported`.
   - Em caso de `4xx/5xx`, lote é marcado `Error`, mantemos payload em `ImportItem` e seguimos para o próximo lote.
6. **Completação**
   - Terminando todos os lotes:
     - Se houve erros parciais → `CompletedWithErrors`.
     - Sem erros → `Completed`.
     - Falhas críticas (ex.: não conseguiu ler arquivo) → `Failed`.
   - Libera mutex (`BatchLocks`), remove arquivo local.
   - Dispara webhook (se configurado) e registra evento no log/metrics.

### Tratamento de Erros e Retentativas
- **Envio de lote**: até 3 tentativas com backoff exponencial (ex.: 2s, 5s, 15s).
- **Erro do OmniFlow**:
  - Se `4xx` fixo (ex.: validação), registramos `Error` e seguimos.
  - Se `429` ou `5xx`, reintentamos até `MaxRetries`; após isso o lote entra como `Error` e a importação termina `CompletedWithErrors`.
- **Timeout de ACK**:
  - Configurável (`AckTimeoutSeconds`). Se expirar, reenvia o mesmo lote (deduplicação via `chunkId` + hash do conteúdo) para evitar duplicidade.

### Armazenamento Temporário e Limpeza
- Diretório padrão: `/var/dataflow/imports/{batchId}`.
- Estrutura:
  - `original/{fileName}` → upload inicial.
  - `chunks/` → JSON intermediário enquanto processa (opcional).
- Após `Completed|Failed`, o worker remove o diretório.
- Quando a política exigir agendamento:
  - Upload vai para storage externo (por exemplo `s3://dataflow-temp/{batchId}/{file}`).
  - O worker, ao detectar `Status = Scheduled` e dentro da janela, baixa o arquivo para o diretório local, muda status para `Pending` e segue fluxo normal.

### Observabilidade e Métricas
- Logs estruturados por etapa (`UploadValidated`, `ChunkSent`, `AckReceived`, `ChunkError`).
- Métricas Prometheus:
  - `dataflow_batches_processing` (gauge).
  - `dataflow_chunk_duration_seconds` (histograma).
  - `dataflow_chunk_errors_total` (counter com labels `clientId`, `reason`).
- Eventos de domínio (ex.: `BatchProcessingStarted`, `BatchChunkDispatched`) podem alimentar dashboards em tempo real.

### Próximos Passos da Fase 2
1. Implementar serviço de mutex + watchdog.
2. Criar parsers streaming (JSON/Excel) com produtor de lotes.
3. Desenvolver cliente HTTP para OmniFlow com retentativas e deduplicação de `chunkId`.
4. Persistir resultados em `ImportItem` e atualizar progresso em `ImportBatch`.
5. Implementar limpeza automática e disparo de webhook ao concluir.

Após finalizar esses itens, avançamos para a Fase 3 (webhooks e observabilidade avançada).

---

## DataFlow – Fase 3 (Webhooks, Observabilidade e Governança)

### Objetivos
- Prover feedback em tempo real para cada aplicação cliente via webhooks assinados.
- Consolidar observabilidade multi-tenant (logs, métricas, tracing) para diagnosticar gargalos.
- Estabelecer governança operacional: rate limits, políticas sazonais e janelas de manutenção.

### Webhooks
- **Cadastro**: `POST /clients/{clientId}/webhooks`
  - Payload: `{ "url": "...", "secret": "opcional", "events": ["BatchCompleted", "BatchFailed"] }`
  - Valida se o clientId pertence ao chamador administrativo.
- **Entrega**
  - Evento `BatchFinalized` envia:
    ```json
    {
      "event": "BatchCompleted",
      "clientId": "store-omniflow",
      "batchId": "GUID",
      "status": "CompletedWithErrors",
      "metrics": {
        "totalRecords": 1000,
        "processedRecords": 1000,
        "errorCount": 23,
        "startedAt": "2025-11-20T12:01:00Z",
        "completedAt": "2025-11-20T12:05:30Z"
      },
      "signature": "HMAC_SHA256(payload, secret)"
    }
    ```
  - Retries: 5 tentativas com backoff (15s, 30s, 60s, 5min, 15min). Falhas permanentes registram alerta e aparecem no dashboard.
  - Dead-letter: após falha final, grava em `WebhookDeliveryFailures` para reprocesso manual.
- **Segurança**
  - Assinatura HMAC (`X-DataFlow-Signature`), timestamp (`X-DataFlow-Timestamp`).
  - Cliente valida antes de aceitar.

### Observabilidade Multi-tenant
- **Logs**
  - Contexto obrigatório: `clientId`, `batchId`, `chunkId`.
  - Eventos principais (`UploadReceived`, `PolicyDeferred`, `ChunkAckTimeout`, `WebhookRetry`).
  - Export para Elastic ou Loki via Serilog sink.
- **Métricas**
  - `dataflow_batch_duration_seconds` (histograma com labels `clientId`, `status`).
  - `dataflow_chunk_inflight` (gauge).
  - `dataflow_webhook_failures_total` (counter).
  - Dashboards por cliente mostrando throughput, sucesso/erro e tempo de ACK.
- **Tracing**
  - Propagar `TraceId` ao OmniFlow via header `x-trace-id` para correlacionar lote enviado x persistência no Omni.

### Governança e Policies Avançadas
- **Rate Limiting**
  - Definir `RequestsPerMinute` e `ConcurrentUploads` por cliente.
  - Middleware responde `429` quando excedido, com cabeçalho `Retry-After`.
- **Janelas e Agendamentos**
  - Policies permitem definir:
    - `BusinessHoursOnly`: bloqueia grandes imports em horário comercial.
    - `MaintenanceWindow`: impede uploads durante manutenção programada.
  - O endpoint `POST /imports` retorna `202 Scheduled` com `scheduledFor` quando o processamento for adiado.
- **Quota de Armazenamento Temporário**
  - Limite de espaço usado simultaneamente por cliente; evita ocupar todo o disco/ bucket temporário.
- **Alertas**
  - Integração com PagerDuty/Teams quando:
    - Batch travado > X minutos.
    - Falha de webhook recorrente.
    - Taxa de erros > threshold.

### Próximos Passos da Fase 3
1. Implementar CRUD de webhooks e engine de entrega com retries + HMAC.
2. Instrumentar logs/métricas/traces em todos os serviços (API, Worker, Reporting).
3. Acrescentar middleware de rate limit e enforcement das novas policies.
4. Criar dashboards de monitoramento (Grafana/Kibana) e alertas correspondentes.
5. Documentar contratos de webhook e expectativas de validação para aplicativos clientes.

Concluindo a Fase 3, teremos o DataFlow pronto para operar como serviço shared multi-tenant com feedback em tempo real e governança robusta.

---

## DataFlow – Fase 4 (Escalabilidade, Resiliência e Segurança de Dados)

### Objetivos
- Habilitar execução multi-instância do DataFlow (API e Worker) mantendo consistência do processamento exclusivo.
- Garantir segurança/compliance para arquivos sensíveis (criptografia em trânsito e repouso, mascaramento).
- Preparar o serviço para crescimento: filas externas, auto-scaling e DR (disaster recovery).

### Resiliência e Failover
- **Topologia**
  - `DataFlow.Api` atrás de load balancer (NGINX/Ingress) com múltiplas réplicas.
  - `DataFlow.Worker` pode ter N instâncias; apenas uma processa lote ativo graças ao mutex SQL ou, futuramente, Redis distribuído.
- **Mutex distribuído evoluído**
  - Criar camada abstrata `IBatchLockProvider` com implementações:
    - `SqlBatchLock` (atual).
    - `RedisBatchLock` (usando RedLock) para suportar múltiplos nós em diferentes regiões.
  - Adicionar health-check que libera locks órfãos em caso de failover.
- **Fila Externa (opcional Fase 4)**
  - Introduzir broker (RabbitMQ/Kafka/Azure Service Bus) para publicar eventos `BatchReady`.
  - Workers consomem da fila, reduzindo dependência do polling no banco.
- **Disaster Recovery**
  - Backups automáticos do SQL Server com PITR.
  - Storage temporário em bucket redundante (multi-AZ).
  - Scripts de restauração e runbooks documentados.

### Segurança e Compliance
- **Criptografia**
  - HTTPS obrigatório (TLS 1.2+) entre clientes ↔ DataFlow e DataFlow ↔ OmniFlow.
  - Arquivos no disco temporário protegidos com LUKS/dm-crypt ou uso de storage criptografado (Azure Disk Encryption).
  - Secrets (clientSecret, webhook secret) armazenados no KeyVault/Azure Key Vault com rotação automática.
- **Mascaramento/PII**
  - Configuração `SensitiveData.*` controla redaction automática do `ImportItem.PayloadJson` após envio do chunk (hash + flag masked).
  - Logs não recebem payloads completos; apenas hashes e IDs.
- **Mascaramento e PII**
  - Logs nunca escrevem campos sensíveis (CPF, telefone completo). Utilizar `DataMaskingMiddleware`.
  - `ImportItem.PayloadJson` pode armazenar apenas hash do payload ou versão truncada; full payload fica em storage privado com TTL curto.
- **Políticas de Retenção**
  - Definir TTL para dados de batch (ex.: 30 dias) e rotina de purge (`DataFlow.RetentionJob`).
  - Conformidade LGPD/GDPR: fornecer endpoint admin para exclusão antecipada de batches a pedido do cliente.
  - *Status*: `DataRetentionHostedService` (Worker) já executa a limpeza automática com parâmetros `DataRetention`.
- **Auditoria**
  - Tabela `AuditLog` registrando operações sensíveis (criação de client, rotação de secrets, downloads manuais).
- **Rotinas administrativas**
  - Endpoint `POST /admin/purge` (header `X-Admin-Key`) executa `BatchPurgeService` para exclusão ad hoc.
  - Runbooks dedicados: retenção/DR e rotação de segredos.

### Escala Horizontal e Roadmap
- **Dimensionamento automático**
  - Métricas de fila/tamanho de backlog alimentam HPA/VMSS para subir/baixar réplicas.
  - Workers suportam `MaxParallelChunks` configurável (ex.: processar e aguardar ACK de até N lotes simultâneos para reduzir latência).
- **Suporte a múltiplos batches futuros**
  - Introduzir modos de operação:
    - `ExclusiveMode` (piloto atual).
    - `ConcurrentMode` com quotas por cliente (ex.: até 1 batch em processamento por cliente). Requer alteração no lock para `ClientScopedLock`.
- **Integração com outras aplicações Store**
  - Expor SDK/Client Library para facilitar uso (C# e Node).
  - Oficializar contrato de versionamento (`v1`, `v2`) permitindo evoluir payloads sem quebrar clientes antigos.

### Próximos Passos da Fase 4
1. Implementar `IBatchLockProvider` e preparar infraestrutura Redis/SQL HA.
2. Configurar storage criptografado e revisar manuseio de PII nos logs/payloads.
3. Criar job de retenção e endpoints administrativos (purge, audit log).
4. Planejar PoC com fila externa para eventos `BatchReady`.
5. Escrever runbooks de failover, checklist de DR e documentação de segurança.

Com a Fase 4 concluída, o DataFlow estará apto a operar em produção com alta disponibilidade, segurança reforçada e capacidade de suportar múltiplas aplicações Store em escala.

