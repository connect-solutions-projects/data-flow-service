# Tutorial: Tag e Push de Imagens Docker para Docker Hub

Este tutorial explica como fazer tag e push das imagens Docker criadas pelo `docker-compose` para o Docker Hub.

## 📋 Pré-requisitos

1. **Conta no Docker Hub**: Certifique-se de ter uma conta em [hub.docker.com](https://hub.docker.com)
2. **Login realizado**: Execute `docker login -u seu-usuario` antes de começar
3. **Imagens já construídas**: As imagens devem ter sido criadas pelo `docker-compose`

## 🔍 Verificar Imagens Existentes

Primeiro, verifique quais imagens foram criadas pelo docker-compose:

```bash
docker images | grep data-flow
```

Você deve ver algo como:
```
data-flow-data-flow-api        latest    abc123def456   2 hours ago   250MB
data-flow-data-flow-worker     latest    def456abc123   2 hours ago   245MB
data-flow-data-flow-reporting  latest    789ghi012jkl   2 hours ago   248MB
```

## 🏷️ Passo 1: Fazer Tag das Imagens

As imagens criadas pelo docker-compose têm nomes no formato `data-flow-data-flow-{servico}:latest`. 
Precisamos criar novas tags com o formato `seu-usuario/data-flow-{servico}:latest` para publicar no Docker Hub.

### Tag da API

```bash
docker tag data-flow-data-flow-api:latest rudrigo1978/data-flow-api:latest
```

### Tag do Reporting

```bash
docker tag data-flow-data-flow-reporting:latest rudrigo1978/data-flow-reporting:latest
```

### Tag do Worker

```bash
docker tag data-flow-data-flow-worker:latest rudrigo1978/data-flow-worker:latest
```

## 📤 Passo 2: Push para Docker Hub

Após criar as tags, faça o push para o Docker Hub:

### Push da API

```bash
docker push rudrigo1978/data-flow-api:latest
```

### Push do Reporting

```bash
docker push rudrigo1978/data-flow-reporting:latest
```

### Push do Worker

```bash
docker push rudrigo1978/data-flow-worker:latest
```

## 🚀 Script Completo (Todos os Comandos)

Execute todos os comandos em sequência:

```bash
# 1. Login no Docker Hub (se ainda não fez)
docker login -u rudrigo1978

# 2. Tags
docker tag data-flow-data-flow-api:latest rudrigo1978/data-flow-api:latest
docker tag data-flow-data-flow-reporting:latest rudrigo1978/data-flow-reporting:latest
docker tag data-flow-data-flow-worker:latest rudrigo1978/data-flow-worker:latest

# 3. Push
docker push rudrigo1978/data-flow-api:latest
docker push rudrigo1978/data-flow-reporting:latest
docker push rudrigo1978/data-flow-worker:latest
```

## 📝 Explicação dos Comandos

### `docker tag`

Cria uma nova tag (referência) para uma imagem existente sem duplicar o conteúdo.

**Formato:**
```bash
docker tag IMAGEM_ORIGEM:tag IMAGEM_DESTINO:tag
```

**Exemplo:**
```bash
docker tag data-flow-data-flow-api:latest rudrigo1978/data-flow-api:latest
```

Isso cria uma nova tag `rudrigo1978/data-flow-api:latest` que aponta para a mesma imagem `data-flow-data-flow-api:latest`.

### `docker push`

Envia a imagem para o Docker Hub (ou outro registry).

**Formato:**
```bash
docker push usuario/imagem:tag
```

**Exemplo:**
```bash
docker push rudrigo1978/data-flow-api:latest
```

## ✅ Verificação

Após o push, verifique se as imagens foram publicadas:

1. Acesse: https://hub.docker.com/r/rudrigo1978/data-flow-api
2. Confirme que a tag `latest` está disponível
3. Repita para as outras imagens:
   - https://hub.docker.com/r/rudrigo1978/data-flow-reporting
   - https://hub.docker.com/r/rudrigo1978/data-flow-worker

## 🔄 Usando Versões Específicas

Se quiser publicar com uma versão específica além de `latest`:

```bash
# Tag com versão
docker tag data-flow-data-flow-api:latest rudrigo1978/data-flow-api:1.0.0
docker tag data-flow-data-flow-reporting:latest rudrigo1978/data-flow-reporting:1.0.0
docker tag data-flow-data-flow-worker:latest rudrigo1978/data-flow-worker:1.0.0

# Push com versão
docker push rudrigo1978/data-flow-api:1.0.0
docker push rudrigo1978/data-flow-reporting:1.0.0
docker push rudrigo1978/data-flow-worker:1.0.0
```

## 🐛 Troubleshooting

### Erro: "unauthorized: authentication required"

**Solução:** Faça login novamente:
```bash
docker login -u rudrigo1978
```

### Erro: "denied: requested access to the resource is denied"

**Solução:** 
- Verifique se o nome de usuário está correto
- Confirme que você tem permissão para publicar no repositório
- Certifique-se de que o repositório existe no Docker Hub (ou será criado automaticamente)

### Erro: "tag does not exist"

**Solução:** Verifique se a imagem original existe:
```bash
docker images | grep data-flow
```

Se não existir, construa as imagens primeiro:
```bash
docker compose --profile api --profile worker --profile reporting build
```

### Imagem não encontrada

Se os nomes das imagens forem diferentes, verifique o nome exato:

```bash
docker images
```

E ajuste os comandos de tag conforme necessário.

## 📚 Comandos Adicionais Úteis

### Listar todas as tags de uma imagem

```bash
docker images rudrigo1978/data-flow-api
```

### Remover uma tag local (não remove do Docker Hub)

```bash
docker rmi rudrigo1978/data-flow-api:latest
```

### Ver histórico de uma imagem

```bash
docker history rudrigo1978/data-flow-api:latest
```

## 🎯 Próximos Passos

Após publicar as imagens, você pode:

1. **Usar as imagens em outros ambientes**:
   ```yaml
   # docker-compose.yml
   services:
     api:
       image: rudrigo1978/data-flow-api:latest
   ```

2. **Compartilhar com a equipe**: Outros desenvolvedores podem fazer pull das imagens

3. **Usar em CI/CD**: Automatizar o deploy usando as imagens do Docker Hub

4. **Versionar releases**: Criar tags para cada versão do software

---

**Nota:** Este tutorial usa `rudrigo1978` como exemplo. Substitua pelo seu nome de usuário do Docker Hub se for diferente.

