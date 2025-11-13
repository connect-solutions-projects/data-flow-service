param(
    [string]$ApiDomain = "api.local",
    [string]$ReportingDomain = "reporting.local",
    [int]$ValidityDays = 365
)

function Test-OpenSSL {
    $openssl = Get-Command openssl -ErrorAction SilentlyContinue
    return $null -ne $openssl
}

function New-DevCertificate-OpenSSL {
    param(
        [string]$DnsName,
        [string]$OutputFolder,
        [int]$ValidityDays
    )

    Write-Host ">> Gerando certificado para $DnsName usando OpenSSL" -ForegroundColor Cyan

    if (-not (Test-Path $OutputFolder)) {
        New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null
    }

    $keyPath = Join-Path $OutputFolder "privkey.pem"
    $certPath = Join-Path $OutputFolder "fullchain.pem"
    $configPath = Join-Path $OutputFolder "temp.conf"

    # Cria arquivo de configuração temporário para OpenSSL
    $configContent = @"
[req]
distinguished_name = req_distinguished_name
req_extensions = v3_req
prompt = no

[req_distinguished_name]
CN = $DnsName

[v3_req]
keyUsage = keyEncipherment, dataEncipherment
extendedKeyUsage = serverAuth
subjectAltName = @alt_names

[alt_names]
DNS.1 = $DnsName
"@
    $configContent | Set-Content -Path $configPath -Encoding ASCII

    try {
        # Gera chave privada e certificado em um único comando
        # Usa Start-Process para evitar capturar a saída de progresso do OpenSSL
        $process = Start-Process -FilePath "openssl" `
            -ArgumentList @(
                "req", "-x509", "-nodes",
                "-days", $ValidityDays,
                "-newkey", "rsa:2048",
                "-keyout", $keyPath,
                "-out", $certPath,
                "-config", $configPath
            ) `
            -NoNewWindow `
            -Wait `
            -PassThru `
            -RedirectStandardOutput "$env:TEMP\openssl_stdout_$PID.txt" `
            -RedirectStandardError "$env:TEMP\openssl_stderr_$PID.txt"
        
        # Limpa arquivos temporários de saída
        Remove-Item "$env:TEMP\openssl_stdout_$PID.txt" -Force -ErrorAction SilentlyContinue
        Remove-Item "$env:TEMP\openssl_stderr_$PID.txt" -Force -ErrorAction SilentlyContinue
        
        if ($process.ExitCode -ne 0) {
            throw "Falha ao gerar certificado. Exit code: $($process.ExitCode)"
        }

        # Verifica se os arquivos foram criados
        if (-not (Test-Path $keyPath)) {
            throw "Chave privada não foi criada: $keyPath"
        }
        if (-not (Test-Path $certPath)) {
            throw "Certificado não foi criado: $certPath"
        }

        # Remove arquivo temporário
        Remove-Item $configPath -Force -ErrorAction SilentlyContinue

        Write-Host "   Certificados salvos em $OutputFolder" -ForegroundColor Green
    }
    catch {
        # Limpa arquivos em caso de erro
        Remove-Item $keyPath -Force -ErrorAction SilentlyContinue
        Remove-Item $certPath -Force -ErrorAction SilentlyContinue
        Remove-Item $configPath -Force -ErrorAction SilentlyContinue
        throw
    }
}

# Tratamento de erro global
$ErrorActionPreference = "Stop"

try {
    # Verifica se OpenSSL está disponível
    if (-not (Test-OpenSSL)) {
        Write-Host "`nERRO: OpenSSL não encontrado no PATH." -ForegroundColor Red
        Write-Host "`nOpções:" -ForegroundColor Yellow
        Write-Host "1. Instale o OpenSSL e adicione ao PATH" -ForegroundColor White
        Write-Host "2. Use o Git Bash (que inclui OpenSSL) e execute os seguintes comandos:" -ForegroundColor White
        Write-Host "`n   cd '$(Split-Path -Parent (Split-Path -Parent $PSScriptRoot))'" -ForegroundColor Cyan
        Write-Host "   MSYS2_ARG_CONV_EXCL='*' openssl req -x509 -nodes -days 365 -newkey rsa:2048 \" -ForegroundColor Cyan
        Write-Host "     -keyout certs/api/privkey.pem -out certs/api/fullchain.pem \" -ForegroundColor Cyan
        Write-Host "     -subj '/CN=api.local' -addext 'subjectAltName=DNS:api.local'" -ForegroundColor Cyan
        Write-Host "   MSYS2_ARG_CONV_EXCL='*' openssl req -x509 -nodes -days 365 -newkey rsa:2048 \" -ForegroundColor Cyan
        Write-Host "     -keyout certs/reporting/privkey.pem -out certs/reporting/fullchain.pem \" -ForegroundColor Cyan
        Write-Host "     -subj '/CN=reporting.local' -addext 'subjectAltName=DNS:reporting.local'" -ForegroundColor Cyan
        exit 1
    }

    # Calcula o caminho da raiz do projeto (subindo dois níveis de scripts/certs/)
    $projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    
    if (-not (Test-Path $projectRoot)) {
        throw "Não foi possível encontrar a raiz do projeto. Caminho esperado: $projectRoot"
    }

    $targets = @(
        @{ Dns = $ApiDomain; Folder = (Join-Path $projectRoot "certs\api") },
        @{ Dns = $ReportingDomain; Folder = (Join-Path $projectRoot "certs\reporting") }
    )

    foreach ($target in $targets) {
        New-DevCertificate-OpenSSL -DnsName $target.Dns -OutputFolder $target.Folder -ValidityDays $ValidityDays
    }

    Write-Host "`nPronto! Certificados gerados com sucesso." -ForegroundColor Green
    Write-Host "Atualize o arquivo nginx/conf.d/dataflow.conf com os domínios utilizados" -ForegroundColor Yellow
    Write-Host "e adicione os hostnames ao arquivo hosts (ex.: 127.0.0.1 api.local)." -ForegroundColor Yellow
}
catch {
    Write-Host "`nERRO: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Detalhes: $($_.Exception.GetType().FullName)" -ForegroundColor Red
    if ($_.ScriptStackTrace) {
        Write-Host "Stack: $($_.ScriptStackTrace)" -ForegroundColor Red
    }
    exit 1
}

