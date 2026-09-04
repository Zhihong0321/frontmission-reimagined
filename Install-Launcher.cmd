@echo off
REM Double-click this once. It puts a "Mecha Trader" shortcut on your desktop that
REM always rebuilds before it plays, so you can never launch a stale build.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-launcher.ps1" %*
pause
