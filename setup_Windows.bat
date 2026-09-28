@echo off
setlocal enabledelayedexpansion

echo ========================================================
echo   ChessKing - Setup Dipendenze e Pacchetti (Windows)
echo ========================================================
echo.

where dotnet >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERRORE] .NET SDK non trovato nel PATH di sistema.
    echo Scarica e installa .NET 8 SDK da: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo [1/3] Pulizia cache di build precedenti...
call dotnet clean ChessStrategyApp/ChessStrategyApp.csproj -c Debug >nul
call dotnet clean ChessRelayServer/ChessRelayServer.csproj -c Debug >nul

echo [2/3] Ripristino pacchetti NuGet per ChessStrategyApp (Client Avalonia)...
call dotnet restore ChessStrategyApp/ChessStrategyApp.csproj
if %ERRORLEVEL% neq 0 (
    echo [ERRORE] Ripristino pacchetti del Client fallito.
    pause
    exit /b 1
)

echo [3/3] Ripristino pacchetti NuGet per ChessRelayServer (SignalR Server)...
call dotnet restore ChessRelayServer/ChessRelayServer.csproj
if %ERRORLEVEL% neq 0 (
    echo [ERRORE] Ripristino pacchetti del Server fallito.
    pause
    exit /b 1
)

echo.
echo ========================================================
echo   Installazione completata con successo!
echo   Ora puoi compilare o lanciare run_client.bat
echo ========================================================
pause