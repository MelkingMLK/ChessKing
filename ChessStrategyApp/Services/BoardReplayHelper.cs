using System;
using System.Collections.Generic;
using ChessDotNet;
using ChessDotNet.Pieces;
using ChessStrategyApp.Models;

namespace ChessStrategyApp.Services;

public static class BoardReplayHelper
{
    public static List<ChessSquare> GenerateBoardAtStep(List<string> moves, int stepIndex)
    {
        var game = new ChessGame();

        int targetMoves = Math.Min(stepIndex, moves.Count);
        for (int i = 0; i < targetMoves; i++)
        {
            string rawMove = moves[i].Trim();
            ApplyMove(game, rawMove);
        }

        var squares = new List<ChessSquare>(64);
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                int chessRank = 8 - r;
                int chessFile = c;
                var piece = game.GetPieceAt((File)chessFile, chessRank);

                var sq = new ChessSquare(r, c);
                if (piece != null)
                {
                    sq.Piece = piece.GetFenCharacter().ToString().ToUpperInvariant();
                    sq.PieceColor = piece.Owner == Player.White ? "White" : "Black";
                }
                squares.Add(sq);
            }
        }

        return squares;
    }

    private static void ApplyMove(ChessGame game, string rawMoveText)
    {
        if (string.IsNullOrWhiteSpace(rawMoveText)) return;

        string cleanMove = rawMoveText.Trim();

        // 1. Caso Formato a Coordinate Dirette: "e2-e4" o "g1-f3"
        if (cleanMove.Contains('-') && cleanMove.Length >= 5)
        {
            var parts = cleanMove.Split('-');
            var from = ParsePosition(parts[0]);
            var to = ParsePosition(parts[1]);
            if (from != null && to != null)
            {
                var move = new Move(from, to, game.WhoseTurn);
                if (game.IsValidMove(move))
                {
                    game.MakeMove(move, true);
                    return;
                }
            }
        }

        // 2. Risoluzione Mosse Legali Correnti
        var validMoves = game.GetValidMoves(game.WhoseTurn);

        // A. Match Arrocco
        if (cleanMove.Equals("O-O", StringComparison.OrdinalIgnoreCase) || cleanMove.Equals("0-0"))
        {
            int rank = game.WhoseTurn == Player.White ? 1 : 8;
            foreach (var m in validMoves)
            {
                if (m.OriginalPosition.File == File.E && m.OriginalPosition.Rank == rank &&
                    m.NewPosition.File == File.G && m.NewPosition.Rank == rank)
                {
                    game.MakeMove(m, true);
                    return;
                }
            }
        }

        if (cleanMove.Equals("O-O-O", StringComparison.OrdinalIgnoreCase) || cleanMove.Equals("0-0-0"))
        {
            int rank = game.WhoseTurn == Player.White ? 1 : 8;
            foreach (var m in validMoves)
            {
                if (m.OriginalPosition.File == File.E && m.OriginalPosition.Rank == rank &&
                    m.NewPosition.File == File.C && m.NewPosition.Rank == rank)
                {
                    game.MakeMove(m, true);
                    return;
                }
            }
        }

        // B. Match SAN Standard (Pedoni e Pezzi: e4, d4, Nf3, Nc6, Bb5, ecc.)
        foreach (var m in validMoves)
        {
            var piece = game.GetPieceAt(m.OriginalPosition);
            if (piece == null) continue;

            string dest = $"{m.NewPosition.File.ToString().ToLowerInvariant()}{m.NewPosition.Rank}";

            // Se è un pedone (es. input "e4", "d5")
            if (piece is Pawn)
            {
                // Mossa semplice pedone
                if (dest.Equals(cleanMove, StringComparison.OrdinalIgnoreCase))
                {
                    game.MakeMove(m, true);
                    return;
                }

                // Cattura pedone (es. "exd5" o "ed5")
                string pawnCapture1 = $"{m.OriginalPosition.File.ToString().ToLowerInvariant()}x{dest}";
                string pawnCapture2 = $"{m.OriginalPosition.File.ToString().ToLowerInvariant()}{dest}";
                if (cleanMove.Equals(pawnCapture1, StringComparison.OrdinalIgnoreCase) ||
                    cleanMove.Equals(pawnCapture2, StringComparison.OrdinalIgnoreCase))
                {
                    game.MakeMove(m, true);
                    return;
                }
            }
            else
            {
                // Se è un pezzo (N, B, R, Q, K)
                char pieceChar = char.ToUpperInvariant(piece.GetFenCharacter());

                // Formato canonico "Nf3"
                string standardPieceMove = $"{pieceChar}{dest}";

                // Formato cattura "Nxf3"
                string capturePieceMove = $"{pieceChar}x{dest}";

                if (cleanMove.Equals(standardPieceMove, StringComparison.OrdinalIgnoreCase) ||
                    cleanMove.Equals(capturePieceMove, StringComparison.OrdinalIgnoreCase))
                {
                    game.MakeMove(m, true);
                    return;
                }

                // Gestione ambiguità (es. "Nbd7" o "N1f3")
                string disambigFile = $"{pieceChar}{m.OriginalPosition.File.ToString().ToLowerInvariant()}{dest}";
                string disambigRank = $"{pieceChar}{m.OriginalPosition.Rank}{dest}";
                if (cleanMove.Equals(disambigFile, StringComparison.OrdinalIgnoreCase) ||
                    cleanMove.Equals(disambigRank, StringComparison.OrdinalIgnoreCase))
                {
                    game.MakeMove(m, true);
                    return;
                }
            }

            // Fallback diretto formato ToString ("G1-F3")
            if (m.ToString().Equals(cleanMove, StringComparison.OrdinalIgnoreCase))
            {
                game.MakeMove(m, true);
                return;
            }
        }
    }

    private static Position? ParsePosition(string pos)
    {
        if (pos.Length < 2) return null;
        char fileChar = char.ToLowerInvariant(pos[0]);
        if (fileChar < 'a' || fileChar > 'h') return null;
        if (!int.TryParse(pos.Substring(1), out int rank) || rank < 1 || rank > 8) return null;

        return new Position((File)(fileChar - 'a'), rank);
    }
}