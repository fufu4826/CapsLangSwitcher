@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1"
if errorlevel 1 (
  echo.
  echo Install failed.
  pause
  exit /b 1
)
echo.
echo Installed. You can close this window.
pause
