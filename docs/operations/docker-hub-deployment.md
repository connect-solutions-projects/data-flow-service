# Guia de Deploy para Docker Hub

Este guia explica como fazer build e publicar as imagens Docker do DataFlow no Docker Hub.

## 📋 Pré-requisitos

1. **Conta no Docker Hub**: Crie uma conta em [hub.docker.com](https://hub.docker.com)
2. **Docker instalado**: Docker Desktop ou Docker Engine
3. **Acesso ao repositório**: Clone o repositório do DataFlow

## 🚀 Método 1: Usando Scripts Automatizados

### PowerShell (Recomendado)

```powershell
# Build e push com versão específica
.\scripts\docker\build-and-push.ps1 -DockerHubUsername "seu-usuario" -Version "1.0.0"

# Build e push com tag latest
.\scripts\docker\build-and-push.ps1 -DockerHubUsername "seu-usuario"

# Apenas build (sem push)
.\scripts\docker\build-and-push.ps1 -DockerHubUsername "seu-usuario" -Version "1.0.0" -BuildOnly

# Apenas push (assumindo que já fez build)
.\scripts\docker\build-and-push.ps1 -DockerHubUsername "seu-usuario" -Version "1.0.0" -PushOnly
```

### CMD (Windows)

```cmd
REM Build e push com versão específica
scripts\docker\build-and-push.cmd seu-usuario 1.0.0

REM Build e push com tag latest
scripts\docker\build-and-push.cmd seu-usuario
```

## 🔧 Método 2: Comandos Manuais

### 1. Login no Docker Hub

```bash
docker login -u seu-usuario
```

Você será solicitado a inserir sua senha ou token de acesso.

### 2. Build das Imagens

Execute os comandos a partir da **raiz do repositório**:

```bash
# Build da API
docker build -f src/apps/DataFlow.Api/Dockerfile -t seu-usuario/dataflow-api:1.0.0 .

# Build do Worker
docker build -f src/apps/DataFlow.Worker/Dockerfile -t seu-usuario/dataflow-worker:1.0.0 .

# Build do Reporting Service
docker build -f src/apps/DataFlow.ReportingService/Dockerfile -t seu-usuario/dataflow-reporting:1.0.0 .
```

### 3. Tag Latest (Opcional)

Se quiser também criar tags `latest`:

```bash
docker tag seu-usuario/dataflow-api:1.0.0 seu-usuario/dataflow-api:latest
docker tag seu-usuario/dataflow-worker:1.0.0 seu-usuario/dataflow-worker:latest
docker tag seu-usuario/dataflow-reporting:1.0.0 seu-usuario/dataflow-reporting:latest
```

### 4. Push para Docker Hub

```bash
# Push da API
docker push seu-usuario/dataflow-api:1.0.0
docker push seu-usuario/dataflow-api:latest

# Push do Worker
docker push seu-usuario/dataflow-worker:1.0.0
docker push seu-usuario/dataflow-worker:latest

# Push do Reporting
docker push seu-usuario/dataflow-reporting:1.0.0
docker push seu-usuario/dataflow-reporting:latest
```

## 📦 Estrutura das Imagens

As imagens publicadas seguirão o padrão:

- `seu-usuario/dataflow-api:versao`
- `seu-usuario/dataflow-worker:versao`
- `seu-usuario/dataflow-reporting:versao`

## 🔄 Usando Imagens do Docker Hub

Após publicar, você pode atualizar o `docker-compose.yml` para usar as imagens do Docker Hub ao invés de fazer build local:

```yaml
data-flow-api:
  image: seu-usuario/dataflow-api:1.0.0  # ou :latest
  # Remova a seção 'build' se estiver usando imagem do Hub
  # build:
  #   context: .
  #   dockerfile: src/apps/DataFlow.Api/Dockerfile
```

## 🔐 Autenticação com Token

Para CI/CD ou automação, use um **Personal Access Token** ao invés de senha:

1. Acesse: https://hub.docker.com/settings/security
2. Crie um novo token
3. Use o token como senha:

```bash
echo "seu-token" | docker login -u seu-usuario --password-stdin
```

## ✅ Verificação

Após o push, verifique as imagens no Docker Hub:

1. Acesse: https://hub.docker.com/r/seu-usuario/dataflow-api
2. Confirme que as tags estão disponíveis

## 🐛 Troubleshooting

### Erro: "denied: requested access to the resource is denied"

- Verifique se fez login corretamente: `docker login`
- Confirme que o nome de usuário está correto
- Verifique permissões da conta Docker Hub

### Erro: "unauthorized: authentication required"

- Faça login novamente: `docker logout` e depois `docker login`
- Verifique se o token/senha está correto

### Build falha

- Certifique-se de estar na raiz do repositório
- Verifique se todos os arquivos necessários estão presentes
- Execute `docker system prune` se houver problemas de cache

## 📝 Exemplo Completo

```bash
# 1. Login
docker login -u meu-usuario

# 2. Build e tag
docker build -f src/apps/DataFlow.Api/Dockerfile -t meu-usuario/dataflow-api:1.0.0 .
docker tag meu-usuario/dataflow-api:1.0.0 meu-usuario/dataflow-api:latest

# 3. Push
docker push meu-usuario/dataflow-api:1.0.0
docker push meu-usuario/dataflow-api:latest

# Repetir para worker e reporting...
```

## 🎯 Próximos Passos

Após publicar no Docker Hub, você pode:

1. Usar as imagens em outros ambientes
2. Configurar CI/CD para publicar automaticamente
3. Compartilhar as imagens com sua equipe
4. Usar em Kubernetes ou outros orquestradores

