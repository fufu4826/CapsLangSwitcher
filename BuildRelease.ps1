$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$releaseDir = Join-Path $root 'release'
$packageDir = Join-Path $releaseDir 'CapsLangSwitcher'
$zipPath = Join-Path $releaseDir 'CapsLangSwitcher.zip'

if (Test-Path -LiteralPath $packageDir) {
    Remove-Item -LiteralPath $packageDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $packageDir | Out-Null

& (Join-Path $root 'BuildExe.ps1')

$files = @(
    'CapsLangSwitcher.exe',
    'Install.cmd',
    'Install.ps1',
    'InstallExe.ps1',
    'Uninstall.cmd',
    'Uninstall.ps1',
    'README.md',
    'LICENSE'
)

foreach ($file in $files) {
    Copy-Item -LiteralPath (Join-Path $root $file) -Destination $packageDir -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $packageDir '*') -DestinationPath $zipPath

Write-Host "Built release package: $zipPath"
