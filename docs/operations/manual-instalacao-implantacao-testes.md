# Manual de Instalação, Implantação e Testes — DataFlow

Este manual conduz a preparação completa do ambiente: pré-requisitos, configuração de certificados/hostnames, subida do stack com Docker Compose, testes de ingestão, verificação de observabilidade e geração do relatório final.

## 1. Objetivos

- Disponibilizar API, Worker, RabbitMQ, Postgres, Redis, OpenTelemetry Collector, Prometheus, Grafana (com renderer) e Reporting Service.
- Validar ingestão assíncrona (upload → fila → processamento → persistência).
- Confirmar métricas/dashboards e gerar relatório consolidado.

## 2. Pré-requisitos

- Windows 10/11 com PowerShell.
- Docker Desktop (com Docker Compose v2).
- Opcional:
  - `.NET 9 SDK` — executar serviços fora do Docker, se necessário.
  - Node.js (`npx @mermaid-js/mermaid-cli`) — renderizar diagramas Mermaid.

## 3. Estrutura do Repositório

- `src/` — projetos .NET (apps e libs).
- `scripts/` — utilitários (certificados, ingestão, diagramas).
- `docs/` — documentação segmentada em `architecture/`, `operations/` e `templates/`.

## 4. Preparação inicial

1. Clone o repositório e abra PowerShell na raiz.
2. Gere certificados autoassinados:

   ```powershell
   scripts\certs\generate-dev-certs.cmd
   ```

3. Configure hostnames locais (editar `C:\Windows\System32\drivers\etc\hosts` como Administrador):

   ```text
   127.0.0.1 api.local
   127.0.0.1 reporting.local
   ```

4. Garanta a existência da rede Docker:

   ```powershell
   docker network create docker-network
   ```

## 5. Subir o stack com Docker Compose

1. Dependências e observabilidade:

   ```powershell
   docker compose up -d postgres redis rabbitmq otel-collector prometheus grafana-renderer grafana
   ```

2. Serviços de aplicação e proxy:

   ```powershell
   docker compose --profile proxy --profile api --profile worker --profile reporting up -d
   ```

3. Conferir contêineres:

   ```powershell
   docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
   ```

## 6. Health checks rápidos

- Grafana: `http://localhost:3000`
- Prometheus: `http://localhost:9090`
- API via proxy: `https://api.local:8443/health`
- Reporting via proxy: `https://reporting.local:8444/health`

> Certificados são autoassinados: aceite o aviso do navegador ou importe os `.pem` gerados.

## 7. Enviar jobs de ingestão

1. **(Opcional)** Gere um arquivo CSV grande para testes:

   ```cmd
   # Modo interativo (solicita o tamanho no console)
   scripts\ingestion\gerar-csv-grande.cmd
   
   # Ou passe o tamanho como parâmetro
   scripts\ingestion\gerar-csv-grande.cmd -TamanhoMB 100
   ```
   
   **Nota**: Se executar sem parâmetros, o script solicitará o tamanho em MB. Pressione Enter para usar 50 MB como padrão.

   O arquivo será salvo em `files/large-test-data.csv`.

2. Gere parâmetros com o batch:

   ```cmd
   scripts\ingestion\gera-parametros.bat
   ```

   Informe o caminho (ex.: `files\sample-data.csv` ou `files\large-test-data.csv`) e copie os valores exibidos.

3. Envie o arquivo:

   ```bash
   curl -k -X POST https://api.local:8443/ingestion/jobs \
        -F "file=@files/sample-data.csv" \
        -F "clientId=cliente-demo" \
        -F "fileType=csv" \
        -F "checksum=<INSIRA O HASH>"
   ```

4. Verifique o status:

   ```bash
   curl -k https://api.local:8443/ingestion/jobs/{id}
   ```

## 8. Observabilidade

- Prometheus (exemplos):

  ```promql
  histogram_quantile(0.95, sum(rate(http_request_duration_seconds_bucket{job="dataflow-api"}[5m])) by (le))
  ```

  ```promql
  sum by(status) (rate(http_requests_total{job="dataflow-api"}[5m]))
  ```

- Grafana: dashboards provisionados em `http://localhost:3000` (exporte PNG pelo renderer).
- OTEL Collector: métricas em `http://localhost:8889/metrics`.

## 9. Capturar evidências

- Siga `docs/operations/screenshots/README.md` (nomenclatura `YYYYMMDD-HHMM-descricao.png`).
- Armazene em `docs/operations/screenshots/`.

## 10. Gerar relatório final

- Automático via ReportingService:

  ```powershell
  $body = @{ job = "dataflow-api"; window = "5m"; outputDir = "docs" } | ConvertTo-Json
  Invoke-RestMethod -Method Post -Uri https://reporting.local:8444/reports/final -Body $body -ContentType "application/json" -SkipCertificateCheck
  ```

  Resultado: `docs/RELATORIO-FINAL-<timestamp>.md`.
- Manual: utilize `docs/templates/modelo-relatorio-final.md`.

## 11. Diagramas (opcional)

- Renderizar Mermaid:

  ```powershell
  scripts\diagrams\render_mermaid_diagram.cmd
  ```

## 12. Critérios de aceite (referência)

- Latência p95 ≤ 500 ms.
- Erros 5xx < 1%.
- Requests ativos estáveis.

## 13. Troubleshooting

- Portas ocupadas → ajuste mapeamentos no `docker-compose.yml` ou libere via `Get-NetTCPConnection`.
- Renderer do Grafana falhando → reinicie `grafana` e `grafana-renderer`; confirme `GF_RENDERING_*`.
- Sem métricas → verifique `otel-collector` e scrapes do Prometheus.
- Fila não consumida → analise logs do `data-flow-worker` e credenciais do RabbitMQ.

## 14. FAQ rápido

- **Como subir tudo rapidamente?** — siga seções 4/5 e execute os comandos indicados.
- **Preciso de algo além do Docker?** — opcionalmente .NET SDK (execução local sem containers) e Node.js/Mermaid para diagramas.
- **Onde ficam as métricas?** — Grafana (`http://localhost:3000`) e Prometheus (`http://localhost:9090`).
- **Como gerar o relatório?** — endpoint `POST /reports/final` (ver seção 10) ou modelos em `docs/templates/`.
- **Quero rodar fora do Docker. O que fazer?** — consulte `docs/operations/tutoriais/run-outside-docker.md` para entender as adaptações necessárias; por padrão o projeto é suportado via Docker Compose.

## 15. Referências

- `docs/README.md` — visão geral da documentação.
- `docs/architecture/dataflow-technical-architecture.md` — arquitetura detalhada.
- `docs/operations/tutoriais/endpoints-dataflow.md` — uso dos endpoints.
- `docker-compose.yml` — orquestração do stack.
