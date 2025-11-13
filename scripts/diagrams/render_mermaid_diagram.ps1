param(
  [string]$Input = "docs/finais/datlo/arquitetura.mmd",
  [string]$OutBase = "docs/finais/datlo/arquitetura",
  [int]$Width = 1600
)

Write-Host "Renderizando Mermaid: $Input" -ForegroundColor Cyan

function Test-Command($name) {
  try {
    $null = & $name --version 2>$null
    return $true
  } catch { return $false }
}

if (-not (Test-Command "node")) { Write-Host "Node.js não encontrado no PATH." -ForegroundColor Yellow }
if (-not (Test-Command "npx")) { Write-Host "NPX não encontrado no PATH." -ForegroundColor Yellow }

try {
  Write-Host "Gerando PNG..." -ForegroundColor Green
  npx @mermaid-js/mermaid-cli -i $Input -o "${OutBase}.png" -w $Width
  Write-Host "Gerando SVG..." -ForegroundColor Green
  npx @mermaid-js/mermaid-cli -i $Input -o "${OutBase}.svg" -w $Width
  Write-Host "Imagens geradas: ${OutBase}.png e ${OutBase}.svg" -ForegroundColor Cyan
} catch {
  Write-Host "Falha ao gerar imagens via mermaid-cli." -ForegroundColor Red
  Write-Host "Dica: instale Node.js e execute: npm install -g @mermaid-js/mermaid-cli" -ForegroundColor Yellow
}
