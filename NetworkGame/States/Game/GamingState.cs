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

    private const float MIN_ZOOM = 0.1f;

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

    private Vector2 _mouseWorldPos => (InputHandler.MousePosition / _zoom) + _cameraPosition;

    private PlayerData _playerData;
    private Dictionary<byte, PlayerData>? _peersData = new();

    private bool _chatFocused = false;
    private float _chatCooldown;
    private string _chatMessage = "";

    private float _elapsedTime;
    private long _ramUsage;

    private List<(string username, string message)> _chatHistory = new();

    public GamingState(StateResult previousStateResult, int worldSeed) : base(previousStateResult)
    {
        _playerData = previousStateResult.PlayerData;
        _peersData = previousStateResult.PeerData;

        if (previousStateResult is LobbyStateResult lobbyStateResult)
        {
            _chatHistory = lobbyStateResult.ChatHistory;
        }

        _backgroundTileEngine = new(WORLD_WIDTH, WORLD_HEIGHT, 1, worldSeed, true);
        _tileEngine = new(WORLD_WIDTH, WORLD_HEIGHT, 1, worldSeed, false);

        _tileEngine.UpdateVisibleChunks(Vector2.Zero, 1);
        _backgroundTileEngine.UpdateVisibleChunks(Vector2.Zero, 1);
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

        Texture2D playerTexture = AssetManager.Get<Texture2D>("mogus");
        _cameraX = _playerData.X - (App.WindowWidth / 2 / _zoom) + (playerTexture.Width / 2);
        _cameraY = _playerData.Y - (App.WindowHeight / 2 / _zoom) + (playerTexture.Height / 2);

        if (movementVector != Vector2.Zero)
        {
            byte[] positionUpdatePacket = new PositionUpdatePacket(null) { X = _playerData.X, Y = _playerData.Y }.Serialize();
            if (Program.TCPOnly)
                _ = App.SendPacketTcp(positionUpdatePacket);
            else
                _ = App.SendPacketUdp(positionUpdatePacket);


            _backgroundTileEngine.UpdateVisibleChunks(_cameraPosition, _preferredZoom);
            _tileEngine.UpdateVisibleChunks(_cameraPosition, _preferredZoom);
        }

        if (InputHandler.ScrollWheelDelta != 0)
        {
            float previousZoom = _preferredZoom;

            _preferredZoom += InputHandler.ScrollWheelDelta / (20 / _zoom);
            _preferredZoom = Math.Clamp(_preferredZoom, MIN_ZOOM, 2f);

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
            int textureId = AssetManager.GetAssetId("CoalTile");
            if (_tileEngine.PlaceTile(AssetManager.Get<Texture2D>(textureId), _mouseWorldPos))
            {
                _ = App.SendPacketTcp(new PlaceTilePacket(null) { X = (int)_mouseWorldPos.X, Y = (int)_mouseWorldPos.Y, TextureId = textureId }.Serialize());
            }
        }

        if (InputHandler.IsRightMouseDown())
        {
            if (_tileEngine.RemoveTile(_mouseWorldPos))
            {
                _ = App.SendPacketTcp(new RemoveTilePacket(null) { X = (int)_mouseWorldPos.X, Y = (int)_mouseWorldPos.Y }.Serialize());
            }
        }
    }

    public override void Render(Renderer renderer)
    {
        renderer.Clear(Color.CornflowerBlue);

        _backgroundTileEngine.Render(renderer, _cameraPosition);
        _tileEngine.Render(renderer, _cameraPosition);

        (int x, int y) selectorPos = TileEngine.AlignToGrid((int)_mouseWorldPos.X, (int)_mouseWorldPos.Y);
        renderer.RenderTexture(AssetManager.Get<Texture2D>("Selector"), (new Vector2(selectorPos.x, selectorPos.y) - _cameraPosition) * _zoom, Color.White, _zoom);

        renderer.RenderTexture(AssetManager.Get<Texture2D>("mogus"), (_playerData.Position - _cameraPosition) * _zoom, Color.White, _zoom);

        if (_peersData != null)
        {
            foreach (var peer in _peersData)
            {
                renderer.RenderTexture(AssetManager.Get<Texture2D>("mogus"), (peer.Value.Position - _cameraPosition) * _zoom, Color.White, _zoom);
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
        renderer.RenderText(App.Font, $"Chunks: {_backgroundTileEngine.GetChunkAmount() + _tileEngine.GetChunkAmount()}", new Vector2(20, 60), Color.White);
        renderer.RenderText(App.Font, $"Visible Chunks: {_backgroundTileEngine.GetVisibleChunkAmount() + _tileEngine.GetVisibleChunkAmount()}", new Vector2(20, 100), Color.White);
    }

    public override void ForwardPacket(byte[] data)
    {
        data = data[4..data.Length];

        byte packetId = data[0];
        byte playerId = data[^1];

        switch (packetId)
        {
            case PacketId.PACKET_UPDATE_POS:
                PositionUpdatePacket positionUpdatePacket = new(data);
                _peersData![playerId].X = positionUpdatePacket.X;
                _peersData![playerId].Y = positionUpdatePacket.Y;
                break;

            case PacketId.PACKET_LEAVE:
                Console.WriteLine("Received leave packet from " + _peersData![playerId].Username);
                SendChatMessage("", $"{_peersData[playerId].Username} has left the game");
                _peersData!.Remove(playerId);
                break;

            case PacketId.PACKET_PING:
                PingPacket pingPacket = new(data);
                SendChatMessage(_peersData![playerId].Username, pingPacket.Message);
                _chatCooldown = CHAT_BASE_COOLDOWN;
                break;

            case PacketId.PACKET_UPDATE_HOST:
                UpdateHostPacket updateHostPacket = new(data);
                if (updateHostPacket.NewHostPlayerId == App.PlayerId) _playerData.Host = true;
                else _peersData![updateHostPacket.NewHostPlayerId].Host = true;
                break;

            case PacketId.PACKET_PLACE_TILE:
                PlaceTilePacket placeTilePacket = new(data);
                _tileEngine.PlaceTile(AssetManager.Get<Texture2D>(placeTilePacket.TextureId), new Vector2(placeTilePacket.X, placeTilePacket.Y));
                break;

            case PacketId.PACKET_REMOVE_TILE:
                RemoveTilePacket removeTilePacket = new(data);
                _tileEngine.RemoveTile(new Vector2(removeTilePacket.X, removeTilePacket.Y));
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
