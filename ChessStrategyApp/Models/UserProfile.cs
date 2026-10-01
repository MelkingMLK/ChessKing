using System;

namespace ChessStrategyApp.Models;

public class UserProfile
{
    public string Nickname { get; set; } = "Player_" + Random.Shared.Next(100, 999);

    // ELO Ratings
    public int EloOnline { get; set; } = 1000;
    public int EloSoloCampaign { get; set; } = 200;

    // Statistiche Campagna
    public int CampaignMatchesPlayed { get; set; } = 0;
    public int CampaignWins { get; set; } = 0;
    public int CampaignLosses { get; set; } = 0;
    public int CampaignDraws { get; set; } = 0;

    // Statistiche Sfida Bot (Tier Fisso)
    // 0 = Nessun bot ancora sconfitto
    public int HighestBotDefeatedElo { get; set; } = 0;
    public int BotLadderMatchesPlayed { get; set; } = 0;
    public int BotLadderWins { get; set; } = 0;
    public int BotLadderLosses { get; set; } = 0;
    public int BotLadderDraws { get; set; } = 0;

    // Statistiche Online
    public int OnlineMatchesPlayed { get; set; } = 0;
    public int OnlineWins { get; set; } = 0;
    public int OnlineLosses { get; set; } = 0;
    public int OnlineDraws { get; set; } = 0;

    // Metadati
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastPlayedAt { get; set; } = DateTime.Now;

    // Proprietà calcolate per la UI
    public string HighestBotDefeatedDisplay => HighestBotDefeatedElo > 0 
        ? $"ELO {HighestBotDefeatedElo}" 
        : "Nessuno";

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