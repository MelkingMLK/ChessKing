using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
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

    public void ShuffleInitialSetup()
    {
        ResetGame();

        var board = new Piece[8][];
        for (int r = 0; r < 8; r++)
        {
            board[r] = new Piece[8];
        }

        // Setup fazione Bianca (righe 7 e 6)
        PlaceShuffledFaction(board, Player.White, backRank: 7, frontRank: 6);

        // Setup fazione Nera (righe 0 e 1)
        PlaceShuffledFaction(board, Player.Black, backRank: 0, frontRank: 1);

        var customCreation = new GameCreationData
        {
            Board = board,
            WhoseTurn = Player.White,
            CanWhiteCastleKingSide = false,
            CanWhiteCastleQueenSide = false,
            CanBlackCastleKingSide = false,
            CanBlackCastleQueenSide = false
        };

        _game = new ChessGame(customCreation);
        _positionHistory.Clear();
        TrackCurrentPosition();
        OnMoveExecuted?.Invoke();
    }

    private void PlaceShuffledFaction(Piece[][] board, Player player, int backRank, int frontRank)
    {
        var pool = new List<Piece>
        {
            new Queen(player),
            new Rook(player), new Rook(player),
            new Bishop(player), new Bishop(player),
            new Knight(player), new Knight(player),
            new Pawn(player), new Pawn(player), new Pawn(player), new Pawn(player),
            new Pawn(player), new Pawn(player), new Pawn(player), new Pawn(player)
        };

        ShuffleList(pool);

        // Il Re è vincolato tassativamente a una delle 8 caselle della prima traversa
        int kingCol = RandomNumberGenerator.GetInt32(8);
        board[backRank][kingCol] = new King(player);

        var availableSlots = new List<(int Row, int Col)>();
        for (int c = 0; c < 8; c++)
        {
            if (c != kingCol) availableSlots.Add((backRank, c));
            availableSlots.Add((frontRank, c));
        }

        ShuffleList(availableSlots);

        for (int i = 0; i < pool.Count; i++)
        {
            var slot = availableSlots[i];
            board[slot.Row][slot.Col] = pool[i];
        }
    }

    private static void ShuffleList<T>(IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = RandomNumberGenerator.GetInt32(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }

    public void LoadCustomFen(string fen)
    {
        ResetGame();
        _game = new ChessGame(fen);
        _positionHistory.Clear();
        TrackCurrentPosition();
        OnMoveExecuted?.Invoke();
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

    public bool TryMoveWithSan(string fromSquare, string toSquare, char promotion, out string sanNotation)
    {
        sanNotation = string.Empty;
        if (fromSquare.Length < 2 || toSquare.Length < 2) return false;

        int fromFile = fromSquare[0] - 'a';
        int fromRank = fromSquare[1] - '0';
        int toFile = toSquare[0] - 'a';
        int toRank = toSquare[1] - '0';

        var fromPos = new Position((File)fromFile, fromRank);
        var toPos = new Position((File)toFile, toRank);

        var movingPiece = _game.GetPieceAt(fromPos);
        var targetPiece = _game.GetPieceAt(toPos);

        Move move = promotion != ' ' ? new Move(fromPos, toPos, Turn, promotion) : new Move(fromPos, toPos, Turn);

        if (!_game.IsValidMove(move)) return false;

        bool isCapture = targetPiece != null || (movingPiece is Pawn && fromFile != toFile);
        string san = string.Empty;

        if (movingPiece is King && Math.Abs(fromFile - toFile) == 2)
        {
            san = toFile > fromFile ? "O-O" : "O-O-O";
        }
        else if (movingPiece is Pawn)
        {
            if (isCapture)
            {
                san = $"{fromSquare[0]}x{toSquare}";
            }
            else
            {
                san = toSquare;
            }

            if (promotion != ' ')
            {
                san += $"={char.ToUpperInvariant(promotion)}";
            }
        }
        else if (movingPiece != null)
        {
            char pieceChar = char.ToUpperInvariant(movingPiece.GetFenCharacter());
            string captureMarker = isCapture ? "x" : string.Empty;
            san = $"{pieceChar}{captureMarker}{toSquare}";
        }

        _game.MakeMove(move, true);
        TrackCurrentPosition();

        if (IsCheckmate())
        {
            san += "#";
        }
        else if (IsPlayerInCheck(Turn))
        {
            san += "+";
        }

        sanNotation = san;
        OnMoveExecuted?.Invoke();
        return true;
    }

    public bool TryMove(string fromSquare, string toSquare, char promotion = ' ')
    {
        return TryMoveWithSan(fromSquare, toSquare, promotion, out _);
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