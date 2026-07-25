$ErrorActionPreference = 'Stop'

$exePath = Join-Path $PSScriptRoot 'CapsLangSwitcher.exe'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

New-Item -Path $runKey -Force | Out-Null
Set-ItemProperty -Path $runKey -Name 'CapsLangSwitcher' -Value ('"' + $exePath + '"')
Start-Process -FilePath $exePath

Write-Host 'Installed CapsLangSwitcher. Caps Lock now changes keyboard language, and the app will start with Windows.'
