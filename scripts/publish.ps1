<#
.SYNOPSIS
    Publishes the app into a clean folder together with README.txt, LICENSE.txt and THIRD-PARTY-NOTICES.txt.
.DESCRIPTION
    The output folder is recreated on every run so that no personal data (data\) ends up in the distribution.
    Text files are written as UTF-8 with BOM and CRLF so that Notepad shows Japanese correctly.
#>
param(
    [string]$Output = "artifacts\DesktopLauncher"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "src\DesktopLauncher\DesktopLauncher.csproj"
$docs = Join-Path $root "docs\distribution"
$out = Join-Path $root $Output

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish $project -c Release -o $out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

$utf8Bom = New-Object Text.UTF8Encoding $true
function Write-Text([string]$path, [string]$text) {
    [IO.File]::WriteAllText($path, ($text.TrimEnd() -replace "(?<!`r)`n", "`r`n") + "`r`n", $utf8Bom)
}
function Read-Text([string]$path) {
    if (-not (Test-Path $path)) { throw "File not found: $path" }
    [IO.File]::ReadAllText($path).TrimEnd()
}
function Section([string]$title, [string]$body) {
    "`n`n" + ("=" * 60) + "`n" + $title + "`n" + ("=" * 60) + "`n`n" + $body
}

# The runtime packs that were bundled, at the exact versions recorded in deps.json.
$deps = Read-Text (Join-Path $out "DesktopLauncher.deps.json")
$assets = Read-Text (Join-Path $root "src\DesktopLauncher\obj\project.assets.json") | ConvertFrom-Json
$packages = @($assets.packageFolders.PSObject.Properties.Name)[0]
function Get-RuntimePackFile([string]$id, [string]$file) {
    $match = [regex]::Match($deps, "runtimepack\.$([regex]::Escape($id))/([0-9][^`"/]*)")
    if (-not $match.Success) { throw "Runtime pack not found in deps.json: $id" }
    Read-Text (Join-Path $packages "$($id.ToLowerInvariant())\$($match.Groups[1].Value)\$file")
}

Write-Text (Join-Path $out "README.txt") (Read-Text (Join-Path $docs "README.txt"))
Write-Text (Join-Path $out "LICENSE.txt") (Read-Text (Join-Path $docs "LICENSE.txt"))

$notices = Read-Text (Join-Path $docs "third-party-notices.txt")
$notices += Section "MIT License" (Read-Text (Join-Path $docs "licenses\MIT.txt"))
$notices += Section "Appendix: .NET Runtime third-party notices" `
    (Get-RuntimePackFile "Microsoft.NETCore.App.Runtime.win-x64" "THIRD-PARTY-NOTICES.TXT")
$notices += Section "Appendix: WPF third-party notices" (Read-Text (Join-Path $docs "licenses\wpf-THIRD-PARTY-NOTICES.txt"))
$notices += Section "Appendix: Windows Forms third-party notices" (Read-Text (Join-Path $docs "licenses\winforms-THIRD-PARTY-NOTICES.txt"))
Write-Text (Join-Path $out "THIRD-PARTY-NOTICES.txt") $notices

Write-Host "Published to $out"
