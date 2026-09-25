using CommunityToolkit.Mvvm.ComponentModel;

namespace ChessStrategyApp.Models;

public partial class ChessSquare : ObservableObject
{
    public int Row { get; }
    public int Column { get; }
    public bool IsDark => (Row + Column) % 2 != 0;
    public string BackgroundBrush => IsDark ? "#000000" : "#FFFFFF";
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PieceGlyph))]
    private string _piece = string.Empty;       

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PieceGlyph))]
    [NotifyPropertyChangedFor(nameof(PieceForeground))]
    private string _pieceColor = string.Empty;  

    [ObservableProperty]
    private bool _isTargetMove = false;

    [ObservableProperty]
    private bool _isSelected = false;

    // STATI ANIMATI
    [ObservableProperty]
    private bool _isInCheck = false;

    [ObservableProperty]
    private bool _isCheckmated = false;

    [ObservableProperty]
    private bool _isPromoted = false;

    public string PieceGlyph => Piece switch
    {
        "K" => PieceColor == "White" ? "♔" : "♚",
        "Q" => PieceColor == "White" ? "♕" : "♛",
        "R" => PieceColor == "White" ? "♖" : "♜",
        "B" => PieceColor == "White" ? "♗" : "♝",
        "N" => PieceColor == "White" ? "♘" : "♞",
        "P" => PieceColor == "White" ? "♙" : "♟",
        _ => string.Empty
    };

    public string PieceForeground => PieceColor == "White" ? "#AB8476" : "#8675A9";

    public ChessSquare(int row, int col, string piece = "", string pieceColor = "")
    {
        Row = row;
        Column = col;
        _piece = piece;
        _pieceColor = pieceColor;
    }
}