using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChessStrategyApp.Models;
using ChessStrategyApp.Services;

namespace ChessStrategyApp.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly ProfileService _profileService;

    [ObservableProperty]
    private UserProfile _profile = new();

    [ObservableProperty]
    private string _editableNickname = string.Empty;

    [ObservableProperty]
    private string _saveFeedback = string.Empty;

    public ProfileViewModel(ProfileService profileService)
    {
        _profileService = profileService;
    }

    public async Task InitializeAsync()
    {
        Profile = await _profileService.LoadProfileAsync();
        EditableNickname = Profile.Nickname;
        SaveFeedback = string.Empty;
    }

    [RelayCommand]
    private async Task SaveNickname()
    {
        if (string.IsNullOrWhiteSpace(EditableNickname))
        {
            SaveFeedback = "Il nickname non può essere vuoto.";
            return;
        }

        Profile.Nickname = EditableNickname.Trim();
        await _profileService.SaveProfileAsync(Profile);
        OnPropertyChanged(nameof(Profile));
        SaveFeedback = "Nickname aggiornato con successo!";
    }

    // 1. Partita Campagna Progressiva (Stile Oscar)
    public async Task RegisterCampaignResultAsync(double outcome, int botElo)
    {
        Profile.LastPlayedAt = DateTime.Now;
        Profile.CampaignMatchesPlayed++;

        if (outcome == 1.0)
        {
            Profile.CampaignWins++;
            Profile.CurrentWinStreak++;
            if (Profile.CurrentWinStreak > Profile.BestWinStreak)
            {
                Profile.BestWinStreak = Profile.CurrentWinStreak;
            }
        }
        else if (outcome == 0.5)
        {
            Profile.CampaignDraws++;
            Profile.CurrentWinStreak = 0;
        }
        else
        {
            Profile.CampaignLosses++;
            Profile.CurrentWinStreak = 0;
        }

        Profile.EloSoloCampaign = ProfileService.CalculateNewElo(
            Profile.EloSoloCampaign, 
            botElo, 
            outcome, 
            Profile.CurrentWinStreak
        );

        await _profileService.SaveProfileAsync(Profile);
        OnPropertyChanged(nameof(Profile));
    }

    // 2. Partita Bot Ladder a Livello Fisso
public async Task RegisterBotLadderResultAsync(double outcome, int botElo)
    {
        Profile.LastPlayedAt = DateTime.Now;
        Profile.BotLadderMatchesPlayed++;

        if (outcome == 1.0)
        {
            Profile.BotLadderWins++;
            // Se batti l'elo 200, il massimo sconfitto deve diventare almeno 200
            if (botElo > Profile.HighestBotDefeatedElo)
            {
                Profile.HighestBotDefeatedElo = botElo;
            }
        }
        else if (outcome == 0.5)
        {
            Profile.BotLadderDraws++;
        }
        else
        {
            Profile.BotLadderLosses++;
        }

        await _profileService.SaveProfileAsync(Profile);
        var refreshed = Profile;
        Profile = null!;
        Profile = refreshed;
        OnPropertyChanged(nameof(Profile));
    }
    // 3. Partita Online SignalR
    public async Task RegisterOnlineResultAsync(double outcome, int opponentElo)
    {
        Profile.LastPlayedAt = DateTime.Now;
        Profile.OnlineMatchesPlayed++;

        if (outcome == 1.0) Profile.OnlineWins++;
        else if (outcome == 0.5) Profile.OnlineDraws++;
        else Profile.OnlineLosses++;

        Profile.EloOnline = ProfileService.CalculateNewElo(Profile.EloOnline, opponentElo, outcome);

        await _profileService.SaveProfileAsync(Profile);
        OnPropertyChanged(nameof(Profile));
    }
}