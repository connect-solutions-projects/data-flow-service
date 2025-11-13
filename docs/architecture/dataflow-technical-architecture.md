# Arquitetura Técnica Detalhada — DataFlow

## 1. Visão Geral

DataFlow é uma plataforma de ingestão assíncrona construída em .NET 9, organizada em um monorepo (um único repositório contendo todos os serviços e bibliotecas). O objetivo é receber arquivos de grande porte, processá-los em background, expor métricas/relatórios e simplificar a operação através de uma stack containerizada (Docker Compose).

A arquitetura segue princípios de separação por camadas, comunicação assíncrona via fila e observabilidade nativa. O diagrama de alto nível está disponível em `architecture/arquitetura-pipeline.png` (fonte Mermaid em `architecture/arquitetura.mmd`).

## 2. Organização do Monorepo

Adotamos um monorepo porque todos os serviços compartilham o mesmo conjunto de domínios, contratos e utilitários. Em uma demonstração técnica com prazo curto, concentrar tudo em um único repositório simplifica versionamento, reduz overhead de publicação de pacotes e garante que a arquitetura (API, Worker, Reporting e libs) evolua em sincronia. Também facilita a reprodução do ambiente por avaliadores: basta clonar um repositório para ter acesso à solução completa, scripts de suporte e documentação.

```text
src/
  apps/
    DataFlow.Api/
    DataFlow.Worker/
    DataFlow.ReportingService/
  libs/
    DataFlow.Core.Domain/
    DataFlow.Core.Application/
    DataFlow.Infrastructure/
    DataFlow.Observability/
    DataFlow.Shared/
...
scripts/
  certs/
  diagrams/
  ingestion/
docs/
  architecture/
  operations/
  templates/
```

### 2.1 Benefícios do monorepo

- **Refatorações coordenadas**: alterações no domínio ou contratos são propagadas para API, Worker e Reporting numa única PR.
- **Builds e testes integrados**: pipelines conseguem validar todos os projetos simultaneamente, reduzindo “drifts” entre serviços.
- **Reuso explícito**: bibliotecas compartilhadas (`Core`, `Infrastructure`, `Shared`, `Observability`) residem no mesmo repositório, evitando packages externos ou `git submodules`.
- **Governança uniforme**: lint, estilos, scripts e documentação vivem lado a lado; onboarding mais rápido.
- **Releases coesos**: versionamento sem divergência entre múltiplos repositórios.

### 2.2 Trade-offs do monorepo

- Necessidade de governança para evitar acoplamento excessivo entre serviços.
- Requer disciplina de CI/CD para não quebrar projetos não relacionados.

## 3. Serviços e Responsabilidades

| Serviço | Função | Tecnologia |
|---------|--------|------------|
| `DataFlow.Api` | Recebe uploads (`POST /ingestion/jobs`), valida, publica mensagens no RabbitMQ, consulta estado de jobs. | ASP.NET Core Minimal API (.NET 9), MassTransit. |
| `DataFlow.Worker` | Consome mensagens da fila, processa arquivos, aplica validações e persiste resultados. | Worker Service (.NET 9) + MassTransit. |
| `DataFlow.ReportingService` | Gera relatórios (ex.: Markdown/PDF) a partir de métricas Prometheus e links Grafana. | ASP.NET Core Minimal API. |
| Proxy (Nginx) | Termina TLS, expõe `/swagger`, `/reporting`, roteia para serviços internos. | Nginx + certificados autoassinados. |
| Observabilidade | OTel Collector, Prometheus, Grafana, Grafana Image Renderer. | Docker Compose stack. |
| Banco de Dados | Persistência dos jobs/estado. | PostgreSQL + EF Core. |
| Cache/Coordenação | Reservas de checksum, rate-limiting distribuído. | Redis. |
| Mensageria | Desacoplamento entre ingestão e processamento. | RabbitMQ + MassTransit. |

## 4. Fluxo de Ingestão Detalhado

1. **Upload** (`POST /ingestion/jobs`): cliente envia arquivo via multipart com metadados (`clientId`, `fileType=csv`, `checksum`).
2. **Validações rápidas** na API:
   - Rate limiting por cliente (Redis).
   - Deduplicação por checksum (Redis reserva/associação).
   - Registro do job (`CreateJobHandler` → EF Core / Postgres).
3. **Publicação**: MassTransit envia `ProcessJobMessage` para RabbitMQ.
4. **Processamento** (Worker):
   - Consome mensagem.
   - Abre arquivo do storage.
   - Conta registros / aplica `IValidationRule` (atualmente `NoOp` para CSV, extensão prevista).
   - Atualiza status/job no banco.
5. **Observabilidade**: API e Worker publicam métricas/traces via OpenTelemetry → OTel Collector → Prometheus.
6. **Relatórios**: ReportingService consulta Prometheus (latência p95, taxa por status, requisições ativas) e coleta links sugeridos Grafana → gera Markdown/arquivo final.

## 5. Camadas e Bibliotecas Compartilhadas

| Biblioteca | Conteúdo | Dependências |
|------------|----------|---------------|
| `DataFlow.Core.Domain` | Entidades (`IngestionJob`), agregados, enums (`FileType`), eventos, exceções. | Sem dependência de infraestrutura. |
| `DataFlow.Core.Application` | Handlers (MediatR), comandos/queries, orchestrators, validações (FluentValidation), interfaces de portas. | Depende de Domain; injeta repositórios/serviços via DI. |
| `DataFlow.Infrastructure` | Implementações de portas: EF Core (Postgres), Redis cache, RabbitMQ (MassTransit), parsers (`CsvFileParser`), regras (`NoOpValidationRule`). | Externos: EF Core, StackExchange.Redis, MassTransit. |
| `DataFlow.Observability` | Registradores de métricas custom (`Metrics.RateLimit429Counter`, etc.). | OpenTelemetry. |
| `DataFlow.Shared` | Contratos/mensagens compartilhadas (ex.: `ProcessJobMessage`). | Referenciado por API, Worker, Reporting. |

## 6. Containerização e Proxy

- **Docker Compose** organiza serviços em perfis (`api`, `worker`, `reporting`, `proxy`).
- Proxy Nginx expõe:
  - `https://api.local:8443/*` → API.
  - `https://reporting.local:8444/*` → Reporting.
- Certificados gerados por `scripts/certs/generate-dev-certs.cmd`. Hostnames mapeados em `C:\Windows\System32\drivers\etc\hosts`.
- API/Reporting são executados em containers `aspnet:9.0`. Worker usa `runtime:9.0`.

## 7. Observabilidade

- **OpenTelemetry**: `AddDataFlowTelemetry` registra métricas, traces e logging (opcional).
- **Prometheus**: scrapes do OTel Collector, expõe consultas (padrões documentados em `operations/REPORTING-SERVICE.md`).
- **Grafana**: dashboards + Image Renderer (variáveis `GF_RENDERING_*`).
- **Relatórios**: ReportingService orquestra Prometheus + Grafana para gerar documentos.

## 8. Estratégia de Escalabilidade

- **Horizontal**: API escala múltiplas instâncias atrás do proxy; Worker escala consumidores do RabbitMQ; reporting pode ser replicado se necessário.
- **Backpressure**: RabbitMQ armazena jobs e mantém reentregas em caso de falha.
- **Cache**: Redis ajuda em rate limiting e deduplicação (evitando hits ao banco).
- **Separação de responsabilidades**: ingestão, processamento, relatórios e observabilidade independentes, facilitando implantação gradual.

## 9. Segurança e Conectividade

- TLS encerrado no proxy Nginx (certificados autoassinados nesta demo). Produção deveria usar Let’s Encrypt ou certificados válidos.
- Autenticação/autorização não implementadas (requisito fora de escopo do teste), mas arquitetura suporta middleware / API Gateway.
- Configurações sensíveis via variáveis de ambiente (Docker Compose).

## 10. Roteiro de Operação

1. Criar rede: `docker network create docker-network`.
2. Subir stack principal (dependências + serviços).
3. Mapear hostnames (`api.local`, `reporting.local`).
4. Executar `scripts/ingestion/gera-parametros.bat` para gerar checksum.
5. Postar arquivo via API.
6. Acompanhar processamento no Grafana/Prometheus.
7. Gerar relatório final pelo ReportingService.

## 11. Próximos Passos Técnicos

- Implementar parsers/validações reais para `json` e `parquet` (`IFileParser`, `IValidationRule`).
- Enriquecer ReportingService (PDF, anexos de imagens renderizadas).
- Automatizar upload para storage distribuído (S3/Azure Blob).
- Adicionar autenticação/JWT ou API Keys na API e no Reporting.
- Configurar CI/CD para builds automatizados dos projetos `apps` e execução de testes unitários/integrados.

## 12. Referências

- `docs/architecture/dataflow-technical-architecture.md` (este documento)
- `docs/architecture/decisoes-tecnicas.md`
- `docs/operations/manual-instalacao-implantacao-testes.md`
- `docs/operations/README.md`
- `docker-compose.yml`
- `src/apps/*/Dockerfile`
- `scripts/*` para utilitários

Esta visão técnica deve ser atualizada sempre que novos componentes ou decisões estruturais forem adicionados à plataforma.
