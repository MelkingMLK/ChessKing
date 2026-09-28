using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Porta 5050 esplicita, libera da interferenze di sistema macOS (AirPlay occupa la 5000)
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5050);
});

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseRouting();
app.UseCors();

app.MapHub<ChessHub>("/chesshub");

app.MapGet("/", () => "ChessKing Relay Server Operativo sulla porta 5050.");

app.Run();

public class ChessHub : Hub
{
    private static readonly ConcurrentDictionary<string, RoomSession> Rooms = new();

    public async Task CreateRoom(string roomCode, string hostName)
    {
        var code = roomCode.Trim().ToUpperInvariant();
        await Groups.AddToGroupAsync(Context.ConnectionId, code);

        var session = new RoomSession
        {
            HostConnectionId = Context.ConnectionId,
            HostName = string.IsNullOrWhiteSpace(hostName) ? "Host" : hostName
        };

        Rooms[code] = session;
        await Clients.Caller.SendAsync("RoomCreated", code);
    }

    public async Task JoinRoom(string roomCode, string guestName)
    {
        var code = roomCode.Trim().ToUpperInvariant();

        if (!Rooms.TryGetValue(code, out var session) || string.IsNullOrEmpty(session.HostConnectionId))
        {
            await Clients.Caller.SendAsync("JoinFailed", "Stanza non trovata. Creala prima.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        session.GuestConnectionId = Context.ConnectionId;
        session.GuestName = string.IsNullOrWhiteSpace(guestName) ? "Ospite" : guestName;

        // Sorteggio casuale 50/50
        bool hostIsWhite = RandomNumberGenerator.GetInt32(2) == 0;
        string hostColor = hostIsWhite ? "White" : "Black";
        string guestColor = hostIsWhite ? "Black" : "White";

        await Clients.Client(session.HostConnectionId).SendAsync("GameStarted", hostColor, session.GuestName);
        await Clients.Client(session.GuestConnectionId).SendAsync("GameStarted", guestColor, session.HostName);
    }

    public async Task SendMove(string roomCode, string moveData)
    {
        var code = roomCode.Trim().ToUpperInvariant();
        await Clients.OthersInGroup(code).SendAsync("ReceiveMove", moveData);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var kvp in Rooms)
        {
            var s = kvp.Value;
            if (s.HostConnectionId == Context.ConnectionId || s.GuestConnectionId == Context.ConnectionId)
            {
                Rooms.TryRemove(kvp.Key, out _);
                _ = Clients.Group(kvp.Key).SendAsync("OpponentLeft");
            }
        }
        await base.OnDisconnectedAsync(exception);
    }
}

public class RoomSession
{
    public string HostConnectionId { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string GuestConnectionId { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
}