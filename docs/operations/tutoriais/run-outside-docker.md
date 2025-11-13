# Execução do DataFlow Fora do Docker (Modo Manual)

> ⚠️ Este fluxo é opcional e exige configuração manual de todas as dependências. A forma suportada oficialmente para avaliação é via Docker Compose. Utilize este guia apenas se precisar depurar localmente sem contêineres.

## 1. Pré-requisitos

1. **.NET 9 SDK** instalado.
2. **Runtime das dependências** instaladas localmente:
   - PostgreSQL 15+ (acesso *trust* ou usuário com permissões de criação).
   - Redis 7+.
   - RabbitMQ 3.12+ (plugin management opcional).
3. **Ferramentas adicionais** (opcional, mas recomendadas):
   - `dotnet-ef` (`dotnet tool install --global dotnet-ef`) para aplicar migrations.
   - Node.js (caso deseje manter diagramas/grafana scripts).

## 2. Provisionar dependências

1. **PostgreSQL**
   - Crie um banco, por exemplo `dataflow`.
   - Defina usuário/senha (ex.: `dataflow_admin` / `dataflow_admin`).
   - Verifique a porta (padrão 5432).
2. **Redis**
   - Inicie o serviço (`redis-server`).
3. **RabbitMQ**
   - Inicie o serviço (`rabbitmq-service start`).
   - Opcional: habilite UI (`rabbitmq-plugins enable rabbitmq_management`) e acesse `http://localhost:15672`.

## 3. Variáveis de ambiente

Configure as variáveis abaixo para que os projetos encontrem as dependências locais (exemplo em PowerShell):

```powershell
$env:ConnectionStrings__Postgres = "Host=localhost;Port=5432;Database=dataflow;Username=dataflow_admin;Password=dataflow_admin"
$env:ConnectionStrings__Redis = "localhost:6379"
$env:Rabbit__Connection = "amqp://guest:guest@localhost:5672/"
$env:RABBIT__HOST = "localhost"
$env:REDIS__HOST = "localhost"
$env:POSTGRES__HOST = "localhost"
```

Para desenvolvimento, você pode criar um arquivo `appsettings.Development.local.json` em cada projeto com essas configurações, desde que não seja versionado.

## 4. Aplicar migrations (Postgres)

1. Entre na pasta da API:
   ```powershell
   cd src\apps\DataFlow.Api
   dotnet ef database update
   ```
2. Opcionalmente, repita na pasta do Worker (usa o mesmo contexto através da infraestrutura). Certifique-se de que a conexão está correta.

## 5. Executar os serviços

### API
```powershell
cd src\apps\DataFlow.Api
dotnet run --project DataFlow.Api.csproj
```
Disponível por padrão em `https://localhost:5001` (ou conforme `launchSettings.json`). Ajuste `ASPNETCORE_URLS` se quiser outra porta.

### Worker
```powershell
cd src\apps\DataFlow.Worker
dotnet run --project DataFlow.Worker.csproj
```
Confirme que o Worker consegue conectar-se ao RabbitMQ/Redis/Postgres.

### ReportingService
```powershell
cd src\apps\DataFlow.ReportingService
dotnet run --project DataFlow.ReportingService.csproj
```
Verifique `PROMETHEUS_URL` e `GRAFANA_URL`; configure via ambiente (ex.: `http://localhost:9090` e `http://localhost:3000`).

## 6. Observabilidade fora do Docker

- Se quiser manter Prometheus/Grafana/OTel Collector localmente, será necessário instalá-los manualmente.
- Alternativa: subir apenas o stack de observabilidade via Docker Compose (`docker compose up -d otel-collector prometheus grafana grafana-renderer`) e apontar os serviços locais para esses endpoints.

## 7. Ajuste de endpoints

Sem o proxy Nginx, a API e o Reporting expõem Swagger diretamente nas portas de desenvolvimento (`https://localhost:<porta>/swagger`). Caso queira manter hostnames `api.local`/`reporting.local`, configure um proxy reverso local (IIS Express, Nginx ou similar) ou atualize as URLs no cliente.

## 8. Scripts úteis

- `scripts/ingestion/gera-parametros.bat` continua válido para gerar checksum e metadados.
- Para enviar jobs, ajuste os comandos `curl` apontando para o host/porta onde a API está rodando (`https://localhost:5001/ingestion/jobs`).

## 9. Limitações

- Os Dockerfiles contêm configurações específicas (por exemplo, scripts de espera) que não são executados neste modo.
- Certificados HTTPS locais precisarão ser gerenciados manualmente (use `dotnet dev-certs https --trust`).
- O workflow oficial de avaliação continua sendo o Docker Compose; mantenha esse modo como referência para entrega.

---

> Resumo: é possível rodar DataFlow fora do Docker, porém exige instalar e configurar manualmente Postgres, Redis, RabbitMQ e (opcionalmente) o stack de observabilidade. Utilize este guia como checklist e ajuste as variáveis conforme o seu ambiente.
