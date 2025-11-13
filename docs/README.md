# DataFlow — Documentação

Este diretório concentra todos os materiais de apoio ao projeto, organizados conforme a finalidade.

## Estrutura

| Pasta | Conteúdo |
|-------|----------|
| `architecture/` | Diagramas, visão técnica e decisões de arquitetura. |
| `operations/` | Guias de instalação/testes, tutoriais e evidências. |
| `templates/` | Modelos reutilizáveis (ex.: relatório final). |

Consulte os READMEs de cada pasta para detalhes.

## Comece por aqui

1. **Preparar ambiente** – veja `operations/README.md` e o manual `operations/manual-instalacao-implantacao-testes.md`.
2. **Entender arquitetura** – leia `architecture/dataflow-technical-architecture.md` e `architecture/decisoes-tecnicas.md`.
3. **Executar ingestões** – siga o tutorial `operations/tutoriais/endpoints-dataflow.md`; use `scripts/ingestion/gera-parametros.bat` para obter `checksum` e demais parâmetros.
4. **Gerar relatórios** – consulte `operations/manual-instalacao-implantacao-testes.md` (seção de relatórios) e `templates/modelo-relatorio-final.md` para o formato.

## Dicas rápidas

- Certificados autoassinados para o proxy: `scripts/certs/generate-dev-certs.cmd`.
- Arquivos de teste de upload: 
  - `files/sample-data.csv` — arquivo pequeno para testes rápidos
  - Gerar arquivo grande: `scripts/ingestion/gerar-csv-grande.cmd` (modo interativo ou passe `-TamanhoMB`)
- Acesso Web padrão:
  - Grafana: `http://localhost:3000`
  - Prometheus: `http://localhost:9090`
  - Swagger API: `https://api.local:8443/swagger`
  - Swagger Reporting: `https://reporting.local:8444/swagger`

Mantenha esta documentação sincronizada com as evoluções do projeto para que novos participantes encontrem rapidamente o material necessário.
