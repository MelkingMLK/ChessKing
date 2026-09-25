using System;
using System.Collections.Generic;
using System.Timers;
using ChessDotNet;
using ChessDotNet.Pieces;

namespace ChessStrategyApp.Services;

public class GameEngineService
{
    private ChessGame _game = new();
    private readonly Timer _gameTimer;
    
    public TimeSpan WhiteTime { get; private set; } = TimeSpan.FromMinutes(15);
    public TimeSpan BlackTime { get; private set; } = TimeSpan.FromMinutes(15);
    
    public bool IsTimerRunning => _gameTimer.Enabled;
    public Player Turn => _game.WhoseTurn;
    public bool IsGameOver => _game.IsCheckmated(Player.White) || _game.IsCheckmated(Player.Black) || 
                              _game.IsStalemated(Player.White) || _game.IsStalemated(Player.Black);

    public event Action? OnTimeTick;
    public event Action<Player>? OnTimeOut;
    public event Action? OnMoveExecuted;

    public GameEngineService()
    {
        _gameTimer = new Timer(1000);
        _gameTimer.Elapsed += HandleTimerTick;
    }

    public void ResetGame()
    {
        _gameTimer.Stop();
        _game = new ChessGame();
        WhiteTime = TimeSpan.FromMinutes(15);
        BlackTime = TimeSpan.FromMinutes(15);
        OnMoveExecuted?.Invoke();
    }

    private void HandleTimerTick(object? sender, ElapsedEventArgs e)
    {
        if (Turn == Player.White)
        {
            WhiteTime = WhiteTime.Subtract(TimeSpan.FromSeconds(1));
            if (WhiteTime <= TimeSpan.Zero)
            {
                _gameTimer.Stop();
                OnTimeOut?.Invoke(Player.White);
            }
        }
        else
        {
            BlackTime = BlackTime.Subtract(TimeSpan.FromSeconds(1));
            if (BlackTime <= TimeSpan.Zero)
            {
                _gameTimer.Stop();
                OnTimeOut?.Invoke(Player.Black);
            }
        }

        OnTimeTick?.Invoke();
    }

    public List<Position> GetLegalDestinations(string fromSquare)
    {
        var destinations = new List<Position>();
        try
        {
            var from = new Position(fromSquare);
            var piece = _game.GetPieceAt(from);
            if (piece == null || piece.Owner != _game.WhoseTurn)
                return destinations;

            var validMoves = _game.GetValidMoves(from);
            foreach (var move in validMoves)
            {
                destinations.Add(move.NewPosition);
            }
        }
        catch { }
        return destinations;
    }

    public bool IsPawnPromotion(string fromSquare, string toSquare)
    {
        try
        {
            var from = new Position(fromSquare);
            var to = new Position(toSquare);
            var piece = _game.GetPieceAt(from);
            if (piece is Pawn)
            {
                if (piece.Owner == Player.White && to.Rank == 8) return true;
                if (piece.Owner == Player.Black && to.Rank == 1) return true;
            }
        }
        catch { }
        return false;
    }

    public bool TryMove(string fromSquare, string toSquare, char? promotionPiece = null)
    {
        try
        {
            var from = new Position(fromSquare);
            var to = new Position(toSquare);
            
            Move move = promotionPiece.HasValue
                ? new Move(from, to, _game.WhoseTurn, promotionPiece.Value)
                : new Move(from, to, _game.WhoseTurn);

            bool isValid = _game.IsValidMove(move);
            if (!isValid) return false;

            _game.MakeMove(move, true);

            if (!_gameTimer.Enabled && !IsGameOver)
            {
                _gameTimer.Start();
            }

            if (IsGameOver)
            {
                _gameTimer.Stop();
            }

            OnMoveExecuted?.Invoke();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Piece? GetPieceAt(int file, int rank)
    {
        var fileChar = (File)file;
        var position = new Position(fileChar, rank);
        return _game.GetPieceAt(position);
    }

    public bool IsPlayerInCheck(Player player) => _game.IsInCheck(player);
    public bool IsPlayerCheckmated(Player player) => _game.IsCheckmated(player);

    public Position? FindKingPosition(Player player)
    {
        for (int f = 0; f < 8; f++)
        {
            for (int r = 1; r <= 8; r++)
            {
                var piece = GetPieceAt(f, r);
                if (piece is ChessDotNet.Pieces.King && piece.Owner == player)
                {
                    return new Position((File)f, r);
                }
            }
        }
        return null;
    }

    public (List<string> WhiteCapturedPieces, List<string> BlackCapturedPieces, int MaterialAdvantage) GetCapturedPiecesAndScore()
    {
        var startingWhite = new Dictionary<char, int> { ['P'] = 8, ['N'] = 2, ['B'] = 2, ['R'] = 2, ['Q'] = 1 };
        var startingBlack = new Dictionary<char, int> { ['P'] = 8, ['N'] = 2, ['B'] = 2, ['R'] = 2, ['Q'] = 1 };

        var aliveWhite = new Dictionary<char, int> { ['P'] = 0, ['N'] = 0, ['B'] = 0, ['R'] = 0, ['Q'] = 0 };
        var aliveBlack = new Dictionary<char, int> { ['P'] = 0, ['N'] = 0, ['B'] = 0, ['R'] = 0, ['Q'] = 0 };

        for (int file = 0; file < 8; file++)
        {
            for (int rank = 1; rank <= 8; rank++)
            {
                var piece = GetPieceAt(file, rank);
                if (piece != null)
                {
                    char fen = char.ToUpperInvariant(piece.GetFenCharacter());
                    if (fen == 'K') continue;

                    if (piece.Owner == Player.White)
                    {
                        if (aliveWhite.ContainsKey(fen)) aliveWhite[fen]++;
                    }
                    else
                    {
                        if (aliveBlack.ContainsKey(fen)) aliveBlack[fen]++;
                    }
                }
            }
        }

        var values = new Dictionary<char, int> { ['P'] = 1, ['N'] = 3, ['B'] = 3, ['R'] = 5, ['Q'] = 9 };
        var glyphsWhite = new Dictionary<char, string> { ['P'] = "♙", ['N'] = "♘", ['B'] = "♗", ['R'] = "♖", ['Q'] = "♕" };
        var glyphsBlack = new Dictionary<char, string> { ['P'] = "♟", ['N'] = "♞", ['B'] = "♝", ['R'] = "♜", ['Q'] = "♛" };

        var whiteCaptured = new List<string>();
        int whiteCapturedValue = 0;
        foreach (var kvp in startingBlack)
        {
            int diff = kvp.Value - aliveBlack[kvp.Key];
            if (diff > 0)
            {
                for (int i = 0; i < diff; i++)
                {
                    whiteCaptured.Add(glyphsBlack[kvp.Key]);
                    whiteCapturedValue += values[kvp.Key];
                }
            }
        }

        var blackCaptured = new List<string>();
        int blackCapturedValue = 0;
        foreach (var kvp in startingWhite)
        {
            int diff = kvp.Value - aliveWhite[kvp.Key];
            if (diff > 0)
            {
                for (int i = 0; i < diff; i++)
                {
                    blackCaptured.Add(glyphsWhite[kvp.Key]);
                    blackCapturedValue += values[kvp.Key];
                }
            }
        }

        int advantage = whiteCapturedValue - blackCapturedValue;
        return (whiteCaptured, blackCaptured, advantage);
    }
}