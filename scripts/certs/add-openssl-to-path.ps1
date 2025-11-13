# Script para adicionar OpenSSL do Git ao PATH do usuário
# Execute: powershell -ExecutionPolicy Bypass -File .\scripts\certs\add-openssl-to-path.ps1

$opensslPath = "C:\Program Files\Git\usr\bin"

# Verifica se o OpenSSL existe
if (-not (Test-Path "$opensslPath\openssl.exe")) {
    Write-Host "ERRO: OpenSSL não encontrado em $opensslPath" -ForegroundColor Red
    Write-Host "Verifique se o Git está instalado e ajuste o caminho se necessário." -ForegroundColor Yellow
    exit 1
}

# Obtém o PATH atual do usuário
$currentPath = [Environment]::GetEnvironmentVariable("Path", "User")

# Verifica se já está no PATH
if ($currentPath -like "*$opensslPath*") {
    Write-Host "OpenSSL já está no PATH do usuário." -ForegroundColor Green
    Write-Host "Caminho: $opensslPath" -ForegroundColor Cyan
    exit 0
}

# Adiciona ao PATH do usuário
try {
    $newPath = $currentPath + ";$opensslPath"
    [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
    
    Write-Host "SUCESSO: OpenSSL adicionado ao PATH do usuário!" -ForegroundColor Green
    Write-Host "Caminho adicionado: $opensslPath" -ForegroundColor Cyan
    Write-Host "`nIMPORTANTE: Feche e reabra o PowerShell para que as mudanças tenham efeito." -ForegroundColor Yellow
    Write-Host "Depois, execute: openssl version" -ForegroundColor Yellow
}
catch {
    Write-Host "ERRO: Não foi possível adicionar ao PATH: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Tente executar o PowerShell como Administrador." -ForegroundColor Yellow
    exit 1
}

