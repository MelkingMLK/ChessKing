using CommunityToolkit.Mvvm.ComponentModel;

namespace ChessStrategyApp.Models;

public partial class ChessSquare : ObservableObject
{
    public int Row { get; }
    public int Column { get; }
    public bool IsDark => (Row + Column) % 2 != 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundBrush))]
    private bool _isLastMoveHighlight;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isInCheck;

    [ObservableProperty]
    private bool _isCheckmated;

    [ObservableProperty]
    private bool _isPromoted;

    [ObservableProperty]
    private bool _isTargetMove;

    public string BackgroundBrush
    {
        get
        {
            if (IsLastMoveHighlight) return "#80E2B714"; // Oro translucido mossa recente
            return IsDark ? "#000000" : "#FFFFFF";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PieceGlyph))]
    private string _piece = string.Empty;       

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PieceGlyph))]
    [NotifyPropertyChangedFor(nameof(PieceForeground))]
    private string _pieceColor = string.Empty;  

    public string PieceGlyph => Piece switch
    {
        "K" => PieceColor == "White" ? "♔" : "♚",
        "Q" => PieceColor == "White" ? "♕" : "♛",
        "R" => PieceColor == "White" ? "♖" : "♜",
        "B" => PieceColor == "White" ? "♗" : "♝",
        "N" => PieceColor == "White" ? "♞" : "♞", 
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

    // Mantenuto per retrocompatibilità esplicita con GameViewModel, ma ora privo di duplicati
    public void TriggerBackgroundUpdate() => OnPropertyChanged(nameof(BackgroundBrush));
}