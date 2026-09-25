using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChessStrategyApp.Models;
using ChessStrategyApp.Services;

namespace ChessStrategyApp.ViewModels;

public partial class StrategyViewerViewModel : ObservableObject
{
    private List<string> _currentMoves = new();

    public ObservableCollection<ChessSquare> ViewerSquares { get; } = new();

    [ObservableProperty]
    private string _openingTitle = string.Empty;

    [ObservableProperty]
    private string _playerPerspective = string.Empty;

    [ObservableProperty]
    private int _currentStep = 0;

    [ObservableProperty]
    private int _totalSteps = 0;

    [ObservableProperty]
    private string _currentMoveText = "Posizione Iniziale";

    [ObservableProperty]
    private bool _isViewerOpen = false;

    public void LoadOpening(OpeningStrategy opening, List<string> flatMoves)
    {
        OpeningTitle = opening.Name;
        PlayerPerspective = opening.PlayerColor;
        _currentMoves = new List<string>(flatMoves);
        TotalSteps = _currentMoves.Count;
        CurrentStep = 0;
        IsViewerOpen = true;

        UpdateBoard();
    }

    private void UpdateBoard()
    {
        var updatedSquares = BoardReplayHelper.GenerateBoardAtStep(_currentMoves, CurrentStep);
        ViewerSquares.Clear();
        foreach (var sq in updatedSquares)
        {
            ViewerSquares.Add(sq);
        }

        if (CurrentStep == 0)
        {
            CurrentMoveText = "Posizione Iniziale";
        }
        else
        {
            string lastMove = _currentMoves[CurrentStep - 1];
            string who = (CurrentStep % 2 != 0) ? "Bianco" : "Nero";
            CurrentMoveText = $"Mossa {CurrentStep} di {TotalSteps}: {lastMove} ({who})";
        }
    }
    private void ApplyMoveToViewerBoard(string moveText, bool isWhiteTurn)
    {
        string normalized = moveText.Trim().ToUpperInvariant().Replace('0', 'O');

        if (normalized == "O-O") // Arrocco Corto
        {
            if (isWhiteTurn)
            {
                // Re: e1 (r=7, c=4) -> g1 (r=7, c=6)
                // Torre: h1 (r=7, c=7) -> f1 (r=7, c=5)
                MoveViewerPiece(7, 4, 7, 6);
                MoveViewerPiece(7, 7, 7, 5);
            }
            else
            {
                // Re: e8 (r=0, c=4) -> g8 (r=0, c=6)
                // Torre: h8 (r=0, c=7) -> f8 (r=0, c=5)
                MoveViewerPiece(0, 4, 0, 6);
                MoveViewerPiece(0, 7, 0, 5);
            }
            return;
        }

        if (normalized == "O-O-O") // Arrocco Lungo
        {
            if (isWhiteTurn)
            {
                // Re: e1 (r=7, c=4) -> c1 (r=7, c=2)
                // Torre: a1 (r=7, c=0) -> d1 (r=7, c=3)
                MoveViewerPiece(7, 4, 7, 2);
                MoveViewerPiece(7, 0, 7, 3);
            }
            else
            {
                // Re: e8 (r=0, c=4) -> c8 (r=0, c=2)
                // Torre: a8 (r=0, c=0) -> d8 (r=0, c=3)
                MoveViewerPiece(0, 4, 0, 2);
                MoveViewerPiece(0, 0, 0, 3);
            }
            return;
        }

        // Mossa standard tramite coordinate o motore...
    }

    private void MoveViewerPiece(int fromRow, int fromCol, int toRow, int toCol)
    {
        int fromIdx = fromRow * 8 + fromCol;
        int toIdx = toRow * 8 + toCol;

        if (fromIdx >= 0 && fromIdx < ViewerSquares.Count && toIdx >= 0 && toIdx < ViewerSquares.Count)
        {
            ViewerSquares[toIdx].Piece = ViewerSquares[fromIdx].Piece;
            ViewerSquares[toIdx].PieceColor = ViewerSquares[fromIdx].PieceColor;
            ViewerSquares[fromIdx].Piece = string.Empty;
            ViewerSquares[fromIdx].PieceColor = string.Empty;
        }
    }
    
    [RelayCommand]
    private void FirstMove()
    {
        if (CurrentStep <= 0) return;
        CurrentStep = 0;
        UpdateBoard();
    }

    [RelayCommand]
    private void PreviousMove()
    {
        if (CurrentStep <= 0) return;
        CurrentStep--;
        UpdateBoard();
    }

    [RelayCommand]
    private void NextMove()
    {
        if (CurrentStep >= TotalSteps) return;
        CurrentStep++;
        UpdateBoard();
    }

    [RelayCommand]
    private void LastMove()
    {
        if (CurrentStep >= TotalSteps) return;
        CurrentStep = TotalSteps;
        UpdateBoard();
    }

    [RelayCommand]
    private void CloseViewer()
    {
        IsViewerOpen = false;
    }
}