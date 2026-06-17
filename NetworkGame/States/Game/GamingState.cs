using Color = System.Drawing.Color;
using System.Numerics;
using System.Text.Json;
using SDL3;
using Smash;
using Smash.Graphics;
using Smash.Input;
using System.Diagnostics;

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

    private float _elapsedTime;
    private long _ramUsage;

    private Vector2 _lastSentMovePacketLocation = new();

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

        _backgroundTileEngine = new(WORLD_WIDTH, WORLD_HEIGHT, 1, worldSeed, true);
        _tileEngine = new(WORLD_WIDTH, WORLD_HEIGHT, 1, worldSeed, false);

        _tileEngine.UpdateVisibleChunks(new Vector2(), 1);
        _backgroundTileEngine.UpdateVisibleChunks(new Vector2(), 1);
    }

    public override void Update(double deltaTime)
    {
        _elapsedTime += (float)deltaTime;
        if (_elapsedTime > 2f)
        {
            Process currentProcess = Process.GetCurrentProcess();
            _ramUsage = currentProcess.WorkingSet64 / 1024 / 1024;
            _elapsedTime = 0;
        }

        if (_preferredZoom != _zoom)
        {
            _zoom = _preferredZoom;

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
                    _ = App.SendPacketTcp(new PingPacket(null) { Message = _chatMessage }.Serialize());
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

        float sprintModifier = InputHandler.IsKeyDown(SDL.Keycode.LShift) ? 1200 : 0;
        _playerData.X += movementVector.X * (CAMERA_SPEED + sprintModifier) * (float)deltaTime;
        _playerData.Y += movementVector.Y * (CAMERA_SPEED + sprintModifier) * (float)deltaTime;

        Texture2D playerTexture = AssetManager.GetTexture("mogus");
        _cameraX = _playerData.X - (App.WindowWidth / 2 / _zoom) + (playerTexture.Width / 2);
        _cameraY = _playerData.Y - (App.WindowHeight / 2 / _zoom) + (playerTexture.Height / 2);

        if (movementVector != Vector2.Zero && _cameraPosition != _lastSentMovePacketLocation)
        {
            //if (Program.TCPOnly)
            //    _ = App.SendPacketTcp(PacketId.PACKET_UPDATE_POS, new PositionUpdatePacket() { Username = _playerData.Username, X = _playerData.X, Y = _playerData.Y });
            //else
            //    _ = App.SendPacketUdp(PacketId.PACKET_UPDATE_POS, new PositionUpdatePacket() { Username = _playerData.Username, X = _playerData.X, Y = _playerData.Y });

            _ = App.SendPacketTcp(new PositionUpdatePacket(null) {  X = _playerData.X, Y = _playerData.Y }.Serialize() );
            _lastSentMovePacketLocation = _cameraPosition;

            _backgroundTileEngine.UpdateVisibleChunks(_cameraPosition, _preferredZoom);
            _tileEngine.UpdateVisibleChunks(_cameraPosition, _preferredZoom);
        }

        if (InputHandler.ScrollWheelDelta != 0)
        {
            float previousZoom = _preferredZoom;

            _preferredZoom += InputHandler.ScrollWheelDelta / (20 / _zoom);
            _preferredZoom = Math.Clamp(_preferredZoom, 0.1f, 2f);

            float greaterZoom = Math.Min(previousZoom, _preferredZoom);
            _tileEngine.UpdateVisibleChunks(_cameraPosition, greaterZoom);
            _backgroundTileEngine.UpdateVisibleChunks(_cameraPosition, greaterZoom);
        }

        if (InputHandler.IsMiddleMousePressed())
        {
            _preferredZoom = 1.5f;

            _tileEngine.UpdateVisibleChunks(_cameraPosition, _preferredZoom);
            _backgroundTileEngine.UpdateVisibleChunks(_cameraPosition, _preferredZoom);
        }

        if (InputHandler.IsLeftMouseDown())
        {
            Vector2 position = InputHandler.MousePosition / _zoom + _cameraPosition;
            if (_tileEngine.PlaceTile(AssetManager.GetTexture("CoalTile"), position))
            {
                //_ = App.SendPacketTcp(PacketId.PACKET_PLACE_TILE, new PlaceTilePacket() { Username = _playerData.Username, TextureName = "CoalTile", X = position.X, Y = position.Y});
            }
        }

        if (InputHandler.IsRightMouseDown())
        {
            Vector2 mousePos = _mouseWorldPos;
            if (_tileEngine.RemoveTile(mousePos))
            {
                //_ = App.SendPacketTcp(PacketId.PACKET_REMOVE_TILE, new RemoveTilePacket() { Username = _playerData.Username, X = mousePos.X, Y = mousePos.Y });
            }
        }
    }

    public override void Render(Renderer renderer)
    {
        renderer.Clear(Color.CornflowerBlue);

        _backgroundTileEngine.Render(renderer, _cameraPosition);
        _tileEngine.Render(renderer, _cameraPosition);

        (int x, int y) selectorPos = TileEngine.AlignToGrid((int)_mouseWorldPos.X, (int)_mouseWorldPos.Y);
        renderer.RenderTexture(AssetManager.GetTexture("Selector"), (new Vector2(selectorPos.x, selectorPos.y) - _cameraPosition) * _zoom, Color.White, _zoom);

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

        renderer.RenderText(App.Font, $"RAM: {_ramUsage} MB", new Vector2(20, 20), Color.White);
        renderer.RenderText(App.Font, $"Chunks: {_backgroundTileEngine.GetChunkAmount()}", new Vector2(20, 60), Color.White);
        renderer.RenderText(App.Font, $"Visible Chunks: {_backgroundTileEngine.GetVisibleChunkAmount()}", new Vector2(20, 100), Color.White);
    }

    public override void ForwardPacket(byte[] data)
    {
        data = data[4..data.Length];

        byte packetId = data[0];
        byte playerId = data[^1];

        string? playerName = null;

        foreach (PlayerData playerData in _peersData!.Values)
            if (playerData.Id == playerId) playerName = playerData.Username;

        switch (packetId)
        {
            case PacketId.PACKET_UPDATE_POS:
                PositionUpdatePacket positionUpdatePacket = new(data);
                _peersData![playerName!].X = positionUpdatePacket.X;
                _peersData![playerName!].Y = positionUpdatePacket.Y;
                break;

            case PacketId.PACKET_LEAVE:
                Console.WriteLine("Received leave packet from " + playerName);
                SendChatMessage("", $"{playerName} has left the game");
                _peersData!.Remove(playerName!);
                break;

            case PacketId.PACKET_PING:
                PingPacket pingPacket = new(data);
                SendChatMessage(playerName!, pingPacket.Message);
                _chatCooldown = CHAT_BASE_COOLDOWN;
                break;
        }

        base.ForwardPacket(data);
    }

    private void SendChatMessage(string username, string message)
    {
        _chatHistory.Add((username, message));
        _chatCooldown = CHAT_BASE_COOLDOWN;
    }
}
