using System;
using System.Collections.Generic;

namespace ChessStrategyApp.Models;

public class MatchHistoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime PlayedAt { get; set; } = DateTime.Now;
    public string WhitePlayer { get; set; } = string.Empty;
    public string BlackPlayer { get; set; } = string.Empty;
    public string WinnerPlayer { get; set; } = string.Empty;
    public string TerminationReason { get; set; } = string.Empty;
    public string WhiteTimeLeft { get; set; } = string.Empty;
    public string BlackTimeLeft { get; set; } = string.Empty;
    public TimeSpan MatchDuration { get; set; }
    public string FinalFen { get; set; } = string.Empty;
    public List<string> MoveHistory { get; set; } = new();

    // NUOVO: Delta punteggio materiale a fine partita
    public string ScoreDifferential { get; set; } = "Parità";

    public string CardTitle => $"{WhitePlayer} vs {BlackPlayer}";
    public string FormattedDate => PlayedAt.ToString("dd/MM/yyyy HH:mm");
    public string ResultSummary => string.IsNullOrEmpty(WinnerPlayer) 
        ? "Patta" 
        : $"Vincitore: {WinnerPlayer} ({TerminationReason}) • Delta: {ScoreDifferential}";
}