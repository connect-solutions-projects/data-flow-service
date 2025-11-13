# Decisões Técnicas — Ingestão e Processamento Assíncrono

## API de Ingestão HTTP

A API expõe um endpoint para receber arquivos (ex.: `POST /ingestion/jobs`). Ela valida metadados, produz uma mensagem com o contexto do job e publica na fila para processamento assíncrono. A comunicação interna usa hostnames na rede `docker-network` (ex.: `docker-rabbitmq`, `docker-redis`) e configurações via variáveis de ambiente.

Na prática: você envia um arquivo pela API; ela registra o pedido e avisa quem processa via fila. Assim, a resposta é rápida e o trabalho pesado segue em segundo plano.

## Fila de Mensagens (RabbitMQ)

RabbitMQ coordena o desacoplamento entre quem produz (API) e quem consome (Workers). Usa exchanges/queues com ACKs e reentrega em caso de falhas. Garante ordenação por fila e permite escalonar consumidores. Está acessível por `docker-rabbitmq` na `docker-network` e a UI (se exposta) em `http://localhost:15672`.

Na prática: pense numa “esteira” onde os pedidos entram; os trabalhadores ficam pegando cada item e processando, sem travar a API.

## Processamento Assíncrono (Workers)

Os Workers consomem mensagens da fila e executam o pipeline: leitura, transformação, validação e persistência quando necessário. Implementam retentativas exponenciais, logs estruturados e métricas de duração/erros. Escalam horizontalmente apenas adicionando mais instâncias.

Na prática: são “operários” que fazem o serviço pesado fora da API. Se crescer a demanda, basta colocar mais operários.

## Redis (Coordenação e Cache)

Redis apoia funções de coordenação (locks, deduplicação) e cache leve para acelerar trechos do pipeline ou guardar estados transitórios. Hostname interno: `docker-redis`. Evita disputas (ex.: job duplicado) e dá velocidade quando algum dado pode ser reusado.

Na prática: é um “quadro branco” muito rápido para marcar o que já foi feito e guardar atalhos temporários.

## Observabilidade com OpenTelemetry Collector

O `otel-collector` recebe traces, métricas e logs dos serviços; roteia/exporta para backends como Prometheus/Grafana. Centraliza configuração de pipelines de telemetria e simplifica instrumentação dos serviços. Hostname interno: `docker-otel-collector`.

Na prática: é o “central de observação” que pega sinais dos sistemas e entrega de forma organizada para visualização.

## Métricas com Prometheus

Prometheus coleta métricas (pull) dos endpoints/targets configurados, armazena séries temporais e permite consultas com PromQL. Integra com o Collector ou scrapes diretos. Fica acessível em `http://localhost:9090` e internamente como `docker-prometheus`.

Na prática: é um banco de dados de números ao longo do tempo para medir quantidade, duração, erros e afins.

## Dashboards com Grafana e Renderização de Imagens

Grafana visualiza métricas do Prometheus e fornece dashboards. O serviço de renderização (`grafana-image-renderer`) está integrado via `GF_RENDERING_SERVER_URL` apontando para `http://docker-grafana-renderer:8081/render` e `GF_RENDERING_CALLBACK_URL` para `http://docker-grafana:3000/`. Datasource do Prometheus usa `http://docker-prometheus:9090`.

Na prática: é onde você enxerga gráficos e exporta imagens (PNG) dos painéis para anexar a relatórios.

## Rede Docker e Descoberta por Hostname

Todos os serviços estão na rede externa `docker-network`, usando hostnames internos padronizados (`docker-*`). Isso garante resolução de nomes entre contêineres e isolamento lógico. Exemplos: `docker-grafana`, `docker-prometheus`, `docker-rabbitmq`, `docker-redis`, `docker-otel-collector`.

Na prática: ao invés de IPs, cada serviço tem um “nome fácil” para conversar com os outros na mesma rede.

## Serviço de Relatórios (ReportingService)

Serviço que consolida indicadores em Markdown, usando `PROMETHEUS_URL=http://docker-prometheus:9090` e (quando necessário) links do Grafana. Expõe endpoints como `POST /reports/final` com janela de análise (ex.: `5m`) e diretório de saída (ex.: `docs/reports`).

Na prática: você aperta um botão e recebe um relatório pronto com métricas, links e seções de saúde.

> Nota: a escolha por Postgres atende o escopo do documento de descrição do teste enviado por e-mail; neste projeto provisionamos a base via Docker Compose, utilizamos EF Core para migrations e registramos o processo em `operations/manual-instalacao-implantacao-testes.md`.
