# Remove arquivo de um commit específico usando rebase interativo
param(
    [string]$CommitHash = "4a7f7cc",
    [string]$FilePath = "files/large-test-data.csv"
)

Write-Host "Removendo arquivo $FilePath do commit $CommitHash" -ForegroundColor Cyan

# Verifica se estamos no commit correto
$currentCommit = git rev-parse HEAD
$targetCommit = git rev-parse $CommitHash

if ($currentCommit -ne $targetCommit) {
    Write-Host "Fazendo checkout para o commit $CommitHash..." -ForegroundColor Yellow
    git checkout $CommitHash
}

# Remove o arquivo do commit
Write-Host "Removendo arquivo do índice..." -ForegroundColor Cyan
git rm --cached $FilePath 2>&1 | Out-Null

# Faz amend do commit
Write-Host "Atualizando commit..." -ForegroundColor Cyan
git commit --amend --no-edit

# Volta para o branch original
$branch = git branch --show-current
if ($branch) {
    Write-Host "Voltando para o branch $branch..." -ForegroundColor Yellow
    git checkout $branch
}

Write-Host "`nArquivo removido do commit!" -ForegroundColor Green
Write-Host "Execute: git push --force para atualizar o repositório remoto." -ForegroundColor Yellow

