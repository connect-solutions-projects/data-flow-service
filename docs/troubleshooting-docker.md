# Troubleshooting Docker - DataFlow

## Erro: `/usr/bin/env: 'sh\r': No such file or directory`

**Causa:** Script shell com line endings do Windows (CRLF) em vez de Unix (LF).

**Solução:**
1. O arquivo `.gitattributes` foi criado para garantir line endings corretos
2. Rebuild da imagem:
   ```bash
   docker-compose --profile api build --no-cache
   docker-compose --profile api up -d
   ```

**Prevenção:**
- Configure o Git para usar LF automaticamente:
  ```bash
  git config --global core.autocrlf input
  ```
- O arquivo `.gitattributes` já está configurado para scripts `.sh` usarem LF

## Container em loop de restart

**Verificar logs:**
```bash
docker logs -f api
docker logs -f worker
```

**Causas comuns:**
1. SQL Server não está rodando
2. Connection string incorreta
3. Script com line endings errados (ver acima)
4. Dependências não instaladas

## API não conecta ao SQL Server

**Verificar:**
```bash
# 1. SQL Server está rodando?
docker ps | grep sqlserver

# 2. Testar conexão do container
docker exec -it api ping sqlserver

# 3. Verificar connection string
docker exec -it api env | grep ConnectionStrings
```

**Solução:**
- Certifique-se que a infraestrutura está rodando:
  ```bash
  docker-compose -f docker-compose.infrastructure.yml --profile infra up -d
  ```

## Worker não processa batches

**Verificar:**
```bash
# Logs do worker
docker logs -f worker

# Verificar se consegue acessar SQL Server
docker exec -it worker ping sqlserver
```

**Causas:**
1. SQL Server não acessível
2. Nenhum batch pendente
3. Lock já adquirido por outro worker

## Rebuild completo

```bash
# Parar tudo
docker-compose --profile api --profile worker down

# Rebuild sem cache
docker-compose --profile api build --no-cache
docker-compose --profile worker build --no-cache

# Subir novamente
docker-compose --profile api --profile worker up -d
```

## Limpar volumes e recomeçar

```bash
# Parar e remover volumes
docker-compose --profile api --profile worker down -v
docker-compose -f docker-compose.infrastructure.yml --profile infra down -v

# Rebuild e subir
docker-compose -f docker-compose.infrastructure.yml --profile infra up -d
# Aguardar SQL Server...
docker-compose --profile api --profile worker up -d
```

