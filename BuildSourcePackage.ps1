$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$releaseDir = Join-Path $root 'release'
$sourceDir = Join-Path $releaseDir 'CapsLangSwitcher-source'
$zipPath = Join-Path $releaseDir 'CapsLangSwitcher-source.zip'

if (Test-Path -LiteralPath $sourceDir) {
    Remove-Item -LiteralPath $sourceDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $sourceDir | Out-Null

$items = @(
    '.github',
    '.gitignore',
    'BuildExe.ps1',
    'BuildRelease.ps1',
    'BuildSourcePackage.ps1',
    'CapsLangSwitcher.cs',
    'CHANGELOG.md',
    'Install.cmd',
    'Install.ps1',
    'InstallExe.ps1',
    'LICENSE',
    'PUBLISHING.md',
    'README.md',
    'Uninstall.cmd',
    'Uninstall.ps1'
)

foreach ($item in $items) {
    Copy-Item -LiteralPath (Join-Path $root $item) -Destination $sourceDir -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $sourceDir '*') -DestinationPath $zipPath

Write-Host "Built source package: $zipPath"
