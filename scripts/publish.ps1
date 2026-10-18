<#
.SYNOPSIS
    Publishes the app into a clean folder with the guide PDF, README.txt, LICENSE.txt and THIRD-PARTY-NOTICES.txt,
    and zips it for distribution.
.DESCRIPTION
    The output folder is recreated on every run so that no personal data (data\) ends up in the distribution.
    Text files are written as UTF-8 with BOM and CRLF so that Notepad shows Japanese correctly.
    This script contains the Japanese PDF name, so it is saved as UTF-8 with BOM: Windows PowerShell 5.1
    reads a .ps1 without BOM as Shift-JIS and would garble the name.
#>
param(
    [string]$Output = "artifacts\Hashelf"
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
$deps = Read-Text (Join-Path $out "Hashelf.deps.json")
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

# The guide keeps an ASCII name in artifacts and gets its Japanese name where readers see it.
$guideName = "ハッシェルフ（Hashelf）のご紹介.pdf"
$guide = & (Join-Path $PSScriptRoot "build-docs.ps1") -Html (Join-Path $root "docs\portfolio\guide.html") `
    -Pdf (Join-Path $root "artifacts\Hashelf-guide.pdf") | Select-Object -Last 1
if (-not $guide) { throw "The guide PDF was not created." }
Copy-Item $guide (Join-Path $out $guideName)
Copy-Item $guide (Join-Path (Split-Path $out) $guideName)

# Entries are added one by one so that paths use "/" (ZipFile.CreateFromDirectory writes "\" under Windows PowerShell)
# and names keep the default encoding, which stores non-ASCII names as UTF-8 with the flag that tells unzip tools so.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zip = "$out.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
$stream = [IO.File]::Open($zip, [IO.FileMode]::CreateNew)
$archive = New-Object IO.Compression.ZipArchive ($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    $top = Split-Path $out -Leaf
    foreach ($file in Get-ChildItem $out -Recurse -File) {
        $name = "$top/" + $file.FullName.Substring($out.Length + 1).Replace("\", "/")
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $name, [IO.Compression.CompressionLevel]::Optimal)
    }
}
finally {
    $archive.Dispose()
    $stream.Dispose()
}

Write-Host "Published to $out"
Write-Host "Zipped to $zip"
Write-Host "Guide for uploading next to the zip: $(Join-Path (Split-Path $out) $guideName)"
