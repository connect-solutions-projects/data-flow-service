## Configuração do DataFlow com SQL Server

### 1. Connection String
Atualize `src/apps/DataFlow.Api/appsettings.json` (ou o arquivo de ambiente correspondente) com a string de conexão oficial:

```json
"ConnectionStrings": {
  "DataFlow": "Data Source=191.252.214.134,1433;Initial Catalog=DataFlowDev;User ID=user_data_flow_db;Password=DataFlow@Dolar$;Encrypt=True;TrustServerCertificate=True"
}
```

> Em desenvolvimento local você pode manter outra string em `appsettings.Development.json`, mas o arquivo principal deve apontar para o banco da VPS.

### 2. Gerar Migration Inicial
Na raiz do repositório (`C:\Users\rodrigo\Documents\Projetos\ConnectSolutions\Github\data-flow-service`), execute:

```bash
dotnet ef migrations add InitialSqlServer -c IngestionDbContext -p src\libs\DataFlow.Infrastructure\DataFlow.Infrastructure.csproj -s src\apps\DataFlow.Api\DataFlow.Api.csproj
```

### 3. Aplicar Migration no Banco
Ainda na raiz, rode:

```bash
dotnet ef database update -c IngestionDbContext -p src\libs\DataFlow.Infrastructure\DataFlow.Infrastructure.csproj -s src\apps\DataFlow.Api\DataFlow.Api.csproj
```

Isso cria todas as tabelas (`Clients`, `ImportBatches`, `ImportItems`, `BatchLocks`, etc.) dentro de `DataFlowDev`.

### 4. Seed de Clientes
No `appsettings`, configure a seção `ClientSeed` com os pares `ClientId/ClientSecret` que cada aplicação usará (ex.: OmniFlow). Exemplo:

```json
"ClientSeed": {
  "Clients": [
    {
      "Name": "OmniFlow",
      "ClientIdentifier": "omniflow",
      "Secret": "omniflow-prod-secret"
    }
  ]
}
```

Ao iniciar o `DataFlow.Api` com `ApplyMigrationsOnStartup=true`, o seed cria (ou rotaciona) o cliente automaticamente.

### 5. Teste Rápido
1. Envie um arquivo para `POST /imports` com os cabeçalhos `X-Client-Id`/`X-Client-Secret`.
2. Consulte `GET /imports/{batchId}` para confirmar que o lote está registrado (`Pending`).

Com esses passos finalizados, o DataFlow está pronto para evoluir para a Fase 2 (worker + lotes). 

