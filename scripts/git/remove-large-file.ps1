# Script para remover arquivo grande do histórico do Git
# Uso: powershell -ExecutionPolicy Bypass -File scripts/git/remove-large-file.ps1

param(
    [string]$FilePath = "files/large-test-data.csv"
)

Write-Host "Removendo arquivo do histórico do Git: $FilePath" -ForegroundColor Cyan

# Verifica se o arquivo está no histórico
$commits = git log --all --full-history --oneline -- $FilePath
if ($commits -eq $null -or $commits.Count -eq 0) {
    Write-Host "Arquivo não encontrado no histórico do Git." -ForegroundColor Green
    exit 0
}

Write-Host "`nArquivo encontrado nos seguintes commits:" -ForegroundColor Yellow
$commits | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }

Write-Host "`nUsando git filter-branch para remover do histórico..." -ForegroundColor Cyan

$env:FILTER_BRANCH_SQUELCH_WARNING = "1"
git filter-branch --force --index-filter "git rm --cached --ignore-unmatch $FilePath" --prune-empty --tag-name-filter cat -- --all

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nLimpando referências..." -ForegroundColor Cyan
    git for-each-ref --format="delete %(refname)" refs/original | git update-ref --stdin
    git reflog expire --expire=now --all
    git gc --prune=now --aggressive
    
    Write-Host "`nArquivo removido do histórico com sucesso!" -ForegroundColor Green
    Write-Host "Execute: git push --force para atualizar o repositório remoto." -ForegroundColor Yellow
} else {
    Write-Host "`nERRO: Falha ao remover arquivo do histórico." -ForegroundColor Red
    exit 1
}

