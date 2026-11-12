#!/bin/bash
set -e

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "=== [ChessKing] Verifica Ambiente macOS ==="

if [[ -f "/opt/homebrew/bin/brew" ]]; then
    eval "$(/opt/homebrew/bin/brew shellenv)"
elif [[ -f "/usr/local/bin/brew" ]]; then
    eval "$(/usr/local/bin/brew shellenv)"
fi

export PATH="$PATH:/usr/local/share/dotnet:~/.dotnet"

if ! command -v dotnet &> /dev/null; then
    echo "Installazione .NET SDK in corso..."
    brew install --cask dotnet-sdk
    export PATH="$PATH:/usr/local/share/dotnet:~/.dotnet"
fi

echo "Ripristino dipendenze e avvio ChessKing..."
dotnet run --project "$REPO_ROOT/ChessStrategyApp/ChessStrategyApp.csproj"