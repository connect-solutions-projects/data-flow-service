# Runbook - Fase 4 (Resiliência, Retenção e DR)

Este runbook consolida as rotinas operacionais introduzidas na Fase 4.

---

## 1. Retenção Automática de Batches

- Serviço: `DataRetentionHostedService` (DataFlow.Worker)
- Configuração (`appsettings`):
  ```json
  "DataRetention": {
    "EnableCleanup": true,
    "BatchRetentionDays": 30,
    "CheckIntervalHours": 6,
    "MaxBatchesPerRun": 100
  }
  ```
- Critério: remove batches com `CompletedAt` inferior ao cutoff (`UtcNow - BatchRetentionDays`), incluindo `ImportItems` e diretórios em `/var/dataflow/imports/{batchId}`.
- Logs: procurar por `DataRetentionHostedService` no worker (`Removed {BatchCount} batches ...`).

### 1.1 Execução Manual
1. Ajustar `DataRetention.MaxBatchesPerRun` conforme necessário.
2. Reiniciar o worker (se quiser forçar nova janela).
3. Conferir logs / métricas (`dataflow_retention_runs_total` – TBD).

### 1.2 Desabilitar Temporariamente
```json
"DataRetention": {
  "EnableCleanup": false,
  ...
}
```
Reimplante ou reinicie o worker. Sem impacto nos demais serviços.

---

## 2. Failover / Disaster Recovery (DR)

### 2.1 Banco SQL Server
- Backups: seguir política da VPS/serviço (snapshot diário + PITR quando em nuvem).
- Passos:
  1. Restaurar backup mais recente em servidor standby.
  2. Ajustar `ConnectionStrings:DataFlow` em `appsettings` ou variáveis (`dotnet user-secrets`, secrets Docker).
  3. Executar `dotnet ef database update ...` para garantir migrations.

### 2.2 Redis / RedLock
- Se o nó Redis falhar, o `IBatchLockRepository` automaticamente recai para a implementação configurada (Redis ou SQL). Em ambiente multi-nó, usar Redis replicado/cluster.
- Para liberar locks órfãos: o worker já chama `ForceReleaseExpiredLocksAsync`. Se necessário manualmente, executar `DEL dataflow:batch:lock` no Redis.

### 2.3 Recuperação de Arquivos Temporários
- Os uploads ficam em `/var/dataflow/imports/{batchId}`. Após `Completed` eles são excluídos, mas em caso de recuperação:
  1. Copiar diretório do snapshot.
  2. Ajustar `UploadPath` em `ImportBatches` para apontar para o novo local.
  3. Reprocessar setando `Status = Pending` e disparando evento `BatchReady`.

---

## 3. Checklist Operacional

| Item | Frequência | Responsável |
|------|------------|-------------|
| Verificar logs de retenção | Diário | SRE |
| Revisar tamanho de `/var/dataflow/imports` | Diário | SRE |
| Confirmar backups SQL executados | Diário | DBA |
| Testar restauração (tabletop) | Mensal | SRE/DBA |

---

## 4. Scripts Úteis

### 4.1 Listar Batches elegíveis para retenção
```sql
SELECT TOP (50) Id, ClientId, Status, CompletedAt
FROM ImportBatches
WHERE CompletedAt < DATEADD(day, -30, GETUTCDATE())
ORDER BY CompletedAt ASC;
```

### 4.2 Forçar limpeza manual via SQL
```sql
DELETE FROM ImportItems
WHERE BatchId IN (
    SELECT TOP (100) Id
    FROM ImportBatches
    WHERE CompletedAt < DATEADD(day, -30, GETUTCDATE())
);

DELETE FROM ImportBatches
WHERE CompletedAt < DATEADD(day, -30, GETUTCDATE());
```

> Sempre executar após garantir que os arquivos foram removidos do filesystem.

---

## 5. Próximos Passos

- Monitorar métricas de retenção e adicionar alertas no Grafana.
- Documentar política de criptografia de disco (LUKS/Azure Disk Encryption).
- Rodar o runbook de rotação de segredos (`docs/runbook-rotacao-segredos.md`) sempre que fizer troca de clientSecret/webhook secret.
- Utilizar o endpoint `POST /admin/purge` (header `X-Admin-Key`) para purges sob demanda; em caso de indisponibilidade da API use o serviço `BatchPurgeService`.
- Para políticas específicas de cada cliente, ajustar `RedactPayloadOnSuccess`, `RedactPayloadOnFailure` e `RetentionDays` em `ClientPolicies` (sobrepõem `SensitiveData.*` e `DataRetention.*` globais).

