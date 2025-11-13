param(
  [int]$Port = 8080,
  [string]$SiteRoot
)

if (-not $SiteRoot) {
  $projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
  $SiteRoot = Join-Path (Join-Path $projectRoot 'docs') 'site'
}

if (-not (Test-Path -LiteralPath $SiteRoot)) {
  Write-Error "Site root not found: $SiteRoot"
  exit 1
}

$listener = New-Object System.Net.HttpListener
$prefix = "http://localhost:$Port/"
$listener.Prefixes.Add($prefix)
$listener.Start()
Write-Host "Preview server running at $prefix"
Write-Host "Serving: $SiteRoot"

function Get-ContentType($ext) {
  switch ($ext.ToLower()) {
    '.html' { 'text/html; charset=utf-8' }
    '.css'  { 'text/css' }
    '.js'   { 'application/javascript; charset=utf-8' }
    '.json' { 'application/json; charset=utf-8' }
    '.png'  { 'image/png' }
    '.jpg'  { 'image/jpeg' }
    '.jpeg' { 'image/jpeg' }
    '.gif'  { 'image/gif' }
    '.svg'  { 'image/svg+xml' }
    '.ico'  { 'image/x-icon' }
    default { 'application/octet-stream' }
  }
}

try {
  while ($listener.IsListening) {
    $ctx = $listener.GetContext()
    $req = $ctx.Request
    $res = $ctx.Response
    $path = $req.Url.AbsolutePath
    if ($path.EndsWith('/')) { $path = $path + 'index.html' }
    if ($path -eq '/') { $path = '/index.html' }
    $file = Join-Path $SiteRoot ($path.TrimStart('/'))

    # Prevent path traversal
    if (-not $file.StartsWith($SiteRoot)) {
      $res.StatusCode = 403
      $writer = New-Object System.IO.StreamWriter($res.OutputStream)
      $writer.Write('Forbidden')
      $writer.Flush(); $res.Close(); continue
    }

    if (-not (Test-Path -LiteralPath $file)) {
      $res.StatusCode = 404
      $writer = New-Object System.IO.StreamWriter($res.OutputStream)
      $writer.Write('Not Found')
      $writer.Flush(); $res.Close(); continue
    }

    $ext = [System.IO.Path]::GetExtension($file)
    $res.ContentType = Get-ContentType $ext
    $res.ContentEncoding = [System.Text.Encoding]::UTF8
    $stream = [System.IO.File]::OpenRead($file)
    try { $stream.CopyTo($res.OutputStream) } finally { $stream.Close() }
    $res.Close()
  }
} finally {
  $listener.Stop(); $listener.Close()
}
