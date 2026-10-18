# Prints an HTML file to PDF with Microsoft Edge (headless).
# Returns the PDF path, or nothing when Edge is not installed.
# Usage: build-docs.ps1 -Html docs\portfolio\guide.html -Pdf artifacts\App-guide.pdf
param(
    [Parameter(Mandatory)] [string]$Html,
    [Parameter(Mandatory)] [string]$Pdf
)
$ErrorActionPreference = "Stop"
$Html = (Resolve-Path $Html).Path
$Pdf = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Pdf)

$edge = @(
    (Join-Path ${env:ProgramFiles(x86)} "Microsoft\Edge\Application\msedge.exe"),
    (Join-Path $env:ProgramFiles "Microsoft\Edge\Application\msedge.exe")
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) {
    Write-Warning "Microsoft Edge was not found, so the PDF was not created."
    return
}

New-Item -ItemType Directory -Force (Split-Path $Pdf) | Out-Null
if (Test-Path $Pdf) { Remove-Item $Pdf -Force }

$profileDir = Join-Path ([IO.Path]::GetTempPath()) "portfolio-docs-edge"
$url = ([Uri]$Html).AbsoluteUri
# Edge writes diagnostics to stderr; keep them from stopping the script.
# It sometimes exits before the PDF appears, so wait for it and try once more.
foreach ($attempt in 1..2) {
    $ErrorActionPreference = "Continue"
    & $edge --headless=new --disable-gpu --no-first-run --no-pdf-header-footer "--user-data-dir=$profileDir" "--print-to-pdf=$Pdf" $url 2>&1 | Out-Null
    $ErrorActionPreference = "Stop"
    $deadline = (Get-Date).AddSeconds(15)
    while (-not (Test-Path $Pdf) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
    if (Test-Path $Pdf) { break }
}

if (-not (Test-Path $Pdf)) { throw "Edge did not create $Pdf" }
Remove-Item $profileDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Created $Pdf"
$Pdf
