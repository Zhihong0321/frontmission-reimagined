@echo off
REM Double-click this to play. Builds if needed, starts the server, opens your browser.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0play.ps1"
if errorlevel 1 pause
