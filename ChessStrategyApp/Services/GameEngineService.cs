using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using ChessDotNet;
using ChessDotNet.Pieces;

namespace ChessStrategyApp.Services;

public class GameEngineService
{
    private ChessGame _game;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<string, int> _positionHistory = new();

    public event Action? OnTimeTick;
    public event Action<Player>? OnTimeOut;
    public event Action? OnMoveExecuted;

    public TimeSpan WhiteTime { get; private set; } = TimeSpan.FromMinutes(15);
    public TimeSpan BlackTime { get; private set; } = TimeSpan.FromMinutes(15);

    public Player Turn => _game.WhoseTurn;
    public bool IsGameOver => IsCheckmate() || IsStalemate() || IsDrawByRepetition();

    // Esposizione per il motore decisionale del Bot
    public ChessGame RawGameInstance => _game;

    public GameEngineService()
    {
        _game = new ChessGame();
        TrackCurrentPosition();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (s, e) => HandleTimerTick();
        _timer.Start();
    }

    private void HandleTimerTick()
    {
        if (IsGameOver) return;

        if (Turn == Player.White)
        {
            WhiteTime = WhiteTime.Subtract(TimeSpan.FromSeconds(1));
            if (WhiteTime <= TimeSpan.Zero)
            {
                WhiteTime = TimeSpan.Zero;
                _timer.Stop();
                OnTimeOut?.Invoke(Player.White);
            }
        }
        else
        {
            BlackTime = BlackTime.Subtract(TimeSpan.FromSeconds(1));
            if (BlackTime <= TimeSpan.Zero)
            {
                BlackTime = TimeSpan.Zero;
                _timer.Stop();
                OnTimeOut?.Invoke(Player.Black);
            }
        }

        OnTimeTick?.Invoke();
    }

    public void ResetGame()
    {
        _game = new ChessGame();
        _positionHistory.Clear();
        TrackCurrentPosition();

        WhiteTime = TimeSpan.FromMinutes(15);
        BlackTime = TimeSpan.FromMinutes(15);

        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    public Piece? GetPieceAt(int file, int rank)
    {
        var pos = new Position((File)file, rank);
        return _game.GetPieceAt(pos);
    }

    public ReadOnlyCollection<Position> GetLegalDestinations(string fromSquare)
    {
        if (fromSquare.Length < 2) return new ReadOnlyCollection<Position>(new List<Position>());

        int file = fromSquare[0] - 'a';
        int rank = fromSquare[1] - '0';
        var fromPos = new Position((File)file, rank);

        var destinations = new List<Position>();
        var moves = _game.GetValidMoves(Turn);

        foreach (var m in moves)
        {
            if (m.OriginalPosition.Equals(fromPos))
            {
                destinations.Add(m.NewPosition);
            }
        }

        return new ReadOnlyCollection<Position>(destinations);
    }

    public bool IsPawnPromotion(string fromSquare, string toSquare)
    {
        if (fromSquare.Length < 2 || toSquare.Length < 2) return false;

        int fromFile = fromSquare[0] - 'a';
        int fromRank = fromSquare[1] - '0';
        int toRank = toSquare[1] - '0';

        var piece = _game.GetPieceAt(new Position((File)fromFile, fromRank));
        if (piece is Pawn)
        {
            if (piece.Owner == Player.White && fromRank == 7 && toRank == 8) return true;
            if (piece.Owner == Player.Black && fromRank == 2 && toRank == 1) return true;
        }

        return false;
    }

    public bool TryMove(string fromSquare, string toSquare, char promotion = ' ')
    {
        if (fromSquare.Length < 2 || toSquare.Length < 2) return false;

        int fromFile = fromSquare[0] - 'a';
        int fromRank = fromSquare[1] - '0';
        int toFile = toSquare[0] - 'a';
        int toRank = toSquare[1] - '0';

        var fromPos = new Position((File)fromFile, fromRank);
        var toPos = new Position((File)toFile, toRank);

        Move move = promotion != ' ' ? new Move(fromPos, toPos, Turn, promotion) : new Move(fromPos, toPos, Turn);

        if (_game.IsValidMove(move))
        {
            _game.MakeMove(move, true);
            TrackCurrentPosition();
            OnMoveExecuted?.Invoke();
            return true;
        }

        return false;
    }

    private void TrackCurrentPosition()
    {
        string fen = _game.GetFen();
        string key = string.Join(" ", fen.Split(' ').Take(4));

        if (_positionHistory.TryGetValue(key, out int count))
        {
            _positionHistory[key] = count + 1;
        }
        else
        {
            _positionHistory[key] = 1;
        }
    }

    public bool IsDrawByRepetition()
    {
        string fen = _game.GetFen();
        string key = string.Join(" ", fen.Split(' ').Take(4));
        return _positionHistory.TryGetValue(key, out int count) && count >= 3;
    }

    public bool IsStalemate() => _game.IsStalemated(_game.WhoseTurn);

    public bool IsCheckmate() => _game.IsCheckmated(_game.WhoseTurn);

    public bool IsPlayerInCheck(Player player) => _game.IsInCheck(player);

    public bool IsPlayerCheckmated(Player player) => _game.IsCheckmated(player);

    public Position? FindKingPosition(Player player)
    {
        for (int f = 0; f < 8; f++)
        {
            for (int r = 1; r <= 8; r++)
            {
                var pos = new Position((File)f, r);
                var p = _game.GetPieceAt(pos);
                if (p is King && p.Owner == player) return pos;
            }
        }
        return null;
    }

    public (List<string> WhiteCaptured, List<string> BlackCaptured, int ScoreAdvantage) GetCapturedPiecesAndScore()
    {
        int wP = 8, wN = 2, wB = 2, wR = 2, wQ = 1;
        int bP = 8, bN = 2, bB = 2, bR = 2, bQ = 1;

        for (int f = 0; f < 8; f++)
        {
            for (int r = 1; r <= 8; r++)
            {
                var p = _game.GetPieceAt(new Position((File)f, r));
                if (p == null) continue;

                if (p.Owner == Player.White)
                {
                    if (p is Pawn) wP--;
                    else if (p is Knight) wN--;
                    else if (p is Bishop) wB--;
                    else if (p is Rook) wR--;
                    else if (p is Queen) wQ--;
                }
                else
                {
                    if (p is Pawn) bP--;
                    else if (p is Knight) bN--;
                    else if (p is Bishop) bB--;
                    else if (p is Rook) bR--;
                    else if (p is Queen) bQ--;
                }
            }
        }

        var whiteCaptured = new List<string>();
        for (int i = 0; i < Math.Max(0, bQ); i++) whiteCaptured.Add("♛");
        for (int i = 0; i < Math.Max(0, bR); i++) whiteCaptured.Add("♜");
        for (int i = 0; i < Math.Max(0, bB); i++) whiteCaptured.Add("♝");
        for (int i = 0; i < Math.Max(0, bN); i++) whiteCaptured.Add("♞");
        for (int i = 0; i < Math.Max(0, bP); i++) whiteCaptured.Add("♟");

        var blackCaptured = new List<string>();
        for (int i = 0; i < Math.Max(0, wQ); i++) blackCaptured.Add("♕");
        for (int i = 0; i < Math.Max(0, wR); i++) blackCaptured.Add("♖");
        for (int i = 0; i < Math.Max(0, wB); i++) blackCaptured.Add("♗");
        for (int i = 0; i < Math.Max(0, wN); i++) blackCaptured.Add("♘");
        for (int i = 0; i < Math.Max(0, wP); i++) blackCaptured.Add("♙");

        int whiteMaterial = (8 - Math.Max(0, wP)) * 1 + (2 - Math.Max(0, wN)) * 3 + (2 - Math.Max(0, wB)) * 3 + (2 - Math.Max(0, wR)) * 5 + (1 - Math.Max(0, wQ)) * 9;
        int blackMaterial = (8 - Math.Max(0, bP)) * 1 + (2 - Math.Max(0, bN)) * 3 + (2 - Math.Max(0, bB)) * 3 + (2 - Math.Max(0, bR)) * 5 + (1 - Math.Max(0, bQ)) * 9;

        return (whiteCaptured, blackCaptured, whiteMaterial - blackMaterial);
    }
}