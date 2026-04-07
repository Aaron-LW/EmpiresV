using Color = System.Drawing.Color;
using Smash.Graphics;
using System.Numerics;
using System.Net.Sockets;
using System.Net;
using Smash;
using System.Text.Json;
using Smash.Input;
using SDL3;

public class LobbyState : GameState
{
    private string? _localIp;
    private Texture2D _hostCrown;

    private (string username, PlayerData playerData) _userPlayerData;
    private Dictionary<string, PlayerData> _playerData = new();
    private readonly object _playerDataLock = new();

    private InputField _chatInputField;

    public LobbyState(string username, bool host)
    {
        _localIp = GetLocalIp();
        _hostCrown = AssetManager.GetTexture("HostCrown");

        _userPlayerData.username = username;
        _userPlayerData.playerData = new PlayerData { Host = host };

        _chatInputField = new()
        {
            X = 0,
            GetY = (InputField inputField) => App.WindowHeight / 2 + 125,
            Width = 4000,
            Height = 60,
            TextColor = Color.White,
            TextAlignment = Alignment.Left,
            BackgroundColor = Color.FromArgb(35, 35, 35)
        };
    }

    public override void Update(double deltaTime)
    {
        if (InputHandler.IsLeftMousePressed())
        {
            if (_chatInputField.Rectangle.IsPositionInRectangle(InputHandler.MousePosition))
                _chatInputField.Selected = true;
            else
                _chatInputField.Selected = false;
        }

        if (InputHandler.TextInput != null)
        {
            if (_chatInputField.Selected) _chatInputField.SendTextInput(InputHandler.TextInput);
        }

        if (InputHandler.IsKeyPressed(SDL.Keycode.Backspace))
        {
            if (_chatInputField.Selected) _chatInputField.SendTextInput("Backspace");
        }

        if (InputHandler.IsKeyPressed(SDL.Keycode.Return))
        {
            if (_chatInputField.Selected && !string.IsNullOrEmpty(_chatInputField.Text))
            {
                _ = App.SendPacketTcp(new PingPacket() { Type = "ping", Message = _chatInputField.Text, Username = _userPlayerData.username});
                _chatInputField.Text = "";
            }
        }
    }

    public override void Render(Renderer renderer)
    {
        renderer.RenderText(App.Font, "Lobby", new Vector2(20), Color.White);
        renderer.RenderText(App.Font, $"Ip: {_localIp}", new Vector2(App.Font.MeasureString("Lobby").X + 40, 20), Color.White);

        List<(string username, PlayerData data)> values;
        lock (_playerDataLock)
        {
            values = [_userPlayerData, .. _playerData.Select(d => (d.Key, d.Value))];
        }

        Vector2 startPosition = new Vector2(40, 100);
        for (int i = 0; i < values.Count; i++)
        {
            Vector2 currentPosition = startPosition + new Vector2(0, 50 * i + 20 * i);
            Rectangle rectangle = new Rectangle(currentPosition, App.WindowWidth - 80, 50);
            renderer.RenderFilledRectangle(rectangle, Color.FromArgb(40, 40, 40));
            if (_userPlayerData.username == values[i].username) renderer.RenderRectangle(rectangle, Color.White);
            
            Vector2 textPosition = currentPosition + new Vector2(rectangle.Height / 2) - new Vector2(0, App.Font.MeasureString(values[i].username).Y / 2); 
            renderer.RenderText(App.Font, values[i].username, textPosition, Color.White);
            
            if (values[i].data.Host)
            {
                renderer.RenderTexture(_hostCrown, textPosition + new Vector2(App.Font.MeasureString(values[i].username).X + 10, 0), Color.White, 2);
            }
        }

        _chatInputField.Render(renderer);
    }

    private string? GetLocalIp()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
        socket.Connect("8.8.8.8", 65530);

        var endPoint = socket.LocalEndPoint as IPEndPoint;
        if (endPoint != null)
        {
            return endPoint.Address.ToString();
        }
        
        return null;
    }

    public override void ForwardPacket(Packet packet, string json)
    {
        switch (packet.Type)
        {
            case "join":
                JoinPacket joinPacket = JsonSerializer.Deserialize<JoinPacket>(json)!;
                lock (_playerDataLock) _playerData.Add(joinPacket.Username, new PlayerData());
                Console.WriteLine("Received join packet from " + joinPacket.Username);
                break;

            case "playerData":
                PlayerDataPacket playerDataPacket = JsonSerializer.Deserialize<PlayerDataPacket>(json)!;
                lock (_playerDataLock) _playerData[playerDataPacket.Username] = playerDataPacket.PlayerData;
                Console.WriteLine("Received player data packet from " + playerDataPacket.Username);
                break;

            case "ping":
                PingPacket pingPacket = JsonSerializer.Deserialize<PingPacket>(json)!;
                Console.WriteLine($"Received ping from {pingPacket.Username}: {pingPacket.Message}");
                break;
        }
        
        base.ForwardPacket(packet, json);
    }
}