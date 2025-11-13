@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0\..\.."

REM Verifica se o tamanho foi passado como parâmetro
if "%~1"=="" (
    echo.
    echo ========================================
    echo   Gerador de Arquivo CSV Grande
    echo ========================================
    echo.
    set /p TAMANHO="Digite o tamanho do arquivo em MB (pressione Enter para usar 50 MB como padrao): "
    
    REM Se não digitou nada, usa 50 como padrão
    if "!TAMANHO!"=="" set TAMANHO=50
    
    REM Valida se é um número e maior que zero
    set /a VALIDAR=!TAMANHO! 2>nul
    if errorlevel 1 (
        echo.
        echo ERRO: Tamanho invalido. Use apenas numeros.
        pause
        exit /b 1
    )
    
    set /a TESTE=!TAMANHO! - 1
    if !TESTE! LSS 0 (
        echo.
        echo ERRO: Tamanho deve ser maior que zero.
        pause
        exit /b 1
    )
    
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0gerar-csv-grande.ps1" -TamanhoMB !TAMANHO!
) else (
    REM Tamanho foi passado como parâmetro
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0gerar-csv-grande.ps1" %*
)

if errorlevel 1 (
    echo.
    echo ERRO: O script falhou.
    pause
    exit /b 1
)
pause

