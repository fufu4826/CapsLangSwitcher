$ErrorActionPreference = 'Stop'

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
Remove-ItemProperty -Path $runKey -Name 'CapsLangSwitcher' -ErrorAction SilentlyContinue

Get-CimInstance Win32_Process |
    Where-Object {
        ($_.CommandLine -like '*CapsLangSwitcher.ps1*' -or $_.CommandLine -like '*CapsLangSwitcher.exe*') -and
        $_.ProcessId -ne $PID
    } |
    ForEach-Object {
        Stop-Process -Id $_.ProcessId -Force
    }

Write-Host 'Uninstalled CapsLangSwitcher autostart and stopped running instance.'
