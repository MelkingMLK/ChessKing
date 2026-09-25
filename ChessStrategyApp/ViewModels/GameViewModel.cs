using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChessStrategyApp.Models;
using ChessStrategyApp.Services;
using ChessDotNet;

namespace ChessStrategyApp.ViewModels;

public partial class GameViewModel : ObservableObject
{
    private readonly GameEngineService _engine = new();
    private readonly MatchHistoryService _historyService = new();
    private readonly Random _random = new();

    private ChessSquare? _selectedSquare;
    private string _pendingFromSquare = string.Empty;
    private string _pendingToSquare = string.Empty;
    private readonly List<string> _moves = new();
    private DateTime _matchStartTime;
    private bool _matchSaved = false;

    public ObservableCollection<ChessSquare> Squares { get; } = new();

    [ObservableProperty]
    private string _whiteTimerText = "15:00";

    [ObservableProperty]
    private string _blackTimerText = "15:00";

    [ObservableProperty]
    private string _statusMessage = "Inserite i nomi dei giocatori per sorteggiare i colori.";

    // GIOCATORE ATTIVO IN EVIDENZA
    [ObservableProperty]
    private string _activePlayerName = "In attesa";

    [ObservableProperty]
    private string _activePlayerColorTag = "⚪ Bianco";

    [ObservableProperty]
    private bool _isWhiteActive = true;

    // PEZZI CATTURATI E DIFFERENZIALE PUNTEGGIO
    [ObservableProperty]
    private string _whiteCapturedGlyphs = string.Empty;

    [ObservableProperty]
    private string _blackCapturedGlyphs = string.Empty;

    [ObservableProperty]
    private string _whiteScoreDelta = string.Empty;

    [ObservableProperty]
    private string _blackScoreDelta = string.Empty;

    // MODALE PROMOZIONE PEDONE
    [ObservableProperty]
    private bool _isPromotionModalActive = false;

    // SETUP NICKNAME E MODALE SORTEGGIO
    [ObservableProperty]
    private bool _isSetupModalActive = true;

    [ObservableProperty]
    private string _player1Name = "Giocatore 1";

    [ObservableProperty]
    private string _player2Name = "Giocatore 2";

    [ObservableProperty]
    private string _whitePlayerName = string.Empty;

    [ObservableProperty]
    private string _blackPlayerName = string.Empty;

    [ObservableProperty]
    private string _drawResultText = string.Empty;

    [ObservableProperty]
    private bool _canStartMatch = false;

    // POPUP DI VITTORIA
    [ObservableProperty]
    private bool _isVictoryModalActive = false;

    [ObservableProperty]
    private string _victoryTitle = string.Empty;

    [ObservableProperty]
    private string _victoryMessage = string.Empty;

    [ObservableProperty]
    private string _victoryDeltaScore = string.Empty;

    public event Action? RequestNavigateToMenu;

    public GameViewModel()
    {
        _engine.OnTimeTick += () => Dispatcher.UIThread.Post(UpdateTimerDisplay);
        
        _engine.OnTimeOut += (player) => Dispatcher.UIThread.Post(async () =>
        {
            if (_matchSaved) return;
            string winner = (player == Player.White ? BlackPlayerName : WhitePlayerName);
            StatusMessage = $"Tempo scaduto! Vince {winner}!";
            await SaveCurrentMatchAsync(winner, "Tempo Scaduto");
        });

        _engine.OnMoveExecuted += () => Dispatcher.UIThread.Post(RefreshBoardFromEngine);

        InitializeEmptyBoard();
        RefreshBoardFromEngine();
    }

    private void InitializeEmptyBoard()
    {
        Squares.Clear();
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                Squares.Add(new ChessSquare(r, c));
            }
        }
    }

    private void UpdateTimerDisplay()
    {
        WhiteTimerText = _engine.WhiteTime.ToString(@"mm\:ss");
        BlackTimerText = _engine.BlackTime.ToString(@"mm\:ss");
    }

    public void RefreshBoardFromEngine()
    {
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                int chessRank = 8 - r;
                int chessFile = c;

                var piece = _engine.GetPieceAt(chessFile, chessRank);
                int index = r * 8 + c;

                if (piece != null)
                {
                    Squares[index].Piece = piece.GetFenCharacter().ToString().ToUpperInvariant();
                    Squares[index].PieceColor = piece.Owner == Player.White ? "White" : "Black";
                }
                else
                {
                    Squares[index].Piece = string.Empty;
                    Squares[index].PieceColor = string.Empty;
                }
            }
        }

        UpdateTimerDisplay();
        ClearHighlights();

        // Ricalcolo pezzi catturati e bilancio materiale
        var (whiteCaps, blackCaps, advantage) = _engine.GetCapturedPiecesAndScore();
        WhiteCapturedGlyphs = string.Join(" ", whiteCaps);
        BlackCapturedGlyphs = string.Join(" ", blackCaps);

        if (advantage > 0)
        {
            WhiteScoreDelta = $"+{advantage}";
            BlackScoreDelta = string.Empty;
        }
        else if (advantage < 0)
        {
            WhiteScoreDelta = string.Empty;
            BlackScoreDelta = $"+{Math.Abs(advantage)}";
        }
        else
        {
            WhiteScoreDelta = string.Empty;
            BlackScoreDelta = string.Empty;
        }

        // Reset stati animati di controllo Re
        foreach (var sq in Squares)
        {
            sq.IsInCheck = false;
            sq.IsCheckmated = false;
        }

        CheckKingStatus(Player.White);
        CheckKingStatus(Player.Black);

        if (IsSetupModalActive) return;

        IsWhiteActive = _engine.Turn == Player.White;
        ActivePlayerName = IsWhiteActive ? WhitePlayerName : BlackPlayerName;
        ActivePlayerColorTag = IsWhiteActive ? "⚪ Bianco" : "⚫ Nero";

        if (_engine.IsGameOver)
        {
            if (!_matchSaved)
            {
                string winner = _engine.Turn == Player.White ? BlackPlayerName : WhitePlayerName;
                StatusMessage = $"Scacco Matto! Vince {winner}.";
                _ = SaveCurrentMatchAsync(winner, "Scacco Matto");
            }
        }
        else if (_engine.IsPlayerInCheck(_engine.Turn))
        {
            StatusMessage = "Scacco al Re!";
        }
        else
        {
            StatusMessage = "Mossa in corso...";
        }
    }

    private void CheckKingStatus(Player player)
    {
        if (_engine.IsPlayerCheckmated(player))
        {
            var pos = _engine.FindKingPosition(player);
            if (pos != null) SetSquareKingState(pos, isCheckmated: true);
        }
        else if (_engine.IsPlayerInCheck(player))
        {
            var pos = _engine.FindKingPosition(player);
            if (pos != null) SetSquareKingState(pos, isInCheck: true);
        }
    }

    private void SetSquareKingState(Position pos, bool isInCheck = false, bool isCheckmated = false)
    {
        int col = (int)pos.File;
        int row = 8 - pos.Rank;
        int idx = row * 8 + col;
        if (idx >= 0 && idx < Squares.Count)
        {
            Squares[idx].IsInCheck = isInCheck;
            Squares[idx].IsCheckmated = isCheckmated;
        }
    }

    private void ClearHighlights()
    {
        foreach (var sq in Squares)
        {
            sq.IsTargetMove = false;
            sq.IsSelected = false;
        }
    }

    [RelayCommand]
    private void SquareClicked(ChessSquare square)
    {
        if (IsSetupModalActive || _engine.IsGameOver || IsPromotionModalActive || IsVictoryModalActive) return;

        if (_selectedSquare == null)
        {
            if (!string.IsNullOrEmpty(square.Piece))
            {
                string requiredColor = _engine.Turn == Player.White ? "White" : "Black";
                if (square.PieceColor == requiredColor)
                {
                    _selectedSquare = square;
                    square.IsSelected = true;
                    HighlightLegalMoves(GetCoord(square));
                }
            }
        }
        else
        {
            if (_selectedSquare == square)
            {
                _selectedSquare = null;
                ClearHighlights();
                return;
            }

            string requiredColor = _engine.Turn == Player.White ? "White" : "Black";
            if (!string.IsNullOrEmpty(square.Piece) && square.PieceColor == requiredColor)
            {
                ClearHighlights();
                _selectedSquare = square;
                square.IsSelected = true;
                HighlightLegalMoves(GetCoord(square));
                return;
            }

            string from = GetCoord(_selectedSquare);
            string to = GetCoord(square);

            if (_engine.IsPawnPromotion(from, to))
            {
                _pendingFromSquare = from;
                _pendingToSquare = to;
                IsPromotionModalActive = true;
                ClearHighlights();
                _selectedSquare = null;
                return;
            }

            bool success = _engine.TryMove(from, to);
            if (success)
            {
                _moves.Add($"{from}-{to}");
            }

            _selectedSquare = null;
            ClearHighlights();
        }
    }

    private void HighlightLegalMoves(string fromCoord)
    {
        ClearHighlights();
        if (_selectedSquare != null) _selectedSquare.IsSelected = true;

        var destinations = _engine.GetLegalDestinations(fromCoord);
        foreach (var pos in destinations)
        {
            int col = (int)pos.File;
            int row = 8 - pos.Rank;
            int idx = row * 8 + col;
            if (idx >= 0 && idx < Squares.Count)
            {
                Squares[idx].IsTargetMove = true;
            }
        }
    }

    [RelayCommand]
    private void SelectPromotion(string pieceCode)
    {
        char promo = pieceCode.ToUpperInvariant()[0];
        string toSquare = _pendingToSquare;
        bool success = _engine.TryMove(_pendingFromSquare, toSquare, promo);
        if (success)
        {
            _moves.Add($"{_pendingFromSquare}-{toSquare}={promo}");

            if (toSquare.Length == 2)
            {
                int col = toSquare[0] - 'a';
                int row = 8 - (toSquare[1] - '0');
                int idx = row * 8 + col;
                if (idx >= 0 && idx < Squares.Count)
                {
                    Squares[idx].IsPromoted = true;
                }
            }
        }

        IsPromotionModalActive = false;
        _pendingFromSquare = string.Empty;
        _pendingToSquare = string.Empty;
    }

    private string GetCoord(ChessSquare sq)
    {
        char file = (char)('a' + sq.Column);
        int rank = 8 - sq.Row;
        return $"{file}{rank}";
    }

    [RelayCommand]
    private void DrawColors()
    {
        if (string.IsNullOrWhiteSpace(Player1Name) || string.IsNullOrWhiteSpace(Player2Name))
        {
            DrawResultText = "Inserire entrambi i nickname prima del sorteggio.";
            return;
        }

        if (Player1Name.Trim().Equals(Player2Name.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            DrawResultText = "I due nickname devono essere distinti.";
            return;
        }

        bool p1IsWhite = _random.Next(2) == 0;

        WhitePlayerName = p1IsWhite ? Player1Name.Trim() : Player2Name.Trim();
        BlackPlayerName = p1IsWhite ? Player2Name.Trim() : Player1Name.Trim();

        DrawResultText = $"Sorteggio completato:\n⚪ Bianco: {WhitePlayerName}\n⚫ Nero: {BlackPlayerName}";
        CanStartMatch = true;
    }

    [RelayCommand]
    private void StartMatch()
    {
        IsSetupModalActive = false;
        _matchStartTime = DateTime.Now;
        _moves.Clear();
        _matchSaved = false;
        RefreshBoardFromEngine();
    }

    [RelayCommand]
    private void ResetGame()
    {
        _engine.ResetGame();
        _selectedSquare = null;
        _moves.Clear();
        _matchSaved = false;
        CanStartMatch = false;
        DrawResultText = string.Empty;
        IsSetupModalActive = true;
        IsVictoryModalActive = false;
        ActivePlayerName = "In attesa";
        RefreshBoardFromEngine();
    }

    [RelayCommand]
    private void ReturnToMenuAfterVictory()
    {
        ResetGame();
        RequestNavigateToMenu?.Invoke();
    }

    private async Task SaveCurrentMatchAsync(string winner, string reason)
    {
        _matchSaved = true;

        var (_, _, advantage) = _engine.GetCapturedPiecesAndScore();
        string deltaString = advantage == 0 
            ? "Parità materiale" 
            : (advantage > 0 ? $"+{advantage} per il Bianco" : $"+{Math.Abs(advantage)} per il Nero");

        VictoryTitle = $"HA VINTO {winner.ToUpperInvariant()}!";
        VictoryMessage = $"Esito: {reason}";
        VictoryDeltaScore = $"Differenza Materiale: {deltaString}";
        IsVictoryModalActive = true;

        var item = new MatchHistoryItem
        {
            PlayedAt = DateTime.Now,
            WhitePlayer = WhitePlayerName,
            BlackPlayer = BlackPlayerName,
            WinnerPlayer = winner,
            TerminationReason = reason,
            WhiteTimeLeft = WhiteTimerText,
            BlackTimeLeft = BlackTimerText,
            MatchDuration = DateTime.Now - _matchStartTime,
            FinalFen = string.Empty,
            ScoreDifferential = deltaString,
            MoveHistory = new List<string>(_moves)
        };

        await _historyService.AppendMatchAsync(item);
    }
}