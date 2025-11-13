# Modelo — Relatório Final

Resumo executivo
- Objetivo do teste, escopo e principais achados.

Estado do Stack
- Serviços ativos na `docker-network` e portas expostas.
- Datasource do Grafana validado contra `docker-prometheus:9090`.

Métricas Principais (última janela: <window>)
- Taxa de requisições: `rate(http_requests_total[5m])`.
- Latência p95: `histogram_quantile(0.95, sum(rate(http_request_duration_seconds_bucket[5m])) by (le))`.
- Por status: `sum by(status) (rate(http_requests_total[5m]))`.

Dashboards e Imagens
- Links do Grafana com período aplicado.
- Imagens exportadas (PNG) anexadas.

Operações Executadas
- Comandos utilizados (docker compose, scripts, endpoints).
- Geração de tráfego e janela de análise.

Resultados e Observações
- Principais resultados, anomalias e insights.
- Riscos, limitações e sugestões.

Próximos Passos
- Ações recomendadas pós-teste.

Anexos
- Logs relevantes (trechos) e referências.

> Dicas
> - Exporte imagens via Grafana (Share → Export → PNG).
> - Gere relatório automático via `ReportingService` (output em `docs/reports`).
