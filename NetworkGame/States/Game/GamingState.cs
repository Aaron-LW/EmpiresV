using Color = System.Drawing.Color;
using System.Numerics;
using System.Text.Json;
using SDL3;
using Smash;
using Smash.Graphics;
using Smash.Input;

public class GamingState : GameState
{
    public const int TILE_WIDTH = 16;
    public const int TILE_HEIGHT = 16;

    private const int WORLD_WIDTH = 500;
    private const int WORLD_HEIGHT = 500;

    private const int CAMERA_SPEED = 1000;

    private const int CHAT_LINE_SPACING = 30;
    private const int CHAT_BASE_COOLDOWN = 4;
    private const int CHAT_MAX_MESSAGES = 10;
    private const int CHAT_BAR_HEIGHT = 50;
    private const int CHAT_OPACITY = 180;

    private float _zoom = 1;
    private float _preferredZoom = 1;

    private TileEngine _backgroundTileEngine;
    private TileEngine _tileEngine;

    private float _cameraX;
    private float _cameraY;
    private Vector2 _cameraPosition => new Vector2(_cameraX, _cameraY);

    private Vector2 _mouseWorldPos => InputHandler.MousePosition / _zoom + _cameraPosition;

    private PlayerData _playerData;
    private Dictionary<string, PlayerData>? _peersData;

    private bool _chatFocused = false;
    private float _chatCooldown;
    private string _chatMessage = "";

    private List<(string username, string message)> _chatHistory = new();

    public GamingState(StateResult previousStateResult, int worldSeed) : base(previousStateResult)
    {
        _playerData = previousStateResult.PlayerData;
        _peersData = previousStateResult.PeerData;

        if (previousStateResult is LobbyStateResult lobbyStateResult)
        {
            _chatHistory = lobbyStateResult.ChatHistory;
            _chatCooldown = CHAT_BASE_COOLDOWN;
        }

        _backgroundTileEngine = new(WORLD_WIDTH, WORLD_HEIGHT, 1, worldSeed);
        //_backgroundTileEngine.GenerateWorld();

        _tileEngine = new(WORLD_WIDTH, WORLD_HEIGHT, 1, worldSeed);


        _backgroundTileEngine.UpdateVisibleChunks(_cameraPosition, _preferredZoom);
        _tileEngine.UpdateVisibleChunks(_cameraPosition, _preferredZoom);
    }

    public override void Update(double deltaTime)
    {
        if (_preferredZoom != _zoom)
        {
            _zoom = MathHelper.Lerp(_zoom, _preferredZoom, 30 * (float)deltaTime);
            _backgroundTileEngine.SetZoom(_zoom);
            _tileEngine.SetZoom(_zoom);

        }

        if (!_chatFocused)
            _chatCooldown -= (float)deltaTime;


        Vector2 movementVector = new();

        if (!_chatFocused)
        {
            if (InputHandler.IsKeyDown(SDL.Keycode.D)) movementVector.X += 1;
            if (InputHandler.IsKeyDown(SDL.Keycode.A)) movementVector.X -= 1;
            if (InputHandler.IsKeyDown(SDL.Keycode.W)) movementVector.Y -= 1;
            if (InputHandler.IsKeyDown(SDL.Keycode.S)) movementVector.Y += 1;

            if (InputHandler.IsKeyPressed(SDL.Keycode.Return))
            {
                _chatFocused = true;     
                _chatCooldown = CHAT_BASE_COOLDOWN;
            }
        }
        else
        {
            _chatMessage += InputHandler.TextInput;
            if (InputHandler.IsKeyPressed(SDL.Keycode.Return))
            {
                if (_chatMessage != string.Empty)
                {
                    _ = App.SendPacketTcp(PacketId.PACKET_PING, new PingPacket() { Username = _playerData.Username, Message = _chatMessage });
                    SendChatMessage(_playerData.Username, _chatMessage);

                    _chatMessage = "";
                }

                _chatFocused = false;
            }

            if (InputHandler.IsKeyPressed(SDL.Keycode.Backspace))
            {
                if (InputHandler.IsKeyDown(SDL.Keycode.LCtrl))
                {
                    _chatMessage = "";
                }

                if (_chatMessage.Length > 0)
                {
                    _chatMessage = _chatMessage.Remove(_chatMessage.Length - 1, 1);
                }
            }
        }

        _playerData.X += movementVector.X * CAMERA_SPEED * (float)deltaTime;
        _playerData.Y += movementVector.Y * CAMERA_SPEED * (float)deltaTime;

        Texture2D playerTexture = AssetManager.GetTexture("mogus");
        _cameraX = _playerData.X - (App.WindowWidth / 2 / _zoom) + (playerTexture.Width / 2);
        _cameraY = _playerData.Y - (App.WindowHeight / 2 / _zoom) + (playerTexture.Height / 2);

        if (movementVector != Vector2.Zero)
        {
            if (Program.TCPOnly)
                _ = App.SendPacketTcp(PacketId.PACKET_UPDATE_POS, new PositionUpdatePacket() { Username = _playerData.Username, X = _playerData.X, Y = _playerData.Y });
            else
                _ = App.SendPacketUdp(PacketId.PACKET_UPDATE_POS, new PositionUpdatePacket() { Username = _playerData.Username, X = _playerData.X, Y = _playerData.Y });

            _backgroundTileEngine.UpdateVisibleChunks(_cameraPosition, _preferredZoom);
            _tileEngine.UpdateVisibleChunks(_cameraPosition, _preferredZoom);
        }

        if (InputHandler.ScrollWheelDelta != 0)
        {
            _preferredZoom += InputHandler.ScrollWheelDelta / (20 / _zoom);
            _preferredZoom = Math.Clamp(_preferredZoom, 0.08f, 2f);

            _tileEngine.UpdateVisibleChunks(_cameraPosition, _preferredZoom);
            _backgroundTileEngine.UpdateVisibleChunks(_cameraPosition, _preferredZoom);
        }

        if (InputHandler.IsMiddleMousePressed())
        {
            _preferredZoom = 1.5f;
        }

        if (InputHandler.IsLeftMouseDown())
        {
            Vector2 position = InputHandler.MousePosition / _zoom + _cameraPosition;
            if (_tileEngine.PlaceTile(AssetManager.GetTexture("CoalTile"), position))
            {
                _ = App.SendPacketTcp(PacketId.PACKET_PLACE_TILE, new PlaceTilePacket() { Username = _playerData.Username, TextureName = "CoalTile", X = position.X, Y = position.Y});
            }

            _backgroundTileEngine.PlaceChunk(_mouseWorldPos);
        }

        if (InputHandler.IsRightMouseDown())
        {
            Vector2 mousePos = _mouseWorldPos;
            if (_tileEngine.RemoveTile(mousePos))
            {
                _ = App.SendPacketTcp(PacketId.PACKET_REMOVE_TILE, new RemoveTilePacket() { Username = _playerData.Username, X = mousePos.X, Y = mousePos.Y });
            }
        }
    }

    public override void Render(Renderer renderer)
    {
        renderer.Clear(Color.CornflowerBlue);

        _backgroundTileEngine.Render(renderer, _cameraPosition);
        _tileEngine.Render(renderer, _cameraPosition);

        renderer.RenderTexture(AssetManager.GetTexture("Selector"), (TileEngine.AlignToGrid(_mouseWorldPos) - _cameraPosition) * _zoom, Color.White, _zoom);

        renderer.RenderTexture(AssetManager.GetTexture("mogus"), (_playerData.Position - _cameraPosition) * _zoom, Color.White, _zoom);

        if (_peersData != null)
        {
            foreach (var peer in _peersData)
            {
                renderer.RenderTexture(AssetManager.GetTexture("mogus"), (peer.Value.Position - _cameraPosition) * _zoom, Color.White, _zoom);
            }
        }

        Vector2 chatStartPos = new Vector2(0, App.WindowHeight / 2);
        if (_chatCooldown > 0 && _chatHistory.Count > 0)
        {
            Rectangle chatRectangle = new(chatStartPos - new Vector2(0, 5), Math.Max(App.WindowWidth / 3f, 400), Math.Min(_chatHistory.Count, CHAT_MAX_MESSAGES) * CHAT_LINE_SPACING + 10);
            renderer.RenderFilledRectangle(chatRectangle, Color.FromArgb(CHAT_OPACITY, 0, 0, 0));

            int startIndex = Math.Max(_chatHistory.Count - CHAT_MAX_MESSAGES, 0);
            for (int i = startIndex; i < _chatHistory.Count; i++)
            {
                Vector2 textPosition = Vector2.Round(chatStartPos + new Vector2(10, (i - startIndex) * CHAT_LINE_SPACING));

                if (_chatHistory[i].username != string.Empty)
                    renderer.RenderText(App.Font, $"<{_chatHistory[i].username}>  {_chatHistory[i].message}", textPosition, Color.White);
                else
                    renderer.RenderText(App.Font, $"{_chatHistory[i].message}", textPosition, Color.MediumSpringGreen);

            }
        }

        if (_chatFocused)
        {
            renderer.RenderFilledRectangle(new Rectangle(0, App.WindowHeight - CHAT_BAR_HEIGHT, App.WindowWidth, CHAT_BAR_HEIGHT), Color.FromArgb(CHAT_OPACITY, 0, 0, 0));
            renderer.RenderText(App.Font, _chatMessage, new Vector2(10, App.WindowHeight - CHAT_BAR_HEIGHT) + new Vector2(0, CHAT_BAR_HEIGHT / 2) - new Vector2(0, App.Font.MeasureString(_chatMessage).Y / 2), Color.White);
        }

        Vector2 tilePosition = TileEngine.AlignToGrid(_mouseWorldPos);
        renderer.RenderText(App.Font, tilePosition.ToString(), new Vector2(20), Color.White);
    }

    public override void ForwardPacket(byte id, string json)
    {
        switch (id)
        {
            case PacketId.PACKET_UPDATE_POS:
                PositionUpdatePacket positionUpdatePacket = JsonSerializer.Deserialize<PositionUpdatePacket>(json)!;
                _peersData![positionUpdatePacket.Username].X = positionUpdatePacket.X;
                _peersData![positionUpdatePacket.Username].Y = positionUpdatePacket.Y;
                break;

            case PacketId.PACKET_LEAVE:
                LeavePacket leavePacket = JsonSerializer.Deserialize<LeavePacket>(json)!;
                Console.WriteLine("Received leave packet from " + leavePacket.Username);
                SendChatMessage("", $"{leavePacket.Username} has left the game");
                _peersData!.Remove(leavePacket.Username);
                break;

            case PacketId.PACKET_PING:
                PingPacket pingPacket = JsonSerializer.Deserialize<PingPacket>(json)!;
                SendChatMessage(pingPacket.Username, pingPacket.Message);
                _chatCooldown = CHAT_BASE_COOLDOWN;
                break;

            case PacketId.PACKET_PLACE_TILE:
                PlaceTilePacket placeTilePacket = JsonSerializer.Deserialize<PlaceTilePacket>(json)!;
                _tileEngine.PlaceTile(AssetManager.GetTexture(placeTilePacket.TextureName), new Vector2(placeTilePacket.X, placeTilePacket.Y));
                break;

            case PacketId.PACKET_REMOVE_TILE:
                RemoveTilePacket removeTilePacket = JsonSerializer.Deserialize<RemoveTilePacket>(json)!;
                _tileEngine.RemoveTile(new Vector2(removeTilePacket.X, removeTilePacket.Y));
                break;
        }

        base.ForwardPacket(id, json);
    }

    private void SendChatMessage(string username, string message)
    {
        _chatHistory.Add((username, message));
        _chatCooldown = CHAT_BASE_COOLDOWN;
    }
}