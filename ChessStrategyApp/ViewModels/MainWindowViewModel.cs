using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChessStrategyApp.Models;
using ChessStrategyApp.Services;

namespace ChessStrategyApp.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly StrategyService _strategyService = new();
    private readonly ProfileService _profileService = new();
    private ChessStrategyStore _database = new();

    [ObservableProperty]
    private string _currentView = "Play";

    [ObservableProperty]
    private GameViewModel _gameBoard = new();

    [ObservableProperty]
    private HistoryViewModel _history = new();

    [ObservableProperty]
    private StrategyViewerViewModel _strategyViewer = new();

    public ProfileViewModel ProfileVm { get; }

    public bool IsPlayViewVisible => CurrentView == "Play";
    public bool IsStrategyViewVisible => CurrentView == "Strategy";
    public bool IsHistoryViewVisible => CurrentView == "History";
    public bool IsProfileViewVisible => CurrentView == "Profile";

    // --- SELEZIONE BOT LADDER DINAMICA ---
    [ObservableProperty]
    private int _selectedLadderElo = 200;

    public ObservableCollection<int> AvailableLadderTiers { get; } = new() { 200 };

    // --- SEZIONE STRATEGY (CATALOGO APERTURE CON ALBERO MOVENODE) ---
    public ObservableCollection<string> StrategyTypes { get; } = new() { "Opening" };
    public ObservableCollection<string> PlayerColors { get; } = new() { "Tutti", "White", "Black" };
    public ObservableCollection<OpeningStrategy> FilteredOpenings { get; } = new();

    private string _selectedType = "Opening";
    private string _selectedColor = "Tutti";

    private OpeningStrategy? _selectedOpening;
    private bool _isEditing = false;

    private string _newOpeningName = string.Empty;
    private string _singleMoveInput = string.Empty;
    private string _newOpeningColor = "White";

    public ObservableCollection<string> NewOpeningMovesChips { get; } = new();

    public string SelectedType
    {
        get => _selectedType;
        set { _selectedType = value; ApplyFilters(); OnPropertyChanged(nameof(SelectedType)); }
    }

    public string SelectedColor
    {
        get => _selectedColor;
        set { _selectedColor = value; ApplyFilters(); OnPropertyChanged(nameof(SelectedColor)); }
    }

    public string NewOpeningName
    {
        get => _newOpeningName;
        set { _newOpeningName = value; OnPropertyChanged(nameof(NewOpeningName)); }
    }

    public string SingleMoveInput
    {
        get => _singleMoveInput;
        set { _singleMoveInput = value; OnPropertyChanged(nameof(SingleMoveInput)); }
    }

    public string NewOpeningColor
    {
        get => _newOpeningColor;
        set
        {
            _newOpeningColor = value;
            OnPropertyChanged(nameof(NewOpeningColor));
            OnPropertyChanged(nameof(NextMoveTurnHelp));
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set { _isEditing = value; OnPropertyChanged(nameof(IsEditing)); }
    }

    public string NextMoveTurnHelp
    {
        get
        {
            if (NewOpeningColor == "White")
            {
                return (NewOpeningMovesChips.Count % 2 == 0) ? "Mossa del BIANCO (Tua)" : "Risposta del NERO";
            }
            else
            {
                return (NewOpeningMovesChips.Count % 2 == 0) ? "Mossa iniziale del BIANCO" : "Tua risposta col NERO";
            }
        }
    }

    public OpeningStrategy? SelectedOpening
    {
        get => _selectedOpening;
        set
        {
            if (SetProperty(ref _selectedOpening, value))
            {
                if (value != null)
                {
                    NewOpeningName = value.Name;
                    NewOpeningColor = value.PlayerColor;
                    IsEditing = true;

                    NewOpeningMovesChips.Clear();
                    PopulateChipsFromTree(value.RootMoves);
                    OnPropertyChanged(nameof(NextMoveTurnHelp));
                }
            }
        }
    }

    public MainWindowViewModel()
    {
        ProfileVm = new ProfileViewModel(_profileService);

        // Notifica match terminato per aggiornamento ELO o sblocco del Tier successivo
        GameBoard.OnBotMatchConcluded += async (soloType, outcome, botElo) =>
        {
            if (soloType == GameViewModel.SoloType.Campaign)
            {
                await ProfileVm.RegisterCampaignResultAsync(outcome, botElo);
            }
            else if (soloType == GameViewModel.SoloType.BotLadder)
            {
                await ProfileVm.RegisterBotLadderResultAsync(outcome, botElo);
                Dispatcher.UIThread.Post(() =>
                {
                    RefreshAvailableTiers();
                    // Preseleziona il nuovo livello sbloccato in caso di vittoria
                    if (outcome == 1.0 && AvailableLadderTiers.Count > 0)
                    {
                        SelectedLadderElo = AvailableLadderTiers.Last();
                    }
                });
            }
        };

        _database = Task.Run(async () => await _strategyService.LoadStrategiesAsync()).Result;
        ApplyFilters();

        _ = InitializeProfileAndLadderAsync();
    }

    private async Task InitializeProfileAndLadderAsync()
    {
        await ProfileVm.InitializeAsync();
        Dispatcher.UIThread.Post(() =>
        {
            if (!string.IsNullOrWhiteSpace(ProfileVm.Profile.Nickname))
            {
                GameBoard.Nickname = ProfileVm.Profile.Nickname;
            }

            // Popola i tier leggendo lo stato salvato nel file JSON
            RefreshAvailableTiers();

            // All'avvio dell'applicazione preseleziona l'ultimo tier attualmente sbloccato
            if (AvailableLadderTiers.Count > 0)
            {
                SelectedLadderElo = AvailableLadderTiers.Last();
            }
        });
    }

    public void RefreshAvailableTiers()
    {
        int maxDefeated = ProfileVm.Profile.HighestBotDefeatedElo;
        int maxUnlockable = Math.Max(200, Math.Min(2000, maxDefeated + 200));

        var validTiers = new List<int>();
        for (int tier = 200; tier <= maxUnlockable; tier += 200)
        {
            validTiers.Add(tier);
        }

        if (!AvailableLadderTiers.SequenceEqual(validTiers))
        {
            AvailableLadderTiers.Clear();
            foreach (var tier in validTiers)
            {
                AvailableLadderTiers.Add(tier);
            }
        }

        // Se il valore selezionato non rientra piu tra quelli ammessi, fallback sul primo valido
        if (!AvailableLadderTiers.Contains(SelectedLadderElo))
        {
            SelectedLadderElo = AvailableLadderTiers.FirstOrDefault();
        }

        OnPropertyChanged(nameof(SelectedLadderElo));
        OnPropertyChanged(nameof(AvailableLadderTiers));
    }

    [RelayCommand]
    private void LaunchCampaign()
    {
        GameBoard.StartCampaignMatch(ProfileVm.Profile.EloSoloCampaign);
    }

    [RelayCommand]
    private void LaunchBotLadder()
    {
        GameBoard.StartBotLadderMatch(SelectedLadderElo);
    }

    [RelayCommand]
    private async Task SwitchView(string viewName)
    {
        if (CurrentView == viewName) return;

        CurrentView = viewName;
        OnPropertyChanged(nameof(IsPlayViewVisible));
        OnPropertyChanged(nameof(IsStrategyViewVisible));
        OnPropertyChanged(nameof(IsHistoryViewVisible));
        OnPropertyChanged(nameof(IsProfileViewVisible));

        if (viewName == "History")
        {
            await History.LoadHistoryAsync();
        }
        else if (viewName == "Profile")
        {
            await ProfileVm.InitializeAsync();
            RefreshAvailableTiers();
        }
        else if (viewName == "Play")
        {
            if (!string.IsNullOrWhiteSpace(ProfileVm.Profile.Nickname))
            {
                GameBoard.Nickname = ProfileVm.Profile.Nickname;
            }
            RefreshAvailableTiers();
        }
    }

    private void ApplyFilters()
    {
        FilteredOpenings.Clear();
        var query = _database.Openings.AsEnumerable()
            .Where(o => o.StrategyType.Equals(SelectedType, StringComparison.OrdinalIgnoreCase));

        if (!SelectedColor.Equals("Tutti", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(o => o.PlayerColor.Equals(SelectedColor, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in query)
        {
            FilteredOpenings.Add(item);
        }
    }

    [RelayCommand]
    private void AddMoveChip()
    {
        if (string.IsNullOrWhiteSpace(SingleMoveInput)) return;

        string cleanMove = SingleMoveInput.Trim().Replace(" ", "");
        if (!string.IsNullOrEmpty(cleanMove))
        {
            NewOpeningMovesChips.Add(cleanMove);
            SingleMoveInput = string.Empty;
            OnPropertyChanged(nameof(NextMoveTurnHelp));
        }
    }

    [RelayCommand]
    private void RemoveLastMoveChip()
    {
        if (NewOpeningMovesChips.Count > 0)
        {
            NewOpeningMovesChips.RemoveAt(NewOpeningMovesChips.Count - 1);
            OnPropertyChanged(nameof(NextMoveTurnHelp));
        }
    }

    [RelayCommand]
    private async Task SaveOpening()
    {
        if (string.IsNullOrWhiteSpace(NewOpeningName) || NewOpeningMovesChips.Count == 0) return;

        List<MoveNode> treeRoot = new();
        MoveNode current = new MoveNode { MoveSan = NewOpeningMovesChips[0] };
        treeRoot.Add(current);

        for (int i = 1; i < NewOpeningMovesChips.Count; i++)
        {
            var nextNode = new MoveNode { MoveSan = NewOpeningMovesChips[i] };
            current.NextMoves.Add(nextNode);
            current = nextNode;
        }

        if (IsEditing && SelectedOpening != null)
        {
            var existing = _database.Openings.FirstOrDefault(o => o.Id == SelectedOpening.Id);
            if (existing != null)
            {
                existing.Name = NewOpeningName;
                existing.PlayerColor = NewOpeningColor;
                existing.RootMoves = treeRoot;
            }
        }
        else
        {
            _database.Openings.Add(new OpeningStrategy
            {
                Name = NewOpeningName,
                PlayerColor = NewOpeningColor,
                StrategyType = "Opening",
                RootMoves = treeRoot
            });
        }

        await _strategyService.SaveStrategiesAsync(_database);
        ResetForm();
        ApplyFilters();
    }

    [RelayCommand]
    private async Task DeleteOpening()
    {
        if (SelectedOpening == null) return;

        var toRemove = _database.Openings.FirstOrDefault(o => o.Id == SelectedOpening.Id);
        if (toRemove != null)
        {
            _database.Openings.Remove(toRemove);
            await _strategyService.SaveStrategiesAsync(_database);
        }

        ResetForm();
        ApplyFilters();
    }

    [RelayCommand]
    private void CancelEdit()
    {
        ResetForm();
    }

    [RelayCommand]
    private async Task DeleteMatch(MatchHistoryItem? match)
    {
        await History.DeleteMatchCommand.ExecuteAsync(match);
    }

    [RelayCommand]
    private void ViewOpening(OpeningStrategy? opening)
    {
        var target = opening ?? SelectedOpening;
        if (target == null) return;

        var moves = new List<string>();
        if (target.RootMoves != null && target.RootMoves.Count > 0)
        {
            var cur = target.RootMoves[0];
            while (cur != null)
            {
                moves.Add(cur.MoveSan);
                cur = cur.NextMoves.Count > 0 ? cur.NextMoves[0] : null!;
            }
        }

        StrategyViewer.LoadOpening(target, moves);
    }

    private void ResetForm()
    {
        _selectedOpening = null;
        OnPropertyChanged(nameof(SelectedOpening));

        NewOpeningName = string.Empty;
        SingleMoveInput = string.Empty;
        NewOpeningMovesChips.Clear();
        IsEditing = false;
        OnPropertyChanged(nameof(NextMoveTurnHelp));
    }

    private void PopulateChipsFromTree(List<MoveNode> nodes)
    {
        if (nodes == null || nodes.Count == 0) return;
        var current = nodes[0];
        while (current != null)
        {
            NewOpeningMovesChips.Add(current.MoveSan);
            current = current.NextMoves.Count > 0 ? current.NextMoves[0] : null!;
        }
    }
}