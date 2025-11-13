# Guia de Grafana e Prometheus — DataFlow

Este documento explica como usar o Grafana e Prometheus no projeto DataFlow para monitoramento e observabilidade.

## 1. Visão Geral

### Prometheus
- **O que é**: Sistema de monitoramento e alerta que coleta métricas em formato de séries temporais
- **Função no DataFlow**: Armazena métricas coletadas do OpenTelemetry Collector (traces, métricas de performance, contadores customizados)
- **Acesso**: `http://localhost:9090`

### Grafana
- **O que é**: Plataforma de visualização e análise de dados que permite criar dashboards interativos
- **Função no DataFlow**: Visualiza métricas do Prometheus, permite criar dashboards e exportar imagens via Image Renderer
- **Acesso**: `http://localhost:3000`

## 2. Arquitetura de Observabilidade

```
API/Worker → OpenTelemetry → OTel Collector → Prometheus → Grafana
                                                      ↓
                                              Reporting Service
```

### Fluxo de Dados

1. **Coleta**: API e Worker publicam métricas/traces via OpenTelemetry
2. **Agregação**: OTel Collector recebe e processa os dados
3. **Armazenamento**: Prometheus faz scrape do OTel Collector e armazena as métricas
4. **Visualização**: Grafana consulta Prometheus e exibe dashboards
5. **Relatórios**: Reporting Service consulta Prometheus e Grafana para gerar relatórios

## 3. Acessando os Serviços

### Prometheus

**URL**: `http://localhost:9090`

**Funcionalidades**:
- Interface web para consultas PromQL
- Visualização de métricas em tempo real
- Exploração de séries temporais
- Configuração de alertas (opcional)

**Exemplo de consulta**:
```promql
rate(http_requests_total[5m])
```

### Grafana

**URL**: `http://localhost:3000`

**Credenciais padrão** (se configurado):
- Usuário: `admin`
- Senha: `admin` (solicita alteração no primeiro acesso)

**Funcionalidades**:
- Dashboards pré-configurados ou customizados
- Visualizações (gráficos, tabelas, gauges, etc.)
- Exportação de imagens via Image Renderer
- Alertas e notificações

## 4. Configuração no Projeto

### Prometheus

O Prometheus está configurado para fazer scrape do OpenTelemetry Collector:

- **Hostname interno**: `docker-prometheus` (na rede `dev_net`)
- **Porta externa**: `9090`
- **Datasource no Grafana**: `http://docker-prometheus:9090`

### Grafana

**Variáveis de ambiente** (configuradas no `docker-compose.yml`):
- `GF_RENDERING_SERVER_URL`: URL do Image Renderer (`http://docker-grafana-renderer:8081/render`)
- `GF_RENDERING_CALLBACK_URL`: URL de callback (`http://docker-grafana:3000/`)
- Datasource do Prometheus: `http://docker-prometheus:9090`

**Image Renderer**:
- Serviço separado que permite exportar dashboards como imagens (PNG)
- Usado pelo Reporting Service para incluir gráficos nos relatórios

## 5. Métricas Disponíveis

### Métricas do ASP.NET Core (automáticas)

- `http_requests_total`: Total de requisições HTTP
- `http_request_duration_seconds`: Duração das requisições
- `http_requests_in_progress`: Requisições em andamento

### Métricas Customizadas (DataFlow)

- `dataflow_rate_limit_429_total`: Contador de requisições bloqueadas por rate limit
- `dataflow_deduplication_hits_total`: Contador de arquivos duplicados detectados
- Métricas de runtime (.NET): GC, threads, memória

## 6. Consultas PromQL Úteis

### Taxa de Requisições (5 minutos)
```promql
rate(http_requests_total[5m])
```

### Latência P95 (percentil 95)
```promql
histogram_quantile(0.95, sum(rate(http_request_duration_seconds_bucket[5m])) by (le))
```

### Requisições por Status
```promql
sum by(status) (rate(http_requests_total[5m]))
```

### Taxa de Erros (5xx)
```promql
sum(rate(http_requests_total{status=~"5.."}[5m]))
```

### Requisições Ativas
```promql
sum(http_requests_in_progress)
```

### Rate Limit 429 (DataFlow)
```promql
rate(dataflow_rate_limit_429_total[5m])
```

### Deduplicação (DataFlow)
```promql
rate(dataflow_deduplication_hits_total[5m])
```

## 7. Criando Dashboards no Grafana

### Passo a Passo

1. **Acesse o Grafana**: `http://localhost:3000`
2. **Crie um novo Dashboard**: Clique em "Create" → "Dashboard"
3. **Adicione um Painel**: Clique em "Add visualization"
4. **Selecione o Datasource**: Escolha "Prometheus"
5. **Configure a Query**: Use PromQL (ex.: `rate(http_requests_total[5m])`)
6. **Escolha a Visualização**: Gráfico, tabela, gauge, etc.
7. **Salve o Dashboard**

### Exemplo de Dashboard Básico

**Painel 1: Taxa de Requisições**
- Query: `rate(http_requests_total[5m])`
- Visualização: Time series
- Título: "Taxa de Requisições por Segundo"

**Painel 2: Latência P95**
- Query: `histogram_quantile(0.95, sum(rate(http_request_duration_seconds_bucket[5m])) by (le))`
- Visualização: Time series
- Título: "Latência P95 (segundos)"

**Painel 3: Requisições por Status**
- Query: `sum by(status) (rate(http_requests_total[5m]))`
- Visualização: Stacked area
- Título: "Requisições por Status HTTP"

## 8. Integração com Reporting Service

O Reporting Service usa Prometheus e Grafana para gerar relatórios:

### Endpoint de Relatório Final

```powershell
$body = @{
    job = "dataflow-api"
    window = "5m"
    outputDir = "docs"
} | ConvertTo-Json

Invoke-RestMethod -Method Post `
    -Uri "https://reporting.local:8444/reports/final" `
    -Body $body `
    -ContentType "application/json" `
    -SkipCertificateCheck
```

### O que o Reporting Service faz

1. **Consulta Prometheus**: Obtém métricas usando PromQL
   - Latência P95
   - Taxa de requisições
   - Requisições por status
   - Requisições ativas

2. **Gera Links do Grafana**: Cria URLs para dashboards específicos

3. **Exporta Imagens** (opcional): Usa o Image Renderer para incluir gráficos no relatório

4. **Gera Markdown**: Consolida tudo em um documento final

## 9. Troubleshooting

### Prometheus não está coletando métricas

**Verificar**:
1. OTel Collector está rodando?
   ```powershell
   docker compose ps otel-collector
   ```

2. Prometheus está fazendo scrape?
   - Acesse `http://localhost:9090/targets`
   - Verifique se os targets estão "UP"

3. API/Worker estão publicando métricas?
   - Verifique logs: `docker compose logs data-flow-api`
   - Confirme variável `OTEL_EXPORTER_OTLP_ENDPOINT`

### Grafana não consegue conectar ao Prometheus

**Verificar**:
1. Datasource configurado corretamente?
   - Settings → Data sources → Prometheus
   - URL deve ser: `http://docker-prometheus:9090`

2. Rede Docker está correta?
   - Ambos devem estar na mesma rede (`dev_net`)

### Image Renderer não funciona

**Verificar**:
1. Serviço está rodando?
   ```powershell
   docker compose ps grafana-renderer
   ```

2. Variáveis de ambiente no Grafana:
   - `GF_RENDERING_SERVER_URL`
   - `GF_RENDERING_CALLBACK_URL`

## 10. Comandos Úteis

### Ver logs do Prometheus
```powershell
docker compose logs -f prometheus
```

### Ver logs do Grafana
```powershell
docker compose logs -f grafana
```

### Reiniciar serviços de observabilidade
```powershell
docker compose restart prometheus grafana grafana-renderer
```

### Verificar métricas disponíveis no Prometheus
1. Acesse `http://localhost:9090`
2. Vá em "Status" → "Targets"
3. Ou use a interface de query para explorar métricas

## 11. Referências

- **Documentação Prometheus**: https://prometheus.io/docs/
- **Documentação Grafana**: https://grafana.com/docs/
- **PromQL Guide**: https://prometheus.io/docs/prometheus/latest/querying/basics/
- **Arquitetura do DataFlow**: `docs/architecture/dataflow-technical-architecture.md`
- **Manual de Instalação**: `docs/operations/manual-instalacao-implantacao-testes.md`

## 12. Próximos Passos

- Criar dashboards customizados para métricas específicas do DataFlow
- Configurar alertas no Prometheus ou Grafana
- Exportar dashboards como JSON para versionamento
- Integrar com outros sistemas de monitoramento (opcional)

