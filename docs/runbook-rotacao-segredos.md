# Runbook – Rotação de Segredos (ClientSecret / Webhook Secret)

## 1. Geração de novo segredo

1. Executar o script `scripts/generate-client-hash.ps1`:
   ```powershell
   .\scripts\generate-client-hash.ps1 -Secret "novo_segredo_super_forte"
   ```
2. O script retorna:
   - `SecretHash (hex)`
   - `SecretSalt (hex)`
3. Guarde o texto em local seguro. O valor em texto puro será mostrado apenas agora.

## 2. Atualização no SQL Server

```sql
USE DataFlowDev;
GO

DECLARE @ClientIdentifier NVARCHAR(64) = 'client-omni-flow';
DECLARE @NewHash VARBINARY(MAX) = 0x...; -- hash gerado no passo anterior
DECLARE @NewSalt VARBINARY(MAX) = 0x...; -- salt gerado no passo anterior

UPDATE Clients
SET SecretHash = @NewHash,
    SecretSalt = @NewSalt,
    LastSeenAt = NULL
WHERE ClientIdentifier = @ClientIdentifier;
```

- Reinicie o cliente (OmniFlow) com o novo segredo.
- O DataFlow passa a aceitar imediatamente o novo valor.

## 3. Rotação de Webhook Secret

1. Gere um GUID ou string aleatória.
2. Atualize a tabela `WebhookSubscriptions`:
   ```sql
   UPDATE WebhookSubscriptions
   SET Secret = 'novo-secret-webhook'
   WHERE ClientId = (SELECT Id FROM Clients WHERE ClientIdentifier = 'client-omni-flow');
   ```
3. A aplicação cliente deve atualizar a validação HMAC para usar o novo secret.

## 4. Auditoria

- Registrar o ticket/motivo da rotação.
- Logar no runbook de segurança:
  - Data/hora
  - Cliente afetado
  - Responsável

## 5. Checklist pós-rotação

| Item | Status |
|------|--------|
| Segredo aplicado no banco | ☐ |
| Cliente externo atualizado | ☐ |
| Testes de autenticação executados | ☐ |
| Logs verificados (falhas de auth) | ☐ |

> Dica: use o endpoint `GET /imports/{batchId}` para confirmar que o cliente consegue autenticar e consultar normalmente após a rotação.

