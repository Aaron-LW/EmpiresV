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

    private readonly string _serverIp;

    private PlayerData _userPlayerData;
    private Dictionary<string, PlayerData> _playerData = new();
    private readonly object _playerDataLock = new();

    private InputField _chatInputField;
    private Rectangle _chatRectangle => new Rectangle(_chatInputField.Position + new Vector2(0, _chatInputField.Height), App.WindowWidth, App.WindowHeight - (_chatInputField.Y + _chatInputField.Height));

    private List<(string username, string message)> _chat = new();
    private float _chatScroll = 0;
    private float _preferredChatScroll = 0;

    private Button? _startGameButton;

    public LobbyState(StateResult previousStateResult, string serverIp) : base(previousStateResult)
    {
        _serverIp = serverIp;

        _localIp = GetLocalIp();
        _hostCrown = AssetManager.GetTexture("HostCrown");

        _userPlayerData = new PlayerData { Host = previousStateResult.PlayerData.Host, Username = previousStateResult.PlayerData.Username };

        _chatInputField = new()
        {
            X = 0,
            GetY = (InputField inputField) => App.WindowHeight / 2 + 125,
            Width = 4000,
            Height = 50,
            TextColor = Color.White,
            TextAlignment = Alignment.Left,
            BackgroundColor = Color.FromArgb(35, 35, 35)
        };

        if (_userPlayerData.Host)
        {
            _startGameButton = new()
            {
                X = 20,
                GetY = (Button button) => App.WindowHeight / 2 + 25,
                Width = 200,
                Height = 80,
                Text = "Start game",
                TextColor = Color.White,
                BackgroundColor = Color.FromArgb(35, 35, 35)
            };
        }
    }

    public override void Update(double deltaTime)
    {
        if (_chatInputField.Rectangle.IsPositionInRectangle(InputHandler.MousePosition))
            SDL.SetCursor(SDL.CreateSystemCursor(SDL.SystemCursor.Text));
        else
            SDL.SetCursor(SDL.CreateSystemCursor(SDL.SystemCursor.Default));

        if (InputHandler.IsLeftMousePressed())
        {
            if (_chatInputField.Rectangle.IsPositionInRectangle(InputHandler.MousePosition))
                _chatInputField.Selected = true;
            else
                _chatInputField.Selected = false;

            if (_startGameButton != null)
            {
                if (_startGameButton.Rectangle.IsPositionInRectangle(InputHandler.MousePosition))
                {
                    OnStateFinish(new LobbyStateResult
                    {
                        PlayerData = _userPlayerData,
                        PeerData = _playerData,
                        Type = "lobby",
                        Seed = 0,
                        ChatHistory = _chat
                    });
                }
            }
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
                PingPacket pingPacket = new PingPacket() { Type = "ping", Message = _chatInputField.Text, Username = _userPlayerData.Username };
                _ = App.SendPacketTcp(pingPacket);

                _chat.Add((_userPlayerData.Username, _chatInputField.Text));
                _preferredChatScroll = Math.Min(-_chat.Count * 40 + _chatRectangle.Height - 25, 0);
                _chatInputField.Text = "";
            }
        }

        if (InputHandler.ScrollWheelDelta != 0)
        {
            _preferredChatScroll += InputHandler.ScrollWheelDelta * 30;
            _preferredChatScroll = Math.Clamp(_preferredChatScroll, Math.Min(-_chat.Count * 40 + _chatRectangle.Height - 25, 0), 0);
        }

        if (_preferredChatScroll != _chatScroll)
        {
            _chatScroll = MathHelper.Lerp(_chatScroll, _preferredChatScroll, 30 * (float)deltaTime);
        }
    }

    public override void Render(Renderer renderer)
    {
        renderer.Clear(Color.FromArgb(20, 20, 20));

        renderer.RenderText(App.Font, "Lobby", new Vector2(20), Color.White);
        //renderer.RenderText(App.Font, $"Server Ip: {_serverIp}", new Vector2(App.Font.MeasureString("Lobby").X + 40, 20), Color.White);

        List<PlayerData> playerData;
        lock (_playerDataLock)
        {
            playerData = [_userPlayerData, .. _playerData.Values];
        }

        Vector2 startPosition = new Vector2(40, 100);
        for (int i = 0; i < playerData.Count; i++)
        {
            Vector2 currentPosition = startPosition + new Vector2(0, 50 * i + 20 * i);
            Rectangle rectangle = new Rectangle(currentPosition, App.WindowWidth - 80, 50);
            renderer.RenderFilledRectangle(rectangle, Color.FromArgb(40, 40, 40));
            if (_userPlayerData.Username == playerData[i].Username) renderer.RenderRectangle(rectangle, Color.White);
            
            Vector2 textPosition = currentPosition + new Vector2(rectangle.Height / 2) - new Vector2(0, App.Font.MeasureString(playerData[i].Username).Y / 2); 
            renderer.RenderText(App.Font, playerData[i].Username, textPosition, Color.White);
            
            if (playerData[i].Host)
            {
                renderer.RenderTexture(_hostCrown, textPosition + new Vector2(App.Font.MeasureString(playerData[i].Username).X + 10, 0), Color.White, 2);
            }
        }

        _chatInputField.Render(renderer);
        renderer.RenderFilledRectangle(_chatRectangle, Color.FromArgb(25, 25, 25));
    
        _startGameButton?.Render(renderer);

        SDL.Rect clipRect = _chatRectangle.ToSDLRect();
        SDL.SetRenderClipRect(renderer.Handle, clipRect);

        Vector2 startPos = _chatRectangle.Position + new Vector2(20);
        for (int i = 0; i < _chat.Count; i++)
        {
            Vector2 position = startPos + new Vector2(0, i * 40) + new Vector2(0, _chatScroll);
            if (position.Y < 0) continue;
            renderer.RenderText(App.Font, $"{_chat[i].username}: {_chat[i].message}", Vector2.Round(position), Color.White);
        }

        SDL.SetRenderClipRect(renderer.Handle, 0);
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
                lock (_playerDataLock) _playerData.Add(joinPacket.Username, new PlayerData() { Username = joinPacket.Username });
                Console.WriteLine("Received join packet from " + joinPacket.Username);
                break;

            case "playerData":
                PlayerDataPacket playerDataPacket = JsonSerializer.Deserialize<PlayerDataPacket>(json)!;
                lock (_playerDataLock) _playerData[playerDataPacket.Username] = playerDataPacket.PlayerData;
                Console.WriteLine("Received player data packet from " + playerDataPacket.Username);
                break;

            case "ping":
                PingPacket pingPacket = JsonSerializer.Deserialize<PingPacket>(json)!;
                _chat.Add((pingPacket.Username, pingPacket.Message));
                _preferredChatScroll = Math.Min(-_chat.Count * 40 + _chatRectangle.Height - 25, 0);
                break;

            case "start_game":
                StartGamePacket startGamePacket = JsonSerializer.Deserialize<StartGamePacket>(json)!;
                OnStateFinish(new LobbyStateResult
                {
                    PlayerData = _userPlayerData,
                    PeerData = _playerData,
                    Type = "lobby",
                    Seed = startGamePacket.Seed,
                    ChatHistory = _chat
                });

                break;
        }
        
        base.ForwardPacket(packet, json);
    }

    protected override void OnStateFinish<LobbyStateResult>(LobbyStateResult lobbyStateResult)
    {
        SDL.SetCursor(SDL.CreateSystemCursor(SDL.SystemCursor.Pointer));
        base.OnStateFinish(lobbyStateResult);
    }
}