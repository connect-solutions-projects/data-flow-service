# PowerShell static Markdown → HTML generator for docs/
# Usage: pwsh -File scripts/docs/generate-html-from-md.ps1

param(
  [string]$DocsRoot,
  [string]$OutputRoot,
  [string]$TemplatePath
)

if (-not $DocsRoot -or -not $OutputRoot) {
  $projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
  $DocsRoot = Join-Path $projectRoot 'docs'
  $OutputRoot = Join-Path $DocsRoot 'site'
}

if (-not $TemplatePath) {
  $TemplatePath = Join-Path $DocsRoot 'theme/template.html'
}

function Ensure-Dir($Path) {
  if (-not (Test-Path $Path)) { [System.IO.Directory]::CreateDirectory($Path) | Out-Null }
}

function Escape-Html($s) {
  $s = $s -replace '&','&amp;' -replace '<','&lt;' -replace '>','&gt;'
  return $s
}

function Convert-Inline($s) {
  # Inline code
  $s = [Regex]::Replace($s, '`([^`]+)`', { param($m) "<code>$(Escape-Html($m.Groups[1].Value))</code>" })
  # Images ![alt](src)
  $s = [Regex]::Replace($s, '!\[([^\]]+)\]\(([^\)]+)\)', '<img src="$2" alt="$1" />')
  # Bold **text**
  $s = [Regex]::Replace($s, '\*\*([^*]+)\*\*', '<strong>$1</strong>')
  # Italic *text* (not matching **bold**)
  $s = [Regex]::Replace($s, '(?<!\*)\*([^*]+)\*(?!\*)', '<em>$1</em>')
  # Links [text](url)
  $s = [Regex]::Replace($s, '\[([^\]]+)\]\(([^)]+)\)', '<a href="$2">$1</a>')
  return $s
}

function Convert-MarkdownToHtml($markdown) {
  $markdown = $markdown -replace "\r\n?", "\n"
  $lines = $markdown -split "\n"
  $out = New-Object System.Collections.Generic.List[string]
  $inCode = $false
  $listMode = $null # 'ul' or 'ol'

  function Close-ListIfOpen() {
    if ($listMode) { $out.Add("</$listMode>"); $listMode = $null }
  }

  foreach ($line in $lines) {
    if ($line -match '^```') {
      if (-not $inCode) { Close-ListIfOpen; $inCode = $true; $out.Add('<pre><code>') }
      else { $inCode = $false; $out.Add('</code></pre>') }
      continue
    }
    if ($inCode) { $out.Add((Escape-Html $line)); continue }

    # Headings
    $hMatch = [Regex]::Match($line, '^(#{1,6})\s+(.*)$')
    if ($hMatch.Success) {
      Close-ListIfOpen
      $lvl = $hMatch.Groups[1].Value.Length
      $text = $hMatch.Groups[2].Value
      $out.Add("<h$lvl>$(Convert-Inline $text)</h$lvl>")
      continue
    }

    # Ordered list
    if ($line -match '^\s*\d+\.\s+') {
      $itemText = ($line -replace '^\s*\d+\.\s+', '')
      if ($listMode -ne 'ol') { Close-ListIfOpen; $listMode = 'ol'; $out.Add('<ol>') }
      $out.Add("<li>$(Convert-Inline $itemText)</li>")
      continue
    }

    # Unordered list
    if ($line -match '^\s*[-*+]\s+') {
      $itemText = ($line -replace '^\s*[-*+]\s+', '')
      if ($listMode -ne 'ul') { Close-ListIfOpen; $listMode = 'ul'; $out.Add('<ul>') }
      $out.Add("<li>$(Convert-Inline $itemText)</li>")
      continue
    }

    # Blank line
    if ($line -match '^\s*$') { Close-ListIfOpen; $out.Add(''); continue }

    # Paragraph
    Close-ListIfOpen
    $out.Add("<p>$(Convert-Inline $line)</p>")
  }

  Close-ListIfOpen
  return ($out -join "`n")
}

function Apply-Template($title, $bodyHtml, $navHtml) {
  if (-not (Test-Path -LiteralPath $TemplatePath)) {
    # Fallback simples se o template não existir
    return @(
      '<!DOCTYPE html>',
      '<html lang="pt-BR">',
      '<head>',
      '  <meta charset="utf-8" />',
      '  <meta name="viewport" content="width=device-width, initial-scale=1" />',
      '  <title>' + (Escape-Html $title) + '</title>',
      '  <link rel="stylesheet" href="/css/style.css" />',
      '</head>',
      '<body>',
      '  <nav class="sidebar"><div class="sidebar-header"><h1>DataFlow</h1></div><div class="nav-menu">' + $navHtml + '</div></nav>',
      '  <main class="main-content"><div class="content-page">' + $bodyHtml + '</div></main>',
      '  <script src="/js/main.js"></script>',
      '</body>',
      '</html>'
    ) -join "`n"
  }
  $tpl = Get-Content -LiteralPath $TemplatePath -Raw -Encoding UTF8
  $tpl = $tpl.Replace('{{title}}', (Escape-Html $title))
  $tpl = $tpl.Replace('{{content}}', $bodyHtml)
  $tpl = $tpl.Replace('{{nav}}', $navHtml)
  return $tpl
}

function Get-TitleFromMd($md, $fallback) {
  $m = [Regex]::Match($md, '^#\s+(.*)$', [System.Text.RegularExpressions.RegexOptions]::Multiline)
  if ($m.Success) { return ($m.Groups[1].Value.Trim()) } else { return $fallback }
}

function Relative-ToOutput($file) {
  $abs = (Resolve-Path -LiteralPath $file).Path
  $rel = $abs
  if ($rel.StartsWith($DocsRoot)) {
    $rel = $rel.Substring($DocsRoot.Length)
    if ($rel.StartsWith([IO.Path]::DirectorySeparatorChar)) { $rel = $rel.Substring(1) }
  }
  $rel = ($rel -replace '\.md$', '.html')
  $outPath = Join-Path $OutputRoot $rel
  return $outPath
}

function Copy-PortalAssets() {
  $portalRoot = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'portal'
  $cssSrc = Join-Path $portalRoot 'css/style.css'
  $jsSrc = Join-Path $portalRoot 'js/main.js'
  $imgDir = Join-Path $portalRoot 'images'
  $cssDest = Join-Path $OutputRoot 'css/style.css'
  $jsDest = Join-Path $OutputRoot 'js/main.js'
  Ensure-Dir (Split-Path $cssDest -Parent)
  Ensure-Dir (Split-Path $jsDest -Parent)
  if (Test-Path $cssSrc) { Copy-Item $cssSrc $cssDest -Force }
  if (Test-Path $jsSrc) { Copy-Item $jsSrc $jsDest -Force }
  if (Test-Path $imgDir) {
    Ensure-Dir (Join-Path $OutputRoot 'images')
    Copy-Item (Join-Path $imgDir '*') (Join-Path $OutputRoot 'images') -Force -Recurse
  }
}

function Get-RelHref($outPath) {
  $rel = $outPath
  if ($rel.StartsWith($OutputRoot)) {
    $rel = $rel.Substring($OutputRoot.Length)
    if ($rel.StartsWith([IO.Path]::DirectorySeparatorChar)) { $rel = $rel.Substring(1) }
  }
  return ('/' + ($rel -replace '\\','/'))
}

function Build-NavHtml($pages) {
  $groups = @{}
  foreach ($p in $pages) {
    $href = Get-RelHref $p.output
    $rel = $href.TrimStart('/')
    $group = $rel.Split('/') | Select-Object -First 1
    if (-not $groups.ContainsKey($group)) { $groups[$group] = @() }
    $groups[$group] += @{ title = $p.title; href = $href }
  }
  $groupTitles = @{ 'architecture' = '📐 Arquitetura'; 'operations' = '⚙️ Opera&ccedil;&otilde;es'; 'templates' = '📄 Templates' }
  $sb = New-Object System.Text.StringBuilder
  [void]$sb.Append('<a href="/index.html" class="nav-item"><span class="icon">🏠</span><span>In&iacute;cio</span></a>')
  foreach ($g in $groups.Keys | Sort-Object) {
    $label = $groupTitles[$g]; if (-not $label) { $label = $g }
    [void]$sb.Append('<div class="nav-group">')
    [void]$sb.Append('<div class="nav-group-title">' + (Escape-Html $label) + '</div>')
    foreach ($it in $groups[$g]) {
      [void]$sb.Append('<a href="' + $it.href + '" class="nav-item"><span class="icon">📄</span><span>' + (Escape-Html $it.title) + '</span></a>')
    }
    [void]$sb.Append('</div>')
  }
  return $sb.ToString()
}

function Generate-Index($pages) {
  $groups = @{}
  foreach ($p in $pages) {
    $rel = $p.output
    if ($rel.StartsWith($OutputRoot)) {
      $rel = $rel.Substring($OutputRoot.Length)
      if ($rel.StartsWith([IO.Path]::DirectorySeparatorChar)) { $rel = $rel.Substring(1) }
    }
    $group = $rel.Split([IO.Path]::DirectorySeparatorChar)[0]
    if (-not $groups.ContainsKey($group)) { $groups[$group] = @() }
    $groups[$group] += @{ title = $p.title; href = ($rel -replace '\\','/') }
  }

  $body = @()
  $body += '<h1>Documentação (HTML gerado)</h1>'
  $body += '<p>Conteúdo convertido do Markdown presente em <code>docs/</code>.</p>'
  $body += '<div class="columns">'
  foreach ($kv in $groups.GetEnumerator()) {
    $body += '<div class="card"><h3>' + (Escape-Html $kv.Key) + '</h3><ul>'
    foreach ($it in $kv.Value) { $body += '<li><a href="' + $it.href + '">' + (Escape-Html $it.title) + '</a></li>' }
    $body += '</ul></div>'
  }
  $body += '</div>'
  return Wrap-Html 'Documentação — HTML gerado' ($body -join "")
}

# Main
$sourceRoots = @(
  (Join-Path $DocsRoot 'architecture'),
  (Join-Path $DocsRoot 'operations'),
  (Join-Path $DocsRoot 'templates')
)

Ensure-Dir $OutputRoot
Copy-PortalAssets

$pages = @()
foreach ($root in $sourceRoots) {
  if (-not (Test-Path $root)) { continue }
  $files = Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object { $_.Extension -eq '.md' }
  foreach ($f in $files) {
    $md = Get-Content -LiteralPath $f.FullName -Raw -Encoding UTF8
    $title = Get-TitleFromMd $md $f.Name
    $outPath = Relative-ToOutput $f.FullName
    Ensure-Dir (Split-Path $outPath -Parent)
    $pages += @{ source = $f.FullName; output = $outPath; title = $title }
  }
}

# Renderizar páginas usando o template com navegação
$navHtml = Build-NavHtml $pages
foreach ($p in $pages) {
    $md = Get-Content -LiteralPath $p.source -Raw -Encoding UTF8
  $bodyHtml = Convert-MarkdownToHtml $md
  $wrapped = Apply-Template $p.title $bodyHtml $navHtml
  Set-Content -LiteralPath $p.output -Value $wrapped -Encoding UTF8
  Write-Host "Generated: $($p.output)"
}

function Generate-Index($pages) {
  # Links conhecidos (ajuste conforme nomes dos arquivos no docs/)
  $linkInstalacao = '/operations/guia-instalacao-docker.html'
  $linkCompose    = '/operations/docker-compose-guide.html'
  $linkManual     = '/operations/manual-instalacao-implantacao-testes.html'
  $linkEndpoints  = '/operations/tutoriais/endpoints-dataflow.html'

  $hero = @"
    <div class="hero">
      <h1>Bem-vindo ao DataFlow</h1>
      <p class="subtitle">Plataforma de Ingestão e Processamento Assíncrono de Arquivos Grandes</p>
    </div>
"@

  $quickStart = @"
    <section class="quick-start">
      <h2>🚀 Início Rápido</h2>
      <div class="steps-grid">
        <div class="step-card">
          <div class="step-number">1</div>
          <h3>Instalar Docker</h3>
          <p>Configure Docker e Docker Compose no seu ambiente</p>
          <a href="$linkInstalacao" class="btn-primary">Ver Guia →</a>
        </div>
        <div class="step-card">
          <div class="step-number">2</div>
          <h3>Subir Infraestrutura</h3>
          <p>Execute PostgreSQL, Redis, RabbitMQ e serviços de observabilidade</p>
          <a href="$linkCompose" class="btn-primary">Ver Guia →</a>
        </div>
        <div class="step-card">
          <div class="step-number">3</div>
          <h3>Subir Aplicações</h3>
          <p>Inicie API, Worker e Reporting Service</p>
          <a href="$linkManual" class="btn-primary">Ver Manual →</a>
        </div>
        <div class="step-card">
          <div class="step-number">4</div>
          <h3>Testar Sistema</h3>
          <p>Faça upload de arquivos e verifique o processamento</p>
          <a href="$linkEndpoints" class="btn-primary">Ver Tutorial →</a>
        </div>
      </div>
    </section>
"@

  $features = @"
    <section class="features">
      <h2>✨ Principais Características</h2>
      <div class="features-grid">
        <div class="feature-card">
          <div class="feature-icon">🚀</div>
          <h3>Ingestão Assíncrona</h3>
          <p>Upload de arquivos grandes via HTTP com processamento em background</p>
        </div>
        <div class="feature-card">
          <div class="feature-icon">📊</div>
          <h3>Observabilidade Completa</h3>
          <p>OpenTelemetry, Prometheus e Grafana integrados</p>
        </div>
        <div class="feature-card">
          <div class="feature-icon">🔄</div>
          <h3>Processamento em Fila</h3>
          <p>RabbitMQ para desacoplamento e escalabilidade</p>
        </div>
        <div class="feature-card">
          <div class="feature-icon">📈</div>
          <h3>Relatórios Automáticos</h3>
          <p>Geração de relatórios consolidados com métricas e dashboards</p>
        </div>
        <div class="feature-card">
          <div class="feature-icon">🐳</div>
          <h3>Containerizado</h3>
          <p>Stack completa via Docker Compose</p>
        </div>
        <div class="feature-card">
          <div class="feature-icon">🔒</div>
          <h3>HTTPS</h3>
          <p>Proxy reverso Nginx com certificados SSL</p>
        </div>
      </div>
    </section>
"@

  $arquitetura = @"
    <section id="arquitetura" class="resumo">
      <div class="container">
        <h2 class="section-title">📐 Arquitetura</h2>
        <div class="features-grid">
          <div class="feature-card scroll-reveal">
            <div class="feature-icon"><i class="fas fa-diagram-project"></i></div>
            <h3>Arquitetura Técnica</h3>
            <p>Visão detalhada da arquitetura, componentes e fluxos</p>
            <a href="/architecture/dataflow-technical-architecture.html" class="btn-link">Ler Documento →</a>
          </div>
          <div class="feature-card scroll-reveal">
            <div class="feature-icon"><i class="fas fa-lightbulb"></i></div>
            <h3>Decisões Técnicas</h3>
            <p>Justificativas das escolhas tecnológicas e padrões utilizados</p>
            <a href="/architecture/decisoes-tecnicas.html" class="btn-link">Ler Documento →</a>
          </div>
        </div>
      </div>
    </section>
"@

  $operacoes = @"
    <section id="operacoes" class="features">
      <div class="container">
        <h2 class="section-title">⚙️ Operações</h2>
        <div class="features-grid">
          <div class="feature-card scroll-reveal">
            <div class="feature-icon"><i class="fas fa-download"></i></div>
            <h3>Instalação Docker</h3>
            <p>Guia completo de instalação do Docker e Docker Compose</p>
            <a href="/operations/guia-instalacao-docker.html" class="btn-link">Ler Guia →</a>
          </div>
          <div class="feature-card scroll-reveal">
            <div class="feature-icon"><i class="fas fa-docker"></i></div>
            <h3>Docker Compose</h3>
            <p>Como usar os arquivos docker-compose do projeto</p>
            <a href="/operations/docker-compose-guide.html" class="btn-link">Ler Guia →</a>
          </div>
          <div class="feature-card scroll-reveal">
            <div class="feature-icon"><i class="fas fa-book-open"></i></div>
            <h3>Manual Completo</h3>
            <p>Instalação, implantação e testes fim a fim</p>
            <a href="/operations/manual-instalacao-implantacao-testes.html" class="btn-link">Ler Manual →</a>
          </div>
          <div class="feature-card scroll-reveal">
            <div class="feature-icon"><i class="fas fa-cloud-upload-alt"></i></div>
            <h3>Deploy Docker Hub</h3>
            <p>Build e push das imagens para o Docker Hub</p>
            <a href="/operations/docker-hub-deployment.html" class="btn-link">Ler Guia →</a>
          </div>
          <div class="feature-card scroll-reveal">
            <div class="feature-icon"><i class="fas fa-tags"></i></div>
            <h3>Tag e Push Docker</h3>
            <p>Como fazer tag e push de imagens Docker</p>
            <a href="/operations/tutorial-tag-e-push-docker.html" class="btn-link">Ler Tutorial →</a>
          </div>
        </div>
      </div>
    </section>
"@

  $tutoriais = @"
    <section id="tutoriais" class="projetos">
      <div class="container">
        <h2 class="section-title">📚 Tutoriais</h2>
        <div class="projects-grid">
          <div class="project-card scroll-reveal">
            <div class="project-icon"><i class="fas fa-plug"></i></div>
            <h3>Endpoints da API</h3>
            <p>Uso dos endpoints da API DataFlow para ingestão</p>
            <div class="project-tech"><span class="tech-tag">REST API</span><span class="tech-tag">Swagger</span><span class="tech-tag">.NET 9</span></div>
            <a href="/operations/tutoriais/endpoints-dataflow.html" class="project-link"><i class="fas fa-external-link-alt"></i> Ver Tutorial</a>
          </div>
          <div class="project-card scroll-reveal">
            <div class="project-icon"><i class="fas fa-chart-line"></i></div>
            <h3>Grafana & Prometheus</h3>
            <p>Monitoramento e observabilidade com Grafana e Prometheus</p>
            <div class="project-tech"><span class="tech-tag">Grafana</span><span class="tech-tag">Prometheus</span><span class="tech-tag">Métricas</span></div>
            <a href="/operations/tutoriais/grafana-prometheus.html" class="project-link"><i class="fas fa-external-link-alt"></i> Ver Tutorial</a>
          </div>
          <div class="project-card scroll-reveal">
            <div class="project-icon"><i class="fas fa-terminal"></i></div>
            <h3>Scripts</h3>
            <p>Catálogo e uso dos scripts disponíveis no projeto</p>
            <div class="project-tech"><span class="tech-tag">PowerShell</span><span class="tech-tag">Batch</span><span class="tech-tag">Utilitários</span></div>
            <a href="/operations/tutoriais/scripts-overview.html" class="project-link"><i class="fas fa-external-link-alt"></i> Ver Tutorial</a>
          </div>
          <div class="project-card scroll-reveal">
            <div class="project-icon"><i class="fas fa-server"></i></div>
            <h3>Executar Fora do Docker</h3>
            <p>Executar serviços manualmente fora do Docker</p>
            <div class="project-tech"><span class="tech-tag">.NET 9</span><span class="tech-tag">Local</span><span class="tech-tag">Desenvolvimento</span></div>
            <a href="/operations/tutoriais/run-outside-docker.html" class="project-link"><i class="fas fa-external-link-alt"></i> Ver Tutorial</a>
          </div>
        </div>
      </div>
    </section>
"@

  $quickLinks = @"
    <section class="quick-links">
      <h2>🔗 Links Rápidos</h2>
      <div class="links-grid">
        <a href="https://api.local:8443/swagger" target="_blank" class="link-card">
          <span class="link-icon">📝</span>
          <span class="link-text">Swagger API</span>
        </a>
        <a href="https://reporting.local:8444/swagger" target="_blank" class="link-card">
          <span class="link-icon">📄</span>
          <span class="link-text">Swagger Reporting</span>
        </a>
        <a href="http://localhost:3000" target="_blank" class="link-card">
          <span class="link-icon">📊</span>
          <span class="link-text">Grafana</span>
        </a>
        <a href="http://localhost:9090" target="_blank" class="link-card">
          <span class="link-icon">📈</span>
          <span class="link-text">Prometheus</span>
        </a>
        <a href="http://localhost:15672" target="_blank" class="link-card">
          <span class="link-icon">🔄</span>
          <span class="link-text">RabbitMQ Management</span>
        </a>
      </div>
    </section>
"@

  $content = $hero + $quickStart + $arquitetura + $operacoes + $tutoriais + $quickLinks
  return Apply-Template 'DataFlow — Início' $content (Build-NavHtml $pages)
}

$indexHtml = Generate-Index $pages
Set-Content -LiteralPath (Join-Path $OutputRoot 'index.html') -Value $indexHtml -Encoding UTF8
Write-Host 'Done. Abra docs/site/index.html'
