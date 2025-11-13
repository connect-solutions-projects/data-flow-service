@echo off
setlocal enabledelayedexpansion

REM --------- Entrada ---------
if "%~1"=="" (
    set /p "FILE=Informe o caminho completo do arquivo: "
) else (
    set "FILE=%~1"
)

if "%FILE%"=="" (
    echo Caminho nao informado. Encerrando.
    exit /b 1
)

if not exist "%FILE%" (
    echo Arquivo nao encontrado: %FILE%
    exit /b 1
)

set "CLIENT_ID=%~2"
if "%CLIENT_ID%"=="" set "CLIENT_ID=cliente-default"

REM --------- Checksum SHA-256 ---------
for /f "tokens=1" %%h in ('certutil -hashfile "%FILE%" SHA256 ^| find /i /v "hash"') do (
    set "CHECKSUM=%%h"
    goto hash_ok
)
:hash_ok

REM --------- Tamanho em bytes ---------
for %%f in ("%FILE%") do set "FILE_SIZE=%%~zf"

REM --------- Nome recomendado ---------
for %%f in ("%FILE%") do set "FILE_NAME=%%~nxf"

REM --------- fileType padrao (CSV demonstracao) ---------
set "FILE_TYPE=csv"

REM --------- Content-Type basico ---------
set "CONTENT_TYPE=text/csv"
for %%f in ("%FILE%") do (
    set "EXT=%%~xf"
    set "EXT=!EXT:~1!"
    if /i "!EXT!"=="json" set "CONTENT_TYPE=application/json"
    if /i "!EXT!"=="parquet" set "CONTENT_TYPE=application/octet-stream"
)

REM --------- Exibindo resultados ---------
cls
echo === Parametros para /ingestion/jobs ===
echo Arquivo........: %FILE%
echo clientId.......: %CLIENT_ID%
echo fileName.......: %FILE_NAME%
echo fileType.......: %FILE_TYPE%  ^(demonstacao atual suporta apenas CSV^)
echo contentType....: %CONTENT_TYPE%
echo fileSize.......: %FILE_SIZE% bytes
echo checksum.......: %CHECKSUM%
echo.
echo Copie esses valores e utilize na chamada ao endpoint /ingestion/jobs.
pause
endlocal