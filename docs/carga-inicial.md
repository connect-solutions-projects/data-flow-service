# Carga Inicial do DataFlow

Passos para configurar o ambiente mínimo (cliente + policy).

---

## 1. Inserir o cliente OmniFlow

Arquivo já pronto: `scripts/insert-client-omniflow.sql`

1. Conecte no SQL Server (`DataFlowDev`)
2. Execute o script acima para recriar o cliente `client-omni-flow`
3. O script já grava o hash/salt (PBKDF2) e deixa o cliente com status `Active`

---

## 2. Criar/atualizar a ClientPolicy com rate limit

Use o script abaixo para garantir que o cliente tenha uma policy com `RateLimitPerMinute` configurado:

```sql
USE [DataFlowDev];
GO

DECLARE @ClientId UNIQUEIDENTIFIER =
    (SELECT Id FROM Clients WHERE ClientIdentifier = 'client-omni-flow');

IF NOT EXISTS (SELECT 1 FROM ClientPolicies WHERE ClientId = @ClientId)
BEGIN
    INSERT INTO ClientPolicies (
        Id,
        ClientId,
        RateLimitPerMinute,
        CreatedAt
    )
    VALUES (
        NEWID(),
        @ClientId,
        120,            -- limite desejado (ex.: 120 req/min)
        GETUTCDATE()
    );
END
ELSE
BEGIN
    UPDATE ClientPolicies
    SET RateLimitPerMinute = 120
    WHERE ClientId = @ClientId;
END

SELECT
    c.ClientIdentifier,
    p.RateLimitPerMinute
FROM ClientPolicies p
JOIN Clients c ON p.ClientId = c.Id
WHERE c.ClientIdentifier = 'client-omni-flow';
```

> Ajuste o valor `120` para o limite que deseja aplicar a cada cliente.

---

## 3. Ajustar políticas de PII / Retenção por cliente

É possível definir redaction e retenção customizados diretamente na `ClientPolicies`:

```sql
UPDATE ClientPolicies
SET RedactPayloadOnSuccess = 1,    -- aplica redaction após sucesso
    RedactPayloadOnFailure = 0,    -- redaction também em falhas? (0/1)
    RetentionDays = 45             -- override de retenção (dias)
WHERE ClientId = (SELECT Id FROM Clients WHERE ClientIdentifier = 'client-omni-flow');
```

- `NULL` significa “usar o padrão do appsettings”.
- `RetentionDays` controla quantos dias o `DataRetentionHostedService` manterá batches desse cliente (sobrepõe `DataRetention.BatchRetentionDays`).

---

## 4. Próximos passos

- Repetir os mesmos scripts para outros clientes (basta trocar o `ClientIdentifier`)
- Registrar políticas adicionais (MaxFileSize, horários permitidos, etc.) quando necessário
- Após inserir/alterar, o middleware já respeita o novo limite sem reiniciar a API
