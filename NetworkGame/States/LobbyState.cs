using Color = System.Drawing.Color;
using Smash.Graphics;
using System.Numerics;
using Smash;
using Smash.Input;
using SDL3;

public class LobbyState : GameState
{
    private Texture2D _hostCrown;

    private PlayerData _userPlayerData;
    private Dictionary<byte, PlayerData> _playerData = new();
    private readonly object _playerDataLock = new();

    private InputField _chatInputField;
    private Rectangle _chatRectangle => new Rectangle(_chatInputField.Position + new Vector2(0, _chatInputField.Height), App.WindowWidth, App.WindowHeight - (_chatInputField.Y + _chatInputField.Height));

    private List<(string username, string message)> _chat = new();
    private float _chatScroll = 0;
    private float _preferredChatScroll = 0;

    private Button? _startGameButton;

    public LobbyState(StateResult previousStateResult) : base(previousStateResult)
    {
        _hostCrown = AssetManager.Get<Texture2D>("HostCrown");

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

        _ = App.SendPacketTcp([PacketId.PACKET_REQUEST_DATA]);
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
                PingPacket pingPacket = new PingPacket(null) { Message = _chatInputField.Text };
                _ = App.SendPacketTcp(pingPacket.Serialize());

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

        List<PlayerData> playerData = new();
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
            
            if (_chat[i].username != string.Empty)
                renderer.RenderText(App.Font, $"{_chat[i].username}: {_chat[i].message}", Vector2.Round(position), Color.White);
            else
                renderer.RenderText(App.Font, $"{_chat[i].message}", Vector2.Round(position), Color.MediumSpringGreen);

        }

        SDL.SetRenderClipRect(renderer.Handle, 0);
    }

    public override void ForwardPacket(byte[] data)
    {
        data = data[4..data.Length];

        byte packetId = data[0];
        byte playerId = data[^1];

        switch (packetId)
        {
            case PacketId.PACKET_JOIN:
                JoinPacket joinPacket = new(data);
                lock (_playerDataLock) { _playerData!.Add(playerId, new PlayerData() { Username = joinPacket.Username! }); }
                Console.WriteLine("Received join packet from " + joinPacket.Username);
                _chat.Add(("", $"{joinPacket.Username} has joined the game"));
                _preferredChatScroll = Math.Min(-_chat.Count * 40 + _chatRectangle.Height - 25, 0);
                break;

            case PacketId.PACKET_PLAYERDATA:
                PlayerDataPacket playerDataPacket = new(data);
                lock (_playerDataLock) _playerData![playerId] = playerDataPacket.PlayerData!;
                Console.WriteLine("Received player data packet from " + playerDataPacket.PlayerData!.Username);
                break;

            case PacketId.PACKET_PING:
                PingPacket pingPacket = new(data);
                _chat.Add((_playerData[playerId].Username, pingPacket.Message));
                _preferredChatScroll = Math.Min(-_chat.Count * 40 + _chatRectangle.Height - 25, 0);
                break;

            case PacketId.PACKET_LEAVE:
                Console.WriteLine("Received leave packet from " + _playerData[playerId].Username);
                _chat.Add(("", $"{_playerData[playerId].Username} has left the game"));
                _preferredChatScroll = Math.Min(-_chat.Count * 40 + _chatRectangle.Height - 25, 0);
                lock (_playerDataLock) { _playerData!.Remove(playerId); }
                break;

            case PacketId.PACKET_START_GAME:
                StartGamePacket startGamePacket = new(data);
                OnStateFinish(new LobbyStateResult
                {
                    PlayerData = _userPlayerData,
                    PeerData = _playerData,
                    Type = "lobby",
                    Seed = startGamePacket.Seed,
                    ChatHistory = _chat
                });

                break;

            case PacketId.PACKET_UPDATE_HOST:
                UpdateHostPacket updateHostPacket = new(data);
                if (updateHostPacket.NewHostPlayerId == App.PlayerId)
                {
                    _userPlayerData.Host = true;
                    Console.WriteLine("Received update host packet; I am the new host");
                } 
                else 
                {
                    _playerData[updateHostPacket.NewHostPlayerId].Host = true;
                    Console.WriteLine("Received update host packet; new host is " + _playerData[updateHostPacket.NewHostPlayerId].Host);
                }
                break;
        }
        
        base.ForwardPacket(data);
    }

    protected override void OnStateFinish<LobbyStateResult>(LobbyStateResult lobbyStateResult)
    {
        SDL.SetCursor(SDL.CreateSystemCursor(SDL.SystemCursor.Pointer));
        base.OnStateFinish(lobbyStateResult);
    }
}