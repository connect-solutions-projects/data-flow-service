# Migration: AddRateLimitPerMinuteToClientPolicy

## Resumo

Esta migration adiciona o campo `RateLimitPerMinute` à tabela `ClientPolicies`, permitindo que cada cliente tenha um limite de rate limiting personalizado.

## Aplicar Migration

```bash
dotnet ef database update -c IngestionDbContext -p src\libs\DataFlow.Infrastructure\DataFlow.Infrastructure.csproj -s src\apps\DataFlow.Api\DataFlow.Api.csproj
```

## Como Usar

### 1. Configurar Rate Limit para um Cliente

Atualize a `ClientPolicy` do cliente:

```sql
UPDATE ClientPolicies
SET RateLimitPerMinute = 100  -- Exemplo: 100 requisições por minuto
WHERE ClientId = (SELECT Id FROM Clients WHERE ClientIdentifier = 'client-omni-flow');
```

### 2. Comportamento

- Se `RateLimitPerMinute` estiver configurado na policy: usa esse valor
- Se não estiver configurado (NULL): usa o padrão de 30 req/min
- O middleware `ClientRateLimitMiddleware` busca automaticamente da policy

### 3. Exemplo de Configuração

```csharp
// Ao criar/atualizar uma ClientPolicy
var policy = new ClientPolicy(clientId);
policy.UpdateRateLimit(100); // 100 requisições por minuto
```

## Impacto

- ✅ Rate limiting agora é dinâmico por cliente
- ✅ Cada cliente pode ter seu próprio limite
- ✅ Fallback seguro para 30 req/min se não configurado
- ✅ Métricas incluem identificação do cliente

