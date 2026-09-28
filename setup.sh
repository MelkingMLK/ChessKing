#!/bin/bash

set -e

echo "=== [ChessKing] Controllo e Configurazione Ambiente macOS ==="

# 1. Risoluzione dinamica dei percorsi Homebrew su Apple Silicon o Intel
if [[ -f "/opt/homebrew/bin/brew" ]]; then
    eval "$(/opt/homebrew/bin/brew shellenv)"
elif [[ -f "/usr/local/bin/brew" ]]; then
    eval "$(/usr/local/bin/brew shellenv)"
fi

# 2. Controllo / Installazione Homebrew
if ! command -v brew &> /dev/null; then
    echo "⚠️ Homebrew non rilevato. Installazione in corso..."
    /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
    
    # Ricarica l'ambiente brew subito dopo l'installazione
    if [[ -f "/opt/homebrew/bin/brew" ]]; then
        eval "$(/opt/homebrew/bin/brew shellenv)"
    elif [[ -f "/usr/local/bin/brew" ]]; then
        eval "$(/usr/local/bin/brew shellenv)"
    fi
else
    echo "✓ Homebrew presente."
fi

# 3. Assicura che i binari dotnet standard siano nel PATH della sessione corrente
export PATH="$PATH:/usr/local/share/dotnet:~/.dotnet"

# 4. Controllo .NET SDK (accetta .NET 8 o versioni superiori)
DOTNET_OK=false
if command -v dotnet &> /dev/null; then
    CURRENT_VER=$(dotnet --version 2>/dev/null || echo "0")
    MAJOR_VER=$(echo "$CURRENT_VER" | cut -d'.' -f1)
    if [ "$MAJOR_VER" -ge 8 ]; then
        DOTNET_OK=true
        echo "✓ .NET SDK rilevato: v$CURRENT_VER (compatibile)"
    fi
fi

if [ "$DOTNET_OK" = false ]; then
    echo "⚠️ .NET SDK >= 8.0 non trovato. Installazione via Homebrew..."
    brew install --cask dotnet-sdk
    # Re-importa il PATH dopo l'installazione cask
    export PATH="$PATH:/usr/local/share/dotnet:~/.dotnet"
fi

# 5. Individuazione root del repository
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# 6. Ripristino e compilazione di entrambi i progetti (Client e Server Relay)
echo ""
echo "📦 Ripristino pacchetti NuGet..."

if [ -d "$REPO_ROOT/ChessRelayServer" ]; then
    echo "-> ChessRelayServer..."
    dotnet restore "$REPO_ROOT/ChessRelayServer/ChessRelayServer.csproj"
    dotnet build "$REPO_ROOT/ChessRelayServer/ChessRelayServer.csproj" --no-restore
fi

if [ -d "$REPO_ROOT/ChessStrategyApp" ]; then
    echo "-> ChessStrategyApp..."
    dotnet restore "$REPO_ROOT/ChessStrategyApp/ChessStrategyApp.csproj"
    dotnet build "$REPO_ROOT/ChessStrategyApp/ChessStrategyApp.csproj" --no-restore
fi

echo ""
echo "=== Installazione e compilazione completate con successo! ==="
echo ""
echo "Per giocare:"
echo "  - Client Desktop: dotnet run --project ChessStrategyApp"
echo "  - Server Online:  dotnet run --project ChessRelayServer"
echo ""

# Chiedi se vuole avviarlo subito
read -p "Vuoi avviare ChessKing adesso? [S/n]: " -n 1 -r
echo
if [[ $REPLY =~ ^[Ss]$ ]] || [[ -z $REPLY ]]; then
    dotnet run --project "$REPO_ROOT/ChessStrategyApp"
fi