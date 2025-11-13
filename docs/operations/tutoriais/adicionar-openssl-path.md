# Como Adicionar OpenSSL ao PATH do PowerShell

O OpenSSL geralmente vem instalado com o Git for Windows. Este guia mostra como adicioná-lo ao PATH.

## Localização do OpenSSL

Se o Git está instalado em `C:\Program Files\Git`, o OpenSSL geralmente está em:
```
C:\Program Files\Git\usr\bin\openssl.exe
```

## Método 1: Adicionar Temporariamente (Apenas para a Sessão Atual)

Execute no PowerShell:

```powershell
$env:Path += ";C:\Program Files\Git\usr\bin"
```

Para verificar se funcionou:
```powershell
openssl version
```

## Método 2: Adicionar Permanentemente (Recomendado)

### Via Interface Gráfica (GUI)

1. Pressione `Win + R` e digite `sysdm.cpl`, depois Enter
2. Vá na aba **"Avançado"**
3. Clique em **"Variáveis de Ambiente"**
4. Na seção **"Variáveis do sistema"**, encontre a variável `Path` e clique em **"Editar"**
5. Clique em **"Novo"** e adicione:
   ```
   C:\Program Files\Git\usr\bin
   ```
6. Clique em **"OK"** em todas as janelas
7. **Feche e reabra o PowerShell** para que as mudanças tenham efeito

### Via PowerShell (Como Administrador)

Execute o PowerShell **como Administrador** e execute:

```powershell
# Adiciona ao PATH do sistema (permanente)
[Environment]::SetEnvironmentVariable(
    "Path",
    [Environment]::GetEnvironmentVariable("Path", "Machine") + ";C:\Program Files\Git\usr\bin",
    "Machine"
)
```

Depois, **feche e reabra o PowerShell**.

### Via PowerShell (Apenas para o Usuário Atual)

Se não quiser executar como administrador, pode adicionar apenas para seu usuário:

```powershell
# Adiciona ao PATH do usuário (permanente)
[Environment]::SetEnvironmentVariable(
    "Path",
    [Environment]::GetEnvironmentVariable("Path", "User") + ";C:\Program Files\Git\usr\bin",
    "User"
)
```

Depois, **feche e reabra o PowerShell**.

## Verificação

Após adicionar ao PATH, verifique se funcionou:

```powershell
openssl version
```

Você deve ver algo como:
```
OpenSSL 1.1.1w  11 Sep 2023
```

## Alternativa: Usar Git Bash

Se preferir não modificar o PATH, você pode usar o Git Bash diretamente. O script `generate-dev-certs.ps1` já fornece instruções de como fazer isso quando o OpenSSL não está no PATH.

## Nota

Se o Git estiver instalado em outro local, ajuste o caminho conforme necessário. O OpenSSL geralmente está em:
- `[Caminho do Git]\usr\bin\openssl.exe`

