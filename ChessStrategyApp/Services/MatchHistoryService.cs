using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ChessStrategyApp.Models;

namespace ChessStrategyApp.Services;

public class MatchHistoryService
{
    private static readonly string StoragePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "matches.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<List<MatchHistoryItem>> LoadMatchesAsync()
    {
        if (!File.Exists(StoragePath))
            return new List<MatchHistoryItem>();

        try
        {
            using var stream = File.OpenRead(StoragePath);
            var items = await JsonSerializer.DeserializeAsync<List<MatchHistoryItem>>(stream, JsonOptions);
            return items ?? new List<MatchHistoryItem>();
        }
        catch
        {
            return new List<MatchHistoryItem>();
        }
    }

    public async Task SaveMatchesAsync(IEnumerable<MatchHistoryItem> matches)
    {
        using var stream = File.Create(StoragePath);
        await JsonSerializer.SerializeAsync(stream, matches, JsonOptions);
    }

    public async Task AppendMatchAsync(MatchHistoryItem match)
    {
        var current = await LoadMatchesAsync();
        current.Insert(0, match); // Inserisce in cima (dal più recente)
        await SaveMatchesAsync(current);
    }
}