using System;
using System.Collections.Generic;

namespace ChessStrategyApp.Models;

public class ChessStrategyStore
{
    public List<OpeningStrategy> Openings { get; set; } = new();
}

public class OpeningStrategy
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string StrategyType { get; set; } = "Opening"; 
    public string Name { get; set; } = string.Empty;       
    public string PlayerColor { get; set; } = "White"; // "White" o "Black"
    
    // L'albero parte da una lista di prime mosse possibili (es: 1.e4, 1.d4)
    public List<MoveNode> RootMoves { get; set; } = new(); 
}

public class MoveNode
{
    public string MoveSan { get; set; } = string.Empty;    // Es: "e4"
    public string Comment { get; set; } = string.Empty;    
    
    // Le risposte a questa mossa sono annidate qui dentro (Bivi/Varianti)
    public List<MoveNode> NextMoves { get; set; } = new(); 
}