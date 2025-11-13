@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0render_mermaid_diagram.ps1" %*
endlocal
