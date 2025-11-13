param(
    [int]$TamanhoMB = 100,
    [string]$OutputPath = "files/large-test-data.csv"
)

# Calcula o tamanho alvo em bytes
$tamanhoAlvoBytes = $TamanhoMB * 1024 * 1024

Write-Host "Gerando arquivo CSV de aproximadamente $TamanhoMB MB..." -ForegroundColor Cyan
Write-Host "Arquivo de saída: $OutputPath" -ForegroundColor Yellow

# Garante que o diretório existe
$dir = Split-Path -Parent $OutputPath
if (-not (Test-Path $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

# Cabeçalho do CSV
$cabecalho = "id,cliente,nome,email,telefone,endereco,cidade,estado,cep,data_cadastro,valor,status,observacoes"
$cabecalhoBytes = [System.Text.Encoding]::UTF8.GetByteCount($cabecalho + "`r`n")

# Abre o arquivo para escrita com buffer maior
$file = [System.IO.StreamWriter]::new($OutputPath, $false, [System.Text.Encoding]::UTF8, 65536)
$file.WriteLine($cabecalho)

$bytesEscritos = $cabecalhoBytes
$linhasGeradas = 0
$random = New-Object System.Random
$buffer = New-Object System.Text.StringBuilder

# Listas para dados aleatórios
$nomes = @("João Silva", "Maria Santos", "Pedro Oliveira", "Ana Costa", "Carlos Souza", "Juliana Lima", "Roberto Alves", "Fernanda Rocha")
$cidades = @("São Paulo", "Rio de Janeiro", "Belo Horizonte", "Curitiba", "Porto Alegre", "Salvador", "Recife", "Fortaleza")
$estados = @("SP", "RJ", "MG", "PR", "RS", "BA", "PE", "CE")
$status = @("ativo", "inativo", "pendente", "cancelado")

Write-Host "Gerando linhas..." -ForegroundColor Green

try {
    while ($bytesEscritos -lt $tamanhoAlvoBytes) {
        $id = $linhasGeradas + 1
        $cliente = "CLI-" + ($random.Next(1000, 9999))
        $nome = $nomes[$random.Next($nomes.Length)]
        $email = "user$id@example.com"
        $telefone = "(" + $random.Next(11, 99) + ") " + $random.Next(90000, 99999) + "-" + $random.Next(1000, 9999)
        $endereco = "Rua " + $nomes[$random.Next($nomes.Length)].Split(' ')[0] + ", " + $random.Next(1, 9999)
        $cidade = $cidades[$random.Next($cidades.Length)]
        $estado = $estados[$random.Next($estados.Length)]
        $cep = $random.Next(10000000, 99999999).ToString()
        $dataCadastro = (Get-Date).AddDays(-$random.Next(0, 365)).ToString("yyyy-MM-dd")
        $valor = [math]::Round($random.NextDouble() * 10000, 2)
        $statusAtual = $status[$random.Next($status.Length)]
        $observacoes = "Observação de teste para linha $id com dados variados para aumentar o tamanho do arquivo."

        $linha = "$id,$cliente,$nome,$email,$telefone,$endereco,$cidade,$estado,$cep,$dataCadastro,$valor,$statusAtual,$observacoes"
        $buffer.AppendLine($linha) | Out-Null
        
        $linhaBytes = [System.Text.Encoding]::UTF8.GetByteCount($linha + "`r`n")
        $bytesEscritos += $linhaBytes
        $linhasGeradas++
        
        # Escreve em blocos de 1000 linhas para melhor performance
        if ($linhasGeradas % 1000 -eq 0) {
            $file.Write($buffer.ToString())
            $buffer.Clear() | Out-Null
        }
        
        # Progresso a cada 50.000 linhas
        if ($linhasGeradas % 50000 -eq 0) {
            $tamanhoAtualMB = [math]::Round($bytesEscritos / 1MB, 2)
            $percentual = [math]::Round(($bytesEscritos / $tamanhoAlvoBytes) * 100, 1)
            Write-Host "  $linhasGeradas linhas geradas (~$tamanhoAtualMB MB - $percentual%)" -ForegroundColor Gray
        }
    }
    
    # Escreve o restante do buffer
    if ($buffer.Length -gt 0) {
        $file.Write($buffer.ToString())
        $buffer.Clear() | Out-Null
    }
}
finally {
    $file.Close()
}

$tamanhoFinalMB = [math]::Round((Get-Item $OutputPath).Length / 1MB, 2)
Write-Host "`nArquivo gerado com sucesso!" -ForegroundColor Green
Write-Host "  Linhas: $linhasGeradas" -ForegroundColor White
Write-Host "  Tamanho: $tamanhoFinalMB MB" -ForegroundColor White
Write-Host "  Localização: $OutputPath" -ForegroundColor White
Write-Host "`nUse o script gera-parametros.bat para obter o checksum e outros parâmetros para upload." -ForegroundColor Yellow

