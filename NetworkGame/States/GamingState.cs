using System.Drawing;
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

    private const int WORLD_WIDTH = 1600;
    private const int WORLD_HEIGHT = 1600;

    private const int CAMERA_SPEED = 2000;

    private float _zoom = 1;
    private float _preferredZoom = 1;

    private TileEngine _backgroundTileEngine;
    private TileEngine _tileEngine;

    private float _cameraX;
    private float _cameraY;
    private Vector2 _cameraPosition => new Vector2(_cameraX, _cameraY);

    private PlayerData _playerData;
    private Dictionary<string, PlayerData> _peersData;

    public GamingState(StateResult previousStateResult, int worldSeed) : base(previousStateResult)
    {
        _playerData = previousStateResult.PlayerData;
        _peersData = previousStateResult.PeerData!;

        _backgroundTileEngine = new(WORLD_WIDTH, WORLD_HEIGHT, 1);
        _backgroundTileEngine.GenerateWorld(worldSeed);

        _tileEngine = new(WORLD_WIDTH, WORLD_HEIGHT, 1);
        //_tileEngine.GenerateOres(worldSeed);
    }

    public override void Update(double deltaTime)
    {
        if (_preferredZoom != _zoom)
        {
            Vector2 beforeZoomMouseWorldPos = (InputHandler.MousePosition / _zoom) + _cameraPosition;

            _zoom = MathHelper.Lerp(_zoom, _preferredZoom, 30 * (float)deltaTime);
            _backgroundTileEngine.SetZoom(_zoom);
            _tileEngine.SetZoom(_zoom);

            Vector2 afterZoomMouseWorldPos = (InputHandler.MousePosition / _zoom) + _cameraPosition;

            //_cameraX += beforeZoomMouseWorldPos.X - afterZoomMouseWorldPos.X;
            //_cameraY += beforeZoomMouseWorldPos.Y - afterZoomMouseWorldPos.Y;
        }

        Vector2 movementVector = new();
        if (InputHandler.IsKeyDown(SDL.Keycode.D)) movementVector.X += 1;
        if (InputHandler.IsKeyDown(SDL.Keycode.A)) movementVector.X -= 1;
        if (InputHandler.IsKeyDown(SDL.Keycode.W)) movementVector.Y -= 1;
        if (InputHandler.IsKeyDown(SDL.Keycode.S)) movementVector.Y += 1;
        _playerData.X += movementVector.X * CAMERA_SPEED * (float)deltaTime;
        _playerData.Y += movementVector.Y * CAMERA_SPEED * (float)deltaTime;

        _cameraX = _playerData.X - (App.WindowWidth / 2 / _zoom);
        _cameraY = _playerData.Y - (App.WindowHeight / 2 / _zoom);

        if (movementVector != Vector2.Zero)
        {
            _ = App.SendPacketUdp(new PositionUpdatePacket() { Type = "update_pos", Username = _playerData.Username, X = _playerData.X, Y = _playerData.Y });
        }

        if (InputHandler.ScrollWheelDelta != 0)
        {
            _preferredZoom += InputHandler.ScrollWheelDelta / 10;
            _preferredZoom = Math.Clamp(_preferredZoom, 0.2f, 2f);
        }

        if (InputHandler.IsMiddleMousePressed())
        {
            _preferredZoom = 1;
        }

        if (InputHandler.IsLeftMousePressed())
        {
            _tileEngine.PlaceTileAt(AssetManager.GetTexture("CoalTile"), InputHandler.MousePosition / _zoom + _cameraPosition);
        }

    }

    public override void Render(Renderer renderer)
    {
        renderer.Clear(Color.CornflowerBlue);

        _backgroundTileEngine.Render(renderer, _cameraPosition);
        _tileEngine.Render(renderer, _cameraPosition);

        renderer.RenderTexture(AssetManager.GetTexture("mogus"), (_playerData.Position - _cameraPosition) * _zoom, Color.White, _zoom);

        foreach (var peer in _peersData)
        {
            renderer.RenderTexture(AssetManager.GetTexture("mogus"), (peer.Value.Position - _cameraPosition) * _zoom, Color.White, _zoom);
        }
    }

    public override void ForwardPacket(Packet packet, string json)
    {
        switch (packet.Type)
        {
            case "update_pos":
                PositionUpdatePacket positionUpdatePacket = JsonSerializer.Deserialize<PositionUpdatePacket>(json)!;
                _peersData[positionUpdatePacket.Username].X = positionUpdatePacket.X;
                _peersData[positionUpdatePacket.Username].Y = positionUpdatePacket.Y;
                break;
        }

        base.ForwardPacket(packet, json);
    }
}