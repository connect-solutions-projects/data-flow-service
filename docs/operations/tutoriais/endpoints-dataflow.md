# Guia de Endpoints – Datlo DataFlow

Este documento resume como exercitar os serviços principais do DataFlow e como preparar os parâmetros necessários para as chamadas.

Serviços contemplados:

- **API** (`DataFlow.Api`) – ingestão de arquivos e gestão de jobs.
- **Reporting** (`DataFlow.ReportingService`) – geração de relatórios a partir das métricas coletadas.

Quando a stack está rodando via Docker Compose com o proxy:

- API: `https://api.local:8443`
- Reporting: `https://reporting.local:8444`

> **Observação:** Esta demonstração processa apenas arquivos CSV. Os tipos `json` e `parquet` ainda não possuem parsers/validações implementados.

---

## Configurando hostnames locais

**Passo 1:** Abra o Bloco de Notas como **Administrador**.

**Passo 2:** Em `Arquivo > Abrir`, vá até `C:\Windows\System32\drivers\etc\hosts` (selecione “Todos os arquivos”).

**Passo 3:** Adicione ao final:

```text
127.0.0.1 api.local
127.0.0.1 reporting.local
```

**Passo 4:** Salve o arquivo.

**Passo 5:** Reinicie o proxy (caso já esteja em execução):

```powershell
docker compose --profile proxy restart proxy
```

> Se preferir não editar o `hosts`, adapte `docker-compose.yml`/Nginx para usar `localhost` diretamente.

---

## 1. Script auxiliar (`scripts\gera-parametros.bat`)

O script facilita o preenchimento dos campos derivados do arquivo para o endpoint `/ingestion/jobs`.

1. Abra um Prompt de Comando na raiz do repositório.
2. Execute:

   ```bat
   scripts\gera-parametros.bat
   ```

3. Informe o caminho completo do arquivo quando solicitado (ex.: `C:\dados\lote.csv`).
4. Opcionalmente informe o `clientId` como segundo argumento: `scripts\gera-parametros.bat C:\dados\lote.csv cliente-acme`.
5. O script calcula automaticamente:
   - `checksum` (SHA-256)
   - `fileName`
   - `fileSize`
   - `contentType` sugerido
   - `fileType` (fixado em `csv` nesta demonstração)
6. Copie os valores exibidos e utilize na chamada HTTP.

> **Arquivos de teste disponíveis:**
> - `files/sample-data.csv` — arquivo CSV pequeno para testes rápidos
> - `files/large-test-data.csv` — arquivo CSV grande gerado pelo script `gerar-csv-grande.cmd` (veja seção abaixo)

### Gerar arquivo CSV grande para testes

Para testar uploads de arquivos grandes, use o script de geração:

```bash
# Modo interativo (solicita o tamanho no console)
scripts\ingestion\gerar-csv-grande.cmd

# Ou passe o tamanho como parâmetro
scripts\ingestion\gerar-csv-grande.cmd -TamanhoMB 100
scripts\ingestion\gerar-csv-grande.cmd -TamanhoMB 200
```

**Nota**: Se executar sem parâmetros, o script solicitará o tamanho em MB. Pressione Enter para usar 50 MB como padrão.

O arquivo será gerado em `files/large-test-data.csv` com dados aleatórios. Depois, use `gera-parametros.bat` para obter os parâmetros necessários para o upload.

---

## 2. API de Ingestão (`DataFlow.Api`)

### 2.1 Health Check

- **Endpoint:** `GET /health`
- **Resposta 200:** `{ "status": "ok" }`

### 2.2 Criar Job de Ingestão

- **Endpoint:** `POST /ingestion/jobs`
- **Conteúdo:** `multipart/form-data`
- **Campos obrigatórios:**
  - `file`: arquivo CSV (gerar parâmetros via batch).
  - `clientId`: identificador do cliente.
  - `fileType`: `csv` (único suportado aqui).
  - `checksum`: SHA-256 do arquivo.
- **Campos opcionais:**
  - `fileName`: nome amigável (default: nome original).
  - `contentType`: MIME type (default: detectado pelo batch).
- **Fluxo interno:** deduplicação por checksum → criação do job → upload para storage → publicação em RabbitMQ.

Exemplo `curl` (substitua os valores pelo resultado do batch):

```bash
curl -k -X POST https://api.local:8443/ingestion/jobs \
     -F "file=@files/sample-data.csv" \
     -F "clientId=cliente-demo" \
     -F "fileType=csv" \
     -F "checksum=INSIRA_O_CHECKSUM"
```

Respostas comuns: `201 Created`, `400 Bad Request`, `409 Conflict`, `429 Too Many Requests`.

### 2.3 Consultar Job por ID

- **Endpoint:** `GET /ingestion/jobs/{id}`
- **Descrição:** retorna o status atual (usa cache Redis por 10s).

```bash
curl -k https://api.local:8443/ingestion/jobs/11111111-2222-3333-4444-555555555555
```

- `200 OK`: JSON com status.
- `404 Not Found`: job inexistente.

### 2.4 Processar novamente

- `POST /ingestion/jobs/{id}/process`: re-enfileira o job (retorna `202`).
- `POST /ingestion/jobs/{id}/retry`: reexecuta job que falhou (retorna `200`).
- `POST /ingestion/jobs/{id}/reprocess`: reseta/reprocessa imediatamente (retorna `202`).

### 2.5 Listar uploads

- **Endpoint:** `GET /storage/uploads`
- **Uso:** diagnóstico para ver arquivos presentes no storage.
- **Resposta:** array JSON com metadados.

---

## 3. Reporting Service (`DataFlow.ReportingService`)

### 3.1 Health Check

- **Endpoint:** `GET /health`
- **Resposta:** `{ "status": "ok", "service": "reporting" }`

### 3.2 Relatório final

- **Endpoint:** `POST /reports/final`
- **Conteúdo:** `application/json`
- **Campos:**
  - `job` (opcional, padrão `dataflow-api`).
  - `window` (opcional, padrão `5m`).
  - `outputDir` (opcional, diretório dentro do container).

```bash
curl -k -X POST https://reporting.local:8444/reports/final \
     -H "Content-Type: application/json" \
     -d '{
           "job": "dataflow-api",
           "window": "15m",
           "outputDir": "/tmp/reports"
         }'
```

Resposta 200: `{ "message": "Report generated", "path": "..." }`

### 3.3 Relatório de exemplo

- **Endpoint:** `GET /reports/sample`
- **Descrição:** usa dados mockados para validar o pipeline de relatório.

```bash
curl -k https://reporting.local:8444/reports/sample
```

Resposta 200: `{ "message": "Sample report generated", "path": "..." }`

---

## 4. Observações finais

1. **Certificados:** certificados autoassinados foram gerados para API/Reporting. Use `-k` nos testes ou importe os `.pem` (veja `scripts/generate-dev-certs.ps1`).
2. **Dependências externas:** certifique-se de que Postgres, Redis, RabbitMQ e observabilidade estejam rodando na stack paralela.
3. **Execução via Visual Studio:** ajuste `appsettings` ou variáveis de ambiente para apontar para os hosts corretos.
4. **Evolução de tipos:** para suportar `json` ou `parquet`, implemente `IFileParser` e `IValidationRule` específicos e registre-os em `ServiceCollectionExtensions`.

Mantenha este guia atualizado ao incluir novos endpoints ou parâmetros.
