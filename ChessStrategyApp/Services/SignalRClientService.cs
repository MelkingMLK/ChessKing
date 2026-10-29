using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;

namespace ChessStrategyApp.Services;

public class SignalRClientService
{
    private HubConnection? _hubConnection;

    public event Action<string>? OnRoomCreated;
    public event Action<string>? OnJoinFailed;
    public event Action<string, string, string, string>? OnGameStarted; // (color, opponent, matchType, initialFen)
    public event Action<string>? OnMoveReceived;
    public event Action? OnOpponentLeft;
    public event Action? OnConnected;
    public event Action<string>? OnConnectionFailed;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    private async Task EnsureConnectedAsync(string serverUrl)
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(serverUrl, options =>
            {
                options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = (message) =>
                {
                    if (message is HttpClientHandler clientHandler)
                    {
                        clientHandler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
                    }
                    return message;
                };
            })
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<string>("RoomCreated", code => OnRoomCreated?.Invoke(code));
        _hubConnection.On<string>("JoinFailed", reason => OnJoinFailed?.Invoke(reason));
        _hubConnection.On<string, string, string, string>("GameStarted", (color, opponent, matchType, fen) => 
            OnGameStarted?.Invoke(color, opponent, matchType, fen));
        _hubConnection.On<string>("ReceiveMove", move => OnMoveReceived?.Invoke(move));
        _hubConnection.On("OpponentLeft", () => OnOpponentLeft?.Invoke());

        await _hubConnection.StartAsync();
        OnConnected?.Invoke();
    }

    public async Task CreateRoomAsync(string serverUrl, string roomCode, string hostName, string matchType, string initialFen)
    {
        try
        {
            await EnsureConnectedAsync(serverUrl);
            await _hubConnection!.InvokeAsync("CreateRoom", roomCode, hostName, matchType, initialFen);
        }
        catch (Exception ex)
        {
            OnConnectionFailed?.Invoke(ex.Message);
        }
    }

    public async Task JoinRoomAsync(string serverUrl, string roomCode, string guestName)
    {
        try
        {
            await EnsureConnectedAsync(serverUrl);
            await _hubConnection!.InvokeAsync("JoinRoom", roomCode, guestName);
        }
        catch (Exception ex)
        {
            OnConnectionFailed?.Invoke(ex.Message);
        }
    }

    public async Task SendMoveAsync(string roomCode, string moveData)
    {
        if (IsConnected && _hubConnection != null)
        {
            await _hubConnection.InvokeAsync("SendMove", roomCode, moveData);
        }
    }

    public async Task DisconnectAsync()
    {
        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.StopAsync();
            }
            catch { }
            finally
            {
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
            }
        }
    }
}