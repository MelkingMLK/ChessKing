using System;

namespace ChessStrategyApp.Models;

public class UserProfile
{
    public string Nickname { get; set; } = "Player_" + Random.Shared.Next(100, 999);

    // --- ELO RATINGS ---
    public int EloOnline { get; set; } = 1000;          // Default Online: 1000
    public int EloSoloCampaign { get; set; } = 200;      // Default Campagna Progressiva: 200

    // --- PROGRESSIONE CAMPAGNA PROGRESSIVA (STILE OSCAR) ---
    public int CurrentWinStreak { get; set; } = 0;
    public int BestWinStreak { get; set; } = 0;
    public int CampaignMatchesPlayed { get; set; } = 0;
    public int CampaignWins { get; set; } = 0;
    public int CampaignLosses { get; set; } = 0;
    public int CampaignDraws { get; set; } = 0;

    // --- BOT LADDER FISSO (TIER 200 - 2000) ---
    public int HighestBotDefeatedElo { get; set; } = 0; // 0 = nessuno ancora sconfitto
    public int BotLadderMatchesPlayed { get; set; } = 0;
    public int BotLadderWins { get; set; } = 0;
    public int BotLadderLosses { get; set; } = 0;
    public int BotLadderDraws { get; set; } = 0;

    // --- ONLINE ROOMS ---
    public int OnlineMatchesPlayed { get; set; } = 0;
    public int OnlineWins { get; set; } = 0;
    public int OnlineLosses { get; set; } = 0;
    public int OnlineDraws { get; set; } = 0;

    // --- METADATI ---
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastPlayedAt { get; set; } = DateTime.Now;

    // --- METRICHE DERIVATE ---
    public double CampaignWinRate => CampaignMatchesPlayed > 0 
        ? Math.Round((double)CampaignWins / CampaignMatchesPlayed * 100, 1) 
        : 0.0;

    public double BotLadderWinRate => BotLadderMatchesPlayed > 0 
        ? Math.Round((double)BotLadderWins / BotLadderMatchesPlayed * 100, 1) 
        : 0.0;

    public double OnlineWinRate => OnlineMatchesPlayed > 0 
        ? Math.Round((double)OnlineWins / OnlineMatchesPlayed * 100, 1) 
        : 0.0;
}