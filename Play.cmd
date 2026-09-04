@echo off
REM Double-click this to play. Builds if needed, starts the server, opens your browser.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0play.ps1"
if errorlevel 1 pause
