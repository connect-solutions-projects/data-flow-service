@echo off
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0generate-dev-certs.ps1" %*
if errorlevel 1 (
    echo.
    echo ERRO: O script falhou. Verifique as mensagens acima.
    pause
    exit /b 1
)
endlocal
pause
