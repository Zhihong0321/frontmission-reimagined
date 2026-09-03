@echo off
rem Double-click to start the map asset generator and open it in your browser.
rem Uses the Python that has Pillow installed; installs Pillow if it is missing.
setlocal
cd /d "%~dp0"

set "PY=C:\Users\Eternalgy\AppData\Local\Programs\Python\Python312\python.exe"
if not exist "%PY%" (
  for /f "delims=" %%p in ('where python 2^>nul') do (
    echo %%p | find /i "WindowsApps" >nul || (set "PY=%%p" & goto :found)
  )
)
:found
if not exist "%PY%" (
  echo Could not find a real Python install. Install Python 3 from python.org, then run this again.
  pause
  exit /b 1
)

"%PY%" -c "import PIL" 2>nul || (
  echo Installing Pillow into %PY% ...
  "%PY%" -m pip install --quiet pillow
)

echo Starting the map asset generator with %PY%
echo   generator : http://127.0.0.1:5091
echo   chart     : http://127.0.0.1:5091/chart.html
echo Close this window to stop it.
start "" /b cmd /c "timeout /t 2 >nul & start http://127.0.0.1:5091/"
"%PY%" generator\server.py
pause
