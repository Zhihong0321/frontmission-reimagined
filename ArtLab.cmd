@echo off
REM Double-click this for Art Lab. Builds if needed, starts the server, opens /artlab/.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0play.ps1" -Open "http://localhost:5080/artlab/"
if errorlevel 1 pause
