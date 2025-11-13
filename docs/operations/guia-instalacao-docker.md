# Guia de Instalação do Docker e Docker Compose

Este guia explica como instalar o Docker e Docker Compose no Windows, Linux e macOS, e como executar os arquivos docker-compose do projeto DataFlow.

## 📋 Índice

1. [Instalação no Windows](#instalação-no-windows)
2. [Instalação no Linux](#instalação-no-linux)
3. [Instalação no macOS](#instalação-no-macos)
4. [Verificação da Instalação](#verificação-da-instalação)
5. [Executando Docker Compose](#executando-docker-compose)
6. [Comandos Úteis](#comandos-úteis)
7. [Troubleshooting](#troubleshooting)

---

## 🪟 Instalação no Windows

### Opção 1: Docker Desktop (Recomendado)

1. **Baixar Docker Desktop:**
   - Acesse: https://www.docker.com/products/docker-desktop/
   - Clique em "Download for Windows"
   - Baixe o instalador `Docker Desktop Installer.exe`

2. **Instalar:**
   - Execute o instalador
   - Marque a opção "Use WSL 2 instead of Hyper-V" (recomendado)
   - Siga o assistente de instalação
   - Reinicie o computador quando solicitado

3. **Iniciar Docker Desktop:**
   - Após reiniciar, inicie o Docker Desktop pelo menu Iniciar
   - Aguarde a inicialização (ícone da baleia na bandeja do sistema)
   - Na primeira execução, aceite os termos de serviço

4. **Verificar instalação:**
   ```powershell
   docker --version
   docker compose version
   ```

### Opção 2: WSL 2 + Docker Engine (Avançado)

Se você já usa WSL 2, pode instalar o Docker Engine diretamente no Linux:

```bash
# Dentro do WSL 2
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
sudo usermod -aG docker $USER
```

---

## 🐧 Instalação no Linux

### Ubuntu/Debian

1. **Atualizar pacotes:**
   ```bash
   sudo apt-get update
   sudo apt-get install -y ca-certificates curl gnupg lsb-release
   ```

2. **Adicionar chave GPG oficial do Docker:**
   ```bash
   sudo mkdir -p /etc/apt/keyrings
   curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
   ```

3. **Configurar repositório:**
   ```bash
   echo \
     "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
     $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
   ```

4. **Instalar Docker Engine e Docker Compose:**
   ```bash
   sudo apt-get update
   sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin
   ```

5. **Adicionar usuário ao grupo docker (para não usar sudo):**
   ```bash
   sudo usermod -aG docker $USER
   ```
   **Importante:** Faça logout e login novamente para aplicar as mudanças.

### CentOS/RHEL/Fedora

1. **Instalar dependências:**
   ```bash
   sudo yum install -y yum-utils
   ```

2. **Adicionar repositório Docker:**
   ```bash
   sudo yum-config-manager --add-repo https://download.docker.com/linux/centos/docker-ce.repo
   ```

3. **Instalar Docker:**
   ```bash
   sudo yum install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin
   ```

4. **Iniciar e habilitar Docker:**
   ```bash
   sudo systemctl start docker
   sudo systemctl enable docker
   ```

5. **Adicionar usuário ao grupo docker:**
   ```bash
   sudo usermod -aG docker $USER
   ```

---

## 🍎 Instalação no macOS

### Opção 1: Docker Desktop (Recomendado)

1. **Baixar Docker Desktop:**
   - Acesse: https://www.docker.com/products/docker-desktop/
   - Clique em "Download for Mac"
   - Escolha a versão para Intel ou Apple Silicon (M1/M2)

2. **Instalar:**
   - Abra o arquivo `.dmg` baixado
   - Arraste o Docker para a pasta Applications
   - Abra o Docker Desktop da pasta Applications
   - Siga o assistente de configuração

3. **Verificar instalação:**
   ```bash
   docker --version
   docker compose version
   ```

### Opção 2: Homebrew

```bash
brew install --cask docker
```

Depois, abra o Docker Desktop da pasta Applications.

---

## ✅ Verificação da Instalação

Após instalar, verifique se tudo está funcionando:

### 1. Verificar versões

```bash
docker --version
# Deve mostrar algo como: Docker version 24.0.0, build abc123

docker compose version
# Deve mostrar algo como: Docker Compose version v2.20.0
```

### 2. Testar Docker

```bash
docker run hello-world
```

Se funcionar, você verá uma mensagem de sucesso do Docker.

### 3. Verificar se o Docker está rodando

**Windows/macOS:**
- Verifique o ícone da baleia na bandeja do sistema
- Deve estar verde/ativo

**Linux:**
```bash
sudo systemctl status docker
```

---

## 🚀 Executando Docker Compose

### Pré-requisitos

1. **Navegar para o diretório do projeto:**
   ```bash
   cd C:\Users\rodrigo\Documents\Projetos\ConnectSolutions\Github\data-flow-service
   ```

2. **Criar a rede Docker (se necessário):**
   ```bash
   docker network create dev_net
   ```

### Passo 1: Subir a Infraestrutura

Execute o arquivo de infraestrutura primeiro:

```bash
docker compose -f docker-compose.infrastructure.yml --profile infra up -d
```

Isso criará:
- PostgreSQL (porta 5432)
- Redis (porta 6379)
- RabbitMQ (portas 5672, 15672)
- Prometheus (porta 9090)
- Grafana (porta 3000)
- Exporters (Redis e PostgreSQL)

### Passo 2: Subir as Aplicações

Depois que a infraestrutura estiver rodando, suba as aplicações:

```bash
docker compose --profile proxy --profile api --profile worker --profile reporting up -d
```

Isso criará:
- DataFlow API
- DataFlow Worker
- DataFlow Reporting Service
- Nginx Proxy

### Subir Tudo de Uma Vez

Você pode executar ambos os comandos em sequência:

```bash
# Infraestrutura
docker compose -f docker-compose.infrastructure.yml --profile infra up -d

# Aplicações
docker compose --profile proxy --profile api --profile worker --profile reporting up -d
```

---

## 📝 Comandos Úteis

### Ver Status dos Containers

```bash
# Todos os containers
docker compose ps

# Apenas infraestrutura
docker compose -f docker-compose.infrastructure.yml ps

# Apenas aplicações
docker compose ps
```

### Ver Logs

```bash
# Logs de todos os serviços
docker compose logs -f

# Logs de um serviço específico
docker compose logs -f data-flow-api

# Logs da infraestrutura
docker compose -f docker-compose.infrastructure.yml logs -f postgres
```

### Parar Serviços

```bash
# Parar aplicações
docker compose --profile proxy --profile api --profile worker --profile reporting down

# Parar infraestrutura
docker compose -f docker-compose.infrastructure.yml --profile infra down

# Parar tudo (incluindo volumes - CUIDADO: apaga dados)
docker compose -f docker-compose.infrastructure.yml --profile infra down -v
docker compose --profile proxy --profile api --profile worker --profile reporting down
```

### Rebuild das Imagens

```bash
# Rebuild das aplicações
docker compose --profile proxy --profile api --profile worker --profile reporting up -d --build

# Rebuild forçado (sem cache)
docker compose --profile proxy --profile api --profile worker --profile reporting build --no-cache
```

### Limpar Tudo

```bash
# Parar e remover containers, redes e volumes
docker compose -f docker-compose.infrastructure.yml --profile infra down -v
docker compose --profile proxy --profile api --profile worker --profile reporting down -v

# Remover imagens não utilizadas
docker image prune -a

# Limpar sistema completo (CUIDADO: remove tudo)
docker system prune -a --volumes
```

### Executar Comandos Dentro de um Container

```bash
# Acessar shell do container
docker compose exec data-flow-api bash

# Executar comando específico
docker compose exec postgres psql -U postgres -d postgres
```

---

## 🔧 Troubleshooting

### Erro: "docker: command not found"

**Solução:**
- Verifique se o Docker está instalado: `docker --version`
- No Windows/macOS, certifique-se de que o Docker Desktop está rodando
- No Linux, verifique se o Docker está no PATH

### Erro: "Cannot connect to the Docker daemon"

**Solução:**

**Windows/macOS:**
- Inicie o Docker Desktop
- Aguarde até o ícone da baleia ficar verde

**Linux:**
```bash
sudo systemctl start docker
sudo systemctl enable docker
```

### Erro: "permission denied while trying to connect to the Docker daemon socket"

**Solução (Linux):**
```bash
sudo usermod -aG docker $USER
# Faça logout e login novamente
```

Ou use `sudo` temporariamente:
```bash
sudo docker compose up -d
```

### Erro: "network dev_net not found"

**Solução:**
```bash
docker network create dev_net
```

### Erro: "port is already allocated"

**Solução:**
Verifique qual processo está usando a porta:
```bash
# Windows
netstat -ano | findstr :5432

# Linux/macOS
lsof -i :5432
```

Pare o processo ou altere a porta no `docker-compose.yml`.

### Erro: "no space left on device"

**Solução:**
Limpe imagens e volumes não utilizados:
```bash
docker system prune -a --volumes
```

### Containers não iniciam ou ficam reiniciando

**Solução:**
1. Verifique os logs:
   ```bash
   docker compose logs nome-do-servico
   ```

2. Verifique se as dependências estão rodando:
   ```bash
   docker compose ps
   ```

3. Verifique se a rede está criada:
   ```bash
   docker network ls
   ```

### Docker Desktop não inicia no Windows

**Solução:**
1. Verifique se o WSL 2 está instalado e atualizado
2. Execute como Administrador
3. Verifique se a virtualização está habilitada no BIOS
4. Reinstale o Docker Desktop

### Problemas de Performance no Windows

**Solução:**
1. Use WSL 2 (não Hyper-V)
2. Aumente os recursos do Docker Desktop:
   - Settings → Resources → Advanced
   - Aumente CPU e Memory

---

## 📚 Recursos Adicionais

- **Documentação oficial do Docker:** https://docs.docker.com/
- **Documentação do Docker Compose:** https://docs.docker.com/compose/
- **Docker Hub:** https://hub.docker.com/
- **Tutoriais:** https://docs.docker.com/get-started/

---

## ✅ Checklist de Instalação

- [ ] Docker instalado (`docker --version`)
- [ ] Docker Compose instalado (`docker compose version`)
- [ ] Docker rodando (ícone verde ou `systemctl status docker`)
- [ ] Teste `docker run hello-world` funcionou
- [ ] Rede `dev_net` criada
- [ ] Infraestrutura rodando
- [ ] Aplicações rodando

---

**Última atualização:** 2024

