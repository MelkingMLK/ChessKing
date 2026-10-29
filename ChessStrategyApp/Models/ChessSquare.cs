using CommunityToolkit.Mvvm.ComponentModel;

namespace ChessStrategyApp.Models;

public partial class ChessSquare : ObservableObject
{
    public int Row { get; }
    public int Column { get; }
    public bool IsDark => (Row + Column) % 2 != 0;

    [ObservableProperty]
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

    // Coordinate a bordo casella
    [ObservableProperty]
    private string _rankCoordinate = string.Empty;

    [ObservableProperty]
    private string _fileCoordinate = string.Empty;

    public bool HasRankCoordinate => !string.IsNullOrEmpty(RankCoordinate);
    public bool HasFileCoordinate => !string.IsNullOrEmpty(FileCoordinate);

    // Modalità Cieca (BGC): nasconde l'identità del pezzo nemico
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PieceGlyph))]
    private bool _isObscured;

    public string BackgroundBrush => IsDark ? "#000000" : "#FFFFFF";
    public string CoordinateBrush => IsDark ? "#684756" : "#AB8476";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PieceGlyph))]
    private string _piece = string.Empty;       

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PieceGlyph))]
    [NotifyPropertyChangedFor(nameof(PieceForeground))]
    private string _pieceColor = string.Empty;  

    public string PieceGlyph
    {
        get
        {
            if (string.IsNullOrEmpty(Piece)) return string.Empty;

            // Se la pedina è mascherata dalla modalità BGC
            if (IsObscured)
            {
                return "■";
            }

            return Piece switch
            {
                "K" => PieceColor == "White" ? "♔" : "♚",
                "Q" => PieceColor == "White" ? "♕" : "♛",
                "R" => PieceColor == "White" ? "♖" : "♜",
                "B" => PieceColor == "White" ? "♗" : "♝",
                "N" => PieceColor == "White" ? "♘" : "♞",
                "P" => PieceColor == "White" ? "♙" : "♟",
                _ => string.Empty
            };
        }
    }

    public string PieceForeground => PieceColor == "White" ? "#AB8476" : "#8675A9";

    public ChessSquare(int row, int col, string piece = "", string pieceColor = "")
    {
        Row = row;
        Column = col;
        _piece = piece;
        _pieceColor = pieceColor;
    }

    public void TriggerBackgroundUpdate() => OnPropertyChanged(nameof(BackgroundBrush));
}