using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChessStrategyApp.Models;
using ChessStrategyApp.Services;

namespace ChessStrategyApp.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly MatchHistoryService _historyService = new();

    public ObservableCollection<MatchHistoryItem> Matches { get; } = new();

    [ObservableProperty]
    private MatchHistoryItem? _selectedMatch;

    public HistoryViewModel()
    {
        _ = LoadHistoryAsync();
    }

    // Aggiunto [RelayCommand] per esporre LoadHistoryCommand a XAML
    [RelayCommand]
    public async Task LoadHistoryAsync()
    {
        var items = await _historyService.LoadMatchesAsync();
        Matches.Clear();
        foreach (var item in items)
        {
            Matches.Add(item);
        }
    }

    [RelayCommand]
    private async Task DeleteMatch(MatchHistoryItem? match)
    {
        if (match == null) return;

        Matches.Remove(match);
        if (SelectedMatch == match)
        {
            SelectedMatch = null;
        }

        await _historyService.SaveMatchesAsync(Matches);
    }
}