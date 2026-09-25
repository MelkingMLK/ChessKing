using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using ChessStrategyApp.Models;

namespace ChessStrategyApp.Services;

public class StrategyService
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public StrategyService()
    {
        // FIX MAC: Salva nella cartella ApplicationData dell'utente (~/.config/ChessKing/)
        string appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "ChessKing"
        );

        // Assicura che la directory esista, altrimenti la crea
        if (!Directory.Exists(appDataFolder))
        {
            Directory.CreateDirectory(appDataFolder);
        }

        _filePath = Path.Combine(appDataFolder, "strategies.json");
    }

    public async Task<ChessStrategyStore> LoadStrategiesAsync()
    {
        if (!File.Exists(_filePath))
        {
            var defaultStore = CreateDefaultStore();
            await SaveStrategiesAsync(defaultStore);
            return defaultStore;
        }

        try
        {
            string json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<ChessStrategyStore>(json, Options) ?? CreateDefaultStore();
        }
        catch
        {
            return CreateDefaultStore();
        }
    }

    public async Task SaveStrategiesAsync(ChessStrategyStore store)
    {
        string json = JsonSerializer.Serialize(store, Options);
        await File.WriteAllTextAsync(_filePath, json);
    }

    private ChessStrategyStore CreateDefaultStore()
    {
        var store = new ChessStrategyStore();
        
        var treeRoot = new List<MoveNode>
        {
            new MoveNode 
            { 
                MoveSan = "e4", 
                Comment = "Apertura classica.",
                NextMoves = new List<MoveNode>
                {
                    new MoveNode { MoveSan = "e5", Comment = "Risposta principale." }
                }
            }
        };

        store.Openings.Add(new OpeningStrategy
        {
            Name = "Partita di Re",
            PlayerColor = "White",
            StrategyType = "Opening",
            RootMoves = treeRoot
        });
        
        return store;
    }
}