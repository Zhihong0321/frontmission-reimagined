@echo off
rem Double-click to open Keeper's Chart. If the generator is running, the chart is opened
rem through it (so the generated sprites are served); otherwise the file opens directly.
setlocal
cd /d "%~dp0"
powershell -NoProfile -Command "try { $null = Invoke-WebRequest -UseBasicParsing -TimeoutSec 1 http://127.0.0.1:5091/api/status; exit 0 } catch { exit 1 }" >nul 2>&1
if %errorlevel%==0 (
  start http://127.0.0.1:5091/chart.html
) else (
  start "" "%~dp0chart.html"
)
