@echo off
title ChessKing - Client
cd /d "%~dp0"

where dotnet >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERRORE] .NET 8 SDK non trovato. Installalo da: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo Avvio di ChessKing in corso (ripristino automatico pacchetti)...
dotnet run --project ChessStrategyApp\ChessStrategyApp.csproj
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERRORE] Si e verificato un problema durante l'avvio.
    pause
)