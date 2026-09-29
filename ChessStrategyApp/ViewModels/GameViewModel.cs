using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChessStrategyApp.Models;
using ChessStrategyApp.Services;
using ChessDotNet;

namespace ChessStrategyApp.ViewModels;

public partial class GameViewModel : ObservableObject
{
    public enum GameMode { None, Local, P2P, Solo }

    private readonly GameEngineService _engine = new();
    private readonly MatchHistoryService _historyService = new();
    private readonly SignalRClientService _network = new();
    private readonly Random _random = new();

    private ChessSquare? _selectedSquare;
    private string _pendingFromSquare = string.Empty;
    private string _pendingToSquare = string.Empty;
    private readonly List<string> _moves = new();
    private DateTime _matchStartTime;
    private bool _matchSaved = false;

    public ObservableCollection<ChessSquare> Squares { get; } = new();

    // --- STORICO MOSSE SEPARATO (BIANCO E NERO) ---
    public ObservableCollection<string> WhiteMovesHistory { get; } = new();
    public ObservableCollection<string> BlackMovesHistory { get; } = new();

    // --- TRACCIAMENTO ULTIMA MOSSA (COORDINATE LOGICHE PER CORNER MARKERS) ---
    private (int FromFile, int FromRank, int ToFile, int ToRank)? _lastMoveCoordinates;

    // --- STATO DELLA MODALITÀ E NAVIGAZIONE ---
    private GameMode _currentGameMode = GameMode.None;
    public GameMode CurrentGameMode
    {
        get => _currentGameMode;
        set
        {
            if (SetProperty(ref _currentGameMode, value))
            {
                OnPropertyChanged(nameof(IsModeSelectionVisible));
                OnPropertyChanged(nameof(IsBoardViewVisible));
            }
        }
    }

    public bool IsModeSelectionVisible => CurrentGameMode == GameMode.None;
    public bool IsBoardViewVisible => CurrentGameMode == GameMode.Local || CurrentGameMode == GameMode.Solo || (CurrentGameMode == GameMode.P2P && IsP2PGameActive);

    // --- STATI ONLINE ROOMS (SIGNALR) ---
    private bool _isP2PConfigVisible = false;
    public bool IsP2PConfigVisible
    {
        get => _isP2PConfigVisible;
        set => SetProperty(ref _isP2PConfigVisible, value);
    }

    private bool _isP2PGameActive = false;
    public bool IsP2PGameActive
    {
        get => _isP2PGameActive;
        set
        {
            if (SetProperty(ref _isP2PGameActive, value))
            {
                OnPropertyChanged(nameof(IsBoardViewVisible));
            }
        }
    }

    private string _nickname = "Player_" + Random.Shared.Next(100, 999);
    public string Nickname
    {
        get => _nickname;
        set => SetProperty(ref _nickname, value);
    }

    private string _serverUrl = "https://dependably-enginous-azzie.ngrok-free.dev/chesshub";
    public string ServerUrl
    {
        get => _serverUrl;
        set => SetProperty(ref _serverUrl, value);
    }

    private string _roomCode = "STANZA1";
    public string RoomCode
    {
        get => _roomCode;
        set => SetProperty(ref _roomCode, value);
    }

    private string _networkStatus = "Inserisci server, codice stanza e seleziona Crea o Entra.";
    public string NetworkStatus
    {
        get => _networkStatus;
        set => SetProperty(ref _networkStatus, value);
    }

    private bool _isConnecting = false;
    public bool IsConnecting
    {
        get => _isConnecting;
        set => SetProperty(ref _isConnecting, value);
    }

    private Player _localAssignedPlayer = Player.White;

    // --- LOGICA DI RIBALTAMENTO PROSPETTIVA DELLA SCACCHIERA ---
    private bool IsBoardFlipped => CurrentGameMode == GameMode.P2P && _localAssignedPlayer == Player.Black;

    private (int file, int rank) DisplayToChessCoords(int displayRow, int displayCol)
    {
        if (IsBoardFlipped)
        {
            return (7 - displayCol, displayRow + 1);
        }
        return (displayCol, 8 - displayRow);
    }

    private (int displayRow, int displayCol) ChessToDisplayCoords(int file, int rank)
    {
        if (IsBoardFlipped)
        {
            return (rank - 1, 7 - file);
        }
        return (8 - rank, file);
    }

    // --- TIMER E MESSAGGISTICA ---
    [ObservableProperty]
    private string _whiteTimerText = "15:00";

    [ObservableProperty]
    private string _blackTimerText = "15:00";

    [ObservableProperty]
    private string _statusMessage = "Seleziona la modalità per iniziare.";

    // --- GIOCATORE ATTIVO ---
    [ObservableProperty]
    private string _activePlayerName = "In attesa";

    [ObservableProperty]
    private string _activePlayerColorTag = "⚪ Bianco";

    [ObservableProperty]
    private bool _isWhiteActive = true;

    // --- MATERIALE CATTURATO (FIFO MAX 4 + BADGE OVERFLOW) ---
    public ObservableCollection<string> DisplayedWhiteCaptured { get; } = new();
    public ObservableCollection<string> DisplayedBlackCaptured { get; } = new();

    [ObservableProperty]
    private string _whiteCapturedOverflowBadge = string.Empty;

    [ObservableProperty]
    private string _blackCapturedOverflowBadge = string.Empty;

    [ObservableProperty]
    private bool _hasWhiteOverflow = false;

    [ObservableProperty]
    private bool _hasBlackOverflow = false;

    [ObservableProperty]
    private string _whiteScoreDelta = string.Empty;

    [ObservableProperty]
    private string _blackScoreDelta = string.Empty;

    // --- MODALI ---
    [ObservableProperty]
    private bool _isPromotionModalActive = false;

    [ObservableProperty]
    private bool _isSetupModalActive = false;

    [ObservableProperty]
    private string _player1Name = "Giocatore 1";

    [ObservableProperty]
    private string _player2Name = "Giocatore 2";

    [ObservableProperty]
    private string _whitePlayerName = string.Empty;

    [ObservableProperty]
    private string _blackPlayerName = string.Empty;

    [ObservableProperty]
    private string _drawResultText = string.Empty;

    [ObservableProperty]
    private bool _canStartMatch = false;

    [ObservableProperty]
    private bool _isVictoryModalActive = false;

    [ObservableProperty]
    private string _victoryTitle = string.Empty;

    [ObservableProperty]
    private string _victoryMessage = string.Empty;

    [ObservableProperty]
    private string _victoryDeltaScore = string.Empty;

    public GameViewModel()
    {
        _engine.OnTimeTick += () => Dispatcher.UIThread.Post(UpdateTimerDisplay);

        _engine.OnTimeOut += (player) => Dispatcher.UIThread.Post(async () =>
        {
            if (_matchSaved) return;
            string winner = (player == Player.White ? BlackPlayerName : WhitePlayerName);
            StatusMessage = $"Tempo scaduto! Vince {winner}!";
            await SaveCurrentMatchAsync(winner, "Tempo Scaduto");
        });

        _engine.OnMoveExecuted += () => Dispatcher.UIThread.Post(RefreshBoardFromEngine);

        // Cablaggio eventi SignalR
        _network.OnConnected += () => Dispatcher.UIThread.Post(() =>
        {
            NetworkStatus = "Connesso al server...";
        });

        _network.OnRoomCreated += (code) => Dispatcher.UIThread.Post(() =>
        {
            IsConnecting = false;
            NetworkStatus = $"Stanza [{code}] creata! In attesa che l'avversario entri (colori casuali 50/50)...";
        });

        _network.OnJoinFailed += (err) => Dispatcher.UIThread.Post(() =>
        {
            IsConnecting = false;
            NetworkStatus = $"Errore di accesso: {err}";
        });

        _network.OnGameStarted += (assignedColor, opponentName) => Dispatcher.UIThread.Post(() =>
        {
            IsConnecting = false;
            bool amIWhite = assignedColor.Equals("White", StringComparison.OrdinalIgnoreCase);
            _localAssignedPlayer = amIWhite ? Player.White : Player.Black;

            if (amIWhite)
            {
                WhitePlayerName = string.IsNullOrWhiteSpace(Nickname) ? "Tu" : Nickname.Trim();
                BlackPlayerName = opponentName;
                NetworkStatus = $"Sorteggio: Sei il BIANCO! Avversario: {opponentName}";
            }
            else
            {
                WhitePlayerName = opponentName;
                BlackPlayerName = string.IsNullOrWhiteSpace(Nickname) ? "Tu" : Nickname.Trim();
                NetworkStatus = $"Sorteggio: Sei il NERO! Avversario: {opponentName}";
            }

            IsP2PConfigVisible = false;
            IsP2PGameActive = true;
            NotifyViewStates();

            _matchStartTime = DateTime.Now;
            _moves.Clear();
            WhiteMovesHistory.Clear();
            BlackMovesHistory.Clear();
            _lastMoveCoordinates = null;
            _matchSaved = false;
            _engine.ResetGame();
            RefreshBoardFromEngine();
        });

        _network.OnMoveReceived += (movePayload) => Dispatcher.UIThread.Post(() =>
        {
            var parts = movePayload.Split('=');
            var coords = parts[0].Split('-');

            if (coords.Length == 2)
            {
                char promo = parts.Length > 1 && parts[1].Length > 0 ? parts[1][0] : ' ';
                bool success = promo != ' ' ? _engine.TryMove(coords[0], coords[1], promo) : _engine.TryMove(coords[0], coords[1]);
                if (success)
                {
                    _moves.Add(movePayload);
                    RecordMoveInHistory(coords[0], coords[1], isWhiteTurn: _engine.Turn != Player.White);
                    RefreshBoardFromEngine();
                }
            }
        });

        _network.OnOpponentLeft += () => Dispatcher.UIThread.Post(() =>
        {
            StatusMessage = "L'avversario ha abbandonato la partita.";
            NetworkStatus = "Stanza chiusa: l'avversario si è disconnesso.";
        });

        _network.OnConnectionFailed += (err) => Dispatcher.UIThread.Post(() =>
        {
            NetworkStatus = $"Errore di connessione: {err}";
            IsConnecting = false;
        });

        InitializeEmptyBoard();
        RefreshBoardFromEngine();
    }

    private void InitializeEmptyBoard()
    {
        Squares.Clear();
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                Squares.Add(new ChessSquare(r, c));
            }
        }
    }

    private void UpdateTimerDisplay()
    {
        WhiteTimerText = _engine.WhiteTime.ToString(@"mm\:ss");
        BlackTimerText = _engine.BlackTime.ToString(@"mm\:ss");
    }

    public void RefreshBoardFromEngine()
    {
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                var (chessFile, chessRank) = DisplayToChessCoords(r, c);

                var piece = _engine.GetPieceAt(chessFile, chessRank);
                int index = r * 8 + c;

                if (piece != null)
                {
                    Squares[index].Piece = piece.GetFenCharacter().ToString().ToUpperInvariant();
                    Squares[index].PieceColor = piece.Owner == Player.White ? "White" : "Black";
                }
                else
                {
                    Squares[index].Piece = string.Empty;
                    Squares[index].PieceColor = string.Empty;
                }
            }
        }

        UpdateTimerDisplay();
        ClearHighlights();

        // Evidenziazione Corner Markers proiettata sulla visuale attiva
        if (_lastMoveCoordinates.HasValue)
        {
            var m = _lastMoveCoordinates.Value;
            var (dispFromR, dispFromC) = ChessToDisplayCoords(m.FromFile, m.FromRank);
            var (dispToR, dispToC) = ChessToDisplayCoords(m.ToFile, m.ToRank);

            int idxFrom = dispFromR * 8 + dispFromC;
            int idxTo = dispToR * 8 + dispToC;

            if (idxFrom >= 0 && idxFrom < Squares.Count)
            {
                Squares[idxFrom].IsLastMoveHighlight = true;
                Squares[idxFrom].TriggerBackgroundUpdate();
            }
            if (idxTo >= 0 && idxTo < Squares.Count)
            {
                Squares[idxTo].IsLastMoveHighlight = true;
                Squares[idxTo].TriggerBackgroundUpdate();
            }
        }

        // Calcolo e filtraggio catturati (Max 4 + Badge numerico)
        var (whiteCaps, blackCaps, advantage) = _engine.GetCapturedPiecesAndScore();
        UpdateCapturedLists(whiteCaps, blackCaps);

        if (advantage > 0)
        {
            WhiteScoreDelta = $"+{advantage}";
            BlackScoreDelta = string.Empty;
        }
        else if (advantage < 0)
        {
            WhiteScoreDelta = string.Empty;
            BlackScoreDelta = $"+{Math.Abs(advantage)}";
        }
        else
        {
            WhiteScoreDelta = string.Empty;
            BlackScoreDelta = string.Empty;
        }

        foreach (var sq in Squares)
        {
            sq.IsInCheck = false;
            sq.IsCheckmated = false;
        }

        CheckKingStatus(Player.White);
        CheckKingStatus(Player.Black);

        if (IsModeSelectionVisible || IsSetupModalActive) return;

        IsWhiteActive = _engine.Turn == Player.White;
        ActivePlayerName = IsWhiteActive ? WhitePlayerName : BlackPlayerName;
        ActivePlayerColorTag = IsWhiteActive ? "⚪ Bianco" : "⚫ Nero";

        if (_engine.IsGameOver)
        {
            if (!_matchSaved)
            {
                string winner = _engine.Turn == Player.White ? BlackPlayerName : WhitePlayerName;
                StatusMessage = $"Scacco Matto! Vince {winner}.";
                _ = SaveCurrentMatchAsync(winner, "Scacco Matto");
            }
        }
        else if (_engine.IsPlayerInCheck(_engine.Turn))
        {
            StatusMessage = "Scacco al Re!";
        }
        else
        {
            StatusMessage = "Partita in corso.";
        }
    }

    private void UpdateCapturedLists(List<string> whiteCaps, List<string> blackCaps)
    {
        DisplayedWhiteCaptured.Clear();
        foreach (var p in whiteCaps.TakeLast(4)) DisplayedWhiteCaptured.Add(p);
        int wOver = whiteCaps.Count - 4;
        HasWhiteOverflow = wOver > 0;
        WhiteCapturedOverflowBadge = wOver > 0 ? $"+{wOver}" : string.Empty;

        DisplayedBlackCaptured.Clear();
        foreach (var p in blackCaps.TakeLast(4)) DisplayedBlackCaptured.Add(p);
        int bOver = blackCaps.Count - 4;
        HasBlackOverflow = bOver > 0;
        BlackCapturedOverflowBadge = bOver > 0 ? $"+{bOver}" : string.Empty;
    }

    private void RecordMoveInHistory(string from, string to, bool isWhiteTurn)
    {
        string record = $"{from} → {to}";
        if (isWhiteTurn)
            WhiteMovesHistory.Add(record);
        else
            BlackMovesHistory.Add(record);

        int fromFile = from[0] - 'a';
        int fromRank = from[1] - '0';
        int toFile = to[0] - 'a';
        int toRank = to[1] - '0';

        _lastMoveCoordinates = (fromFile, fromRank, toFile, toRank);
    }

    private void CheckKingStatus(Player player)
    {
        if (_engine.IsPlayerCheckmated(player))
        {
            var pos = _engine.FindKingPosition(player);
            if (pos != null) SetSquareKingState(pos, isCheckmated: true);
        }
        else if (_engine.IsPlayerInCheck(player))
        {
            var pos = _engine.FindKingPosition(player);
            if (pos != null) SetSquareKingState(pos, isInCheck: true);
        }
    }

    private void SetSquareKingState(Position pos, bool isInCheck = false, bool isCheckmated = false)
    {
        var (dispR, dispC) = ChessToDisplayCoords((int)pos.File, pos.Rank);
        int idx = dispR * 8 + dispC;
        if (idx >= 0 && idx < Squares.Count)
        {
            Squares[idx].IsInCheck = isInCheck;
            Squares[idx].IsCheckmated = isCheckmated;
        }
    }

    private void ClearHighlights()
    {
        foreach (var sq in Squares)
        {
            sq.IsTargetMove = false;
            sq.IsSelected = false;
            if (sq.IsLastMoveHighlight)
            {
                sq.IsLastMoveHighlight = false;
                sq.TriggerBackgroundUpdate();
            }
        }
    }

    // --- SELEZIONE MODALITÀ ---
    [RelayCommand]
    private void SelectLocalMode()
    {
        CurrentGameMode = GameMode.Local;
        IsP2PConfigVisible = false;
        IsP2PGameActive = false;
        IsSetupModalActive = true;
    }

    [RelayCommand]
    private void SelectP2PMode()
    {
        CurrentGameMode = GameMode.P2P;
        IsP2PConfigVisible = true;
        IsP2PGameActive = false;
        IsSetupModalActive = false;
        NetworkStatus = "Inserisci server, codice stanza e seleziona Crea o Entra.";
    }

    [RelayCommand]
    private void SelectSoloMode()
    {
        CurrentGameMode = GameMode.Solo;
        IsP2PConfigVisible = false;
        IsP2PGameActive = false;
        IsSetupModalActive = false;
        WhitePlayerName = "Giocatore";
        BlackPlayerName = "Chess Bot (CPU)";
        _matchStartTime = DateTime.Now;
        _moves.Clear();
        WhiteMovesHistory.Clear();
        BlackMovesHistory.Clear();
        _lastMoveCoordinates = null;
        _matchSaved = false;
        RefreshBoardFromEngine();
    }

    [RelayCommand]
    private async Task BackToModeSelection()
    {
        await _network.DisconnectAsync();
        CurrentGameMode = GameMode.None;
        IsP2PConfigVisible = false;
        IsP2PGameActive = false;
        IsVictoryModalActive = false;
        IsSetupModalActive = false;
        IsConnecting = false;
        ResetGame();
    }

    private void NotifyViewStates()
    {
        OnPropertyChanged(nameof(IsModeSelectionVisible));
        OnPropertyChanged(nameof(IsBoardViewVisible));
    }

    // --- COMANDI SIGNALR ONLINE ROOMS ---
    [RelayCommand]
    private async Task CreateRoom()
    {
        if (string.IsNullOrWhiteSpace(RoomCode) || string.IsNullOrWhiteSpace(Nickname))
        {
            NetworkStatus = "Inserisci un nickname e un codice stanza.";
            return;
        }

        IsConnecting = true;
        NetworkStatus = $"Creazione stanza [{RoomCode.Trim().ToUpperInvariant()}] in corso...";
        await _network.CreateRoomAsync(ServerUrl.Trim(), RoomCode.Trim().ToUpperInvariant(), Nickname.Trim());
    }

    [RelayCommand]
    private async Task JoinRoom()
    {
        if (string.IsNullOrWhiteSpace(RoomCode) || string.IsNullOrWhiteSpace(Nickname))
        {
            NetworkStatus = "Inserisci un nickname e un codice stanza.";
            return;
        }

        IsConnecting = true;
        NetworkStatus = $"Accesso alla stanza [{RoomCode.Trim().ToUpperInvariant()}] in corso...";
        await _network.JoinRoomAsync(ServerUrl.Trim(), RoomCode.Trim().ToUpperInvariant(), Nickname.Trim());
    }

    // --- GESTIONE MOSSE ---
    [RelayCommand]
    private void SquareClicked(ChessSquare square)
    {
        if (IsModeSelectionVisible || IsSetupModalActive || _engine.IsGameOver || IsPromotionModalActive || IsVictoryModalActive) return;

        // Blocco turni in modalità Online se non è il turno del colore assegnato
        if (CurrentGameMode == GameMode.P2P && _engine.Turn != _localAssignedPlayer) return;

        if (_selectedSquare == null)
        {
            if (!string.IsNullOrEmpty(square.Piece))
            {
                string requiredColor = _engine.Turn == Player.White ? "White" : "Black";
                if (square.PieceColor == requiredColor)
                {
                    _selectedSquare = square;
                    square.IsSelected = true;
                    HighlightLegalMoves(GetCoord(square));
                }
            }
        }
        else
        {
            if (_selectedSquare == square)
            {
                _selectedSquare = null;
                ClearHighlights();
                return;
            }

            string requiredColor = _engine.Turn == Player.White ? "White" : "Black";
            if (!string.IsNullOrEmpty(square.Piece) && square.PieceColor == requiredColor)
            {
                ClearHighlights();
                _selectedSquare = square;
                square.IsSelected = true;
                HighlightLegalMoves(GetCoord(square));
                return;
            }

            string from = GetCoord(_selectedSquare);
            string to = GetCoord(square);

            if (_engine.IsPawnPromotion(from, to))
            {
                _pendingFromSquare = from;
                _pendingToSquare = to;
                IsPromotionModalActive = true;
                ClearHighlights();
                _selectedSquare = null;
                return;
            }

            bool wasWhite = _engine.Turn == Player.White;
            bool success = _engine.TryMove(from, to);
            if (success)
            {
                string moveRecord = $"{from}-{to}";
                _moves.Add(moveRecord);
                RecordMoveInHistory(from, to, isWhiteTurn: wasWhite);

                if (CurrentGameMode == GameMode.P2P)
                {
                    _ = _network.SendMoveAsync(RoomCode.Trim().ToUpperInvariant(), moveRecord);
                }
                else if (CurrentGameMode == GameMode.Solo && !_engine.IsGameOver)
                {
                    TriggerCpuReply();
                }
            }

            _selectedSquare = null;
            ClearHighlights();
        }
    }

    private void HighlightLegalMoves(string fromCoord)
    {
        ClearHighlights();
        if (_selectedSquare != null) _selectedSquare.IsSelected = true;

        var destinations = _engine.GetLegalDestinations(fromCoord);
        foreach (var pos in destinations)
        {
            var (dispR, dispC) = ChessToDisplayCoords((int)pos.File, pos.Rank);
            int idx = dispR * 8 + dispC;
            if (idx >= 0 && idx < Squares.Count)
            {
                Squares[idx].IsTargetMove = true;
            }
        }
    }

    [RelayCommand]
    private void SelectPromotion(string pieceCode)
    {
        char promo = pieceCode.ToUpperInvariant()[0];
        string toSquare = _pendingToSquare;
        bool wasWhite = _engine.Turn == Player.White;
        bool success = _engine.TryMove(_pendingFromSquare, toSquare, promo);
        if (success)
        {
            string moveRecord = $"{_pendingFromSquare}-{toSquare}={promo}";
            _moves.Add(moveRecord);
            RecordMoveInHistory(_pendingFromSquare, toSquare, isWhiteTurn: wasWhite);

            if (CurrentGameMode == GameMode.P2P)
            {
                _ = _network.SendMoveAsync(RoomCode.Trim().ToUpperInvariant(), moveRecord);
            }

            if (toSquare.Length == 2)
            {
                int file = toSquare[0] - 'a';
                int rank = toSquare[1] - '0';
                var (dispR, dispC) = ChessToDisplayCoords(file, rank);
                int idx = dispR * 8 + dispC;
                if (idx >= 0 && idx < Squares.Count)
                {
                    Squares[idx].IsPromoted = true;
                }
            }

            if (CurrentGameMode == GameMode.Solo && !_engine.IsGameOver)
            {
                TriggerCpuReply();
            }
        }

        IsPromotionModalActive = false;
        _pendingFromSquare = string.Empty;
        _pendingToSquare = string.Empty;
    }

    private void TriggerCpuReply()
    {
        Task.Delay(400).ContinueWith(_ =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_engine.Turn == Player.Black && !_engine.IsGameOver)
                {
                    for (int f = 0; f < 8; f++)
                    {
                        for (int r = 1; r <= 8; r++)
                        {
                            var p = _engine.GetPieceAt(f, r);
                            if (p != null && p.Owner == Player.Black)
                            {
                                string from = $"{(char)('a' + f)}{r}";
                                var targets = _engine.GetLegalDestinations(from);
                                if (targets.Count > 0)
                                {
                                    string to = $"{(char)('a' + (int)targets[0].File)}{targets[0].Rank}";
                                    if (_engine.TryMove(from, to))
                                    {
                                        _moves.Add($"{from}-{to}");
                                        RecordMoveInHistory(from, to, isWhiteTurn: false);
                                        RefreshBoardFromEngine();
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            });
        });
    }

    private string GetCoord(ChessSquare sq)
    {
        var (file, rank) = DisplayToChessCoords(sq.Row, sq.Column);
        char fileChar = (char)('a' + file);
        return $"{fileChar}{rank}";
    }

    // --- SETUP LOCALE ---
    [RelayCommand]
    private void DrawColors()
    {
        if (string.IsNullOrWhiteSpace(Player1Name) || string.IsNullOrWhiteSpace(Player2Name))
        {
            DrawResultText = "Inserire entrambi i nickname prima del sorteggio.";
            return;
        }

        if (Player1Name.Trim().Equals(Player2Name.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            DrawResultText = "I due nickname devono essere distinti.";
            return;
        }

        bool p1IsWhite = _random.Next(2) == 0;
        WhitePlayerName = p1IsWhite ? Player1Name.Trim() : Player2Name.Trim();
        BlackPlayerName = p1IsWhite ? Player2Name.Trim() : Player1Name.Trim();

        DrawResultText = $"Sorteggio completato:\n⚪ Bianco: {WhitePlayerName}\n⚫ Nero: {BlackPlayerName}";
        CanStartMatch = true;
    }

    [RelayCommand]
    private void StartMatch()
    {
        IsSetupModalActive = false;
        _matchStartTime = DateTime.Now;
        _moves.Clear();
        WhiteMovesHistory.Clear();
        BlackMovesHistory.Clear();
        _lastMoveCoordinates = null;
        _matchSaved = false;
        RefreshBoardFromEngine();
    }

    [RelayCommand]
    private void ResetGame()
    {
        _engine.ResetGame();
        _selectedSquare = null;
        _moves.Clear();
        WhiteMovesHistory.Clear();
        BlackMovesHistory.Clear();
        _lastMoveCoordinates = null;
        _matchSaved = false;
        CanStartMatch = false;
        DrawResultText = string.Empty;
        IsVictoryModalActive = false;
        ActivePlayerName = "In attesa";
        RefreshBoardFromEngine();
    }

    private async Task SaveCurrentMatchAsync(string winner, string reason)
    {
        _matchSaved = true;

        var (_, _, advantage) = _engine.GetCapturedPiecesAndScore();
        string deltaString = advantage == 0 
            ? "Parità materiale" 
            : (advantage > 0 ? $"+{advantage} per il Bianco" : $"+{Math.Abs(advantage)} per il Nero");

        VictoryTitle = $"HA VINTO {winner.ToUpperInvariant()}!";
        VictoryMessage = $"Esito: {reason}";
        VictoryDeltaScore = $"Differenza Materiale: {deltaString}";
        IsVictoryModalActive = true;

        var item = new MatchHistoryItem
        {
            PlayedAt = DateTime.Now,
            WhitePlayer = WhitePlayerName,
            BlackPlayer = BlackPlayerName,
            WinnerPlayer = winner,
            TerminationReason = reason,
            WhiteTimeLeft = WhiteTimerText,
            BlackTimeLeft = BlackTimerText,
            MatchDuration = DateTime.Now - _matchStartTime,
            FinalFen = string.Empty,
            ScoreDifferential = deltaString,
            MoveHistory = new List<string>(_moves)
        };

        await _historyService.AppendMatchAsync(item);
    }
}