using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ChessDotNet;
using ChessDotNet.Pieces;

namespace ChessStrategyApp.Services;

public class BotEngineService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(3) };
    private readonly Random _random = new();

    public async Task<Move?> PickBestMoveAsync(ChessGame game, int botElo)
    {
        var validMoves = game.GetValidMoves(game.WhoseTurn).ToList();
        if (validMoves.Count == 0) return null;

        // Simulazione fedele per ELO bassi (200 - 600)
        // Stockfish a depth 1 è comunque troppo forte (~1000 ELO), quindi introduciamo il blunder rate
        if (botElo <= 600)
        {
            double blunderRate = botElo switch
            {
                <= 200 => 0.80,
                <= 400 => 0.55,
                _ => 0.35
            };

            if (_random.NextDouble() < blunderRate)
            {
                // Mossa casuale tra le legali
                return validMoves[_random.Next(validMoves.Count)];
            }
        }

        // Calcolo della profondità per l'API di Stockfish in base all'ELO
        int depth = botElo switch
        {
            <= 600 => 1,
            <= 1000 => 2,
            <= 1400 => 4,
            <= 1700 => 6,
            _ => 9
        };

        // Chiamata all'API Stockfish
        try
        {
            string fen = Uri.EscapeDataString(game.GetFen());
            string url = $"https://stockfish.online/api/s/v2.php?fen={fen}&depth={depth}";

            string responseJson = await HttpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("success", out var successProp) && successProp.GetBoolean())
            {
                if (root.TryGetProperty("bestmove", out var bestMoveProp))
                {
                    string rawBestMove = bestMoveProp.GetString() ?? string.Empty;
                    // Il formato restituito è: "bestmove e7e5 ponder d2d4" oppure "bestmove e7e8q"
                    var parts = rawBestMove.Split(' ');
                    if (parts.Length >= 2)
                    {
                        string lan = parts[1]; // es: "e7e5" o "e7e8q"
                        var parsedMove = ConvertLanToMove(lan, game);
                        if (parsedMove != null && game.IsValidMove(parsedMove))
                        {
                            return parsedMove;
                        }
                    }
                }
            }
        }
        catch
        {
            // In caso di offline, timeout o rate-limit, fallback sul motore euristico locale
        }

        return FallbackLocalMove(game, validMoves);
    }

    private Move? ConvertLanToMove(string lan, ChessGame game)
    {
        if (lan.Length < 4) return null;

        int fromFile = lan[0] - 'a';
        int fromRank = lan[1] - '0';
        int toFile = lan[2] - 'a';
        int toRank = lan[3] - '0';

        var fromPos = new Position((File)fromFile, fromRank);
        var toPos = new Position((File)toFile, toRank);

        if (lan.Length >= 5)
        {
            char promo = char.ToUpperInvariant(lan[4]);
            return new Move(fromPos, toPos, game.WhoseTurn, promo);
        }

        return new Move(fromPos, toPos, game.WhoseTurn);
    }

    private Move FallbackLocalMove(ChessGame game, List<Move> validMoves)
    {
        // Ricerca euristica locale: favorisce catture o mosse centrali
        var captures = validMoves.Where(m => game.GetPieceAt(m.NewPosition) != null).ToList();
        if (captures.Count > 0 && _random.NextDouble() < 0.70)
        {
            return captures[_random.Next(captures.Count)];
        }

        return validMoves[_random.Next(validMoves.Count)];
    }
}