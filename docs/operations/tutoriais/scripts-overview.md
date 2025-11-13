# Scripts Úteis do Projeto

Os utilitários foram organizados em subpastas dentro de `scripts/` conforme a finalidade. Abaixo está um guia rápido com a descrição de cada um e como executá-los.

## `scripts/certs/`

- **`generate-dev-certs.ps1`**: gera certificados autoassinados para os domínios `api.local` e `reporting.local`. Produz os arquivos `fullchain.pem` e `privkey.pem` nas pastas `certs/api/` e `certs/reporting/`.
- **`generate-dev-certs.cmd`**: wrapper para quem prefere chamar via Prompt de Comando. Internamente executa o script PowerShell acima.

**Como usar:**

```bash
scripts\certs\generate-dev-certs.cmd
```

Você pode informar parâmetros (por exemplo, outros domínios) passando-os após o cmd, exatamente como faria no `.ps1`.

## `scripts/ingestion/`

- **`gera-parametros.bat`**: solicita o caminho do arquivo (e opcionalmente o `clientId`), calcula SHA-256, tamanho, nome, `contentType` e define `fileType=csv`. Exibe tudo pronto para usar no `POST /ingestion/jobs`.

**Como usar:**

```bash
scripts\ingestion\gera-parametros.bat
```

Informe o caminho completo quando solicitado (ex.: `C:\dados\lote.csv`).

- **`gerar-csv-grande.ps1`** / **`gerar-csv-grande.cmd`**: gera um arquivo CSV de teste com tamanho configurável (padrão: 50 MB). Útil para testar uploads de arquivos grandes. O arquivo é salvo em `files/large-test-data.csv` com dados aleatórios realistas.

**Como usar:**

```bash
# Modo interativo (solicita o tamanho no console)
scripts\ingestion\gerar-csv-grande.cmd

# Ou passe o tamanho como parâmetro
scripts\ingestion\gerar-csv-grande.cmd -TamanhoMB 100
scripts\ingestion\gerar-csv-grande.cmd -TamanhoMB 200
```

**Nota**: Se executar sem parâmetros, o script solicitará o tamanho em MB no console. Pressione Enter para usar 50 MB como padrão.

O script gera um CSV com colunas: id, cliente, nome, email, telefone, endereço, cidade, estado, CEP, data_cadastro, valor, status, observações. Mostra progresso durante a geração e informa o tamanho final do arquivo.

## `scripts/diagrams/`

- **`render_mermaid_diagram.ps1`**: converte o diagrama `docs/finais/datlo/arquitetura.mmd` em PNG e SVG utilizando o `@mermaid-js/mermaid-cli`.
- **`render_mermaid_diagram.cmd`**: wrapper para execução rápida a partir do Prompt de Comando.

**Pré-requisito:** Node.js com `@mermaid-js/mermaid-cli` disponível via `npx`.

**Como usar:**

```bash
scripts\diagrams\render_mermaid_diagram.cmd
```

---

Essa organização facilita localizar o script certo para cada tarefa:

- **Certificados** → `scripts/certs/`
- **Auxílio de ingestão** → `scripts/ingestion/`
- **Diagramas** → `scripts/diagrams/`

Atualize este documento caso novos utilitários sejam adicionados ou existentes mudem de pasta.
