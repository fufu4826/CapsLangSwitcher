$ErrorActionPreference = 'Stop'

$exePath = Join-Path $PSScriptRoot 'CapsLangSwitcher.exe'
if (-not (Test-Path -LiteralPath $exePath)) {
    & (Join-Path $PSScriptRoot 'BuildExe.ps1')
}

& (Join-Path $PSScriptRoot 'InstallExe.ps1')
