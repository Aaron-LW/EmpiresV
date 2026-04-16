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
    private Dictionary<string, PlayerData>? _peersData;

    private List<ITileSystem> _tileSystems = new();

    private Queue<PlaceTilePacket> _placeTileQueue = new();

    public GamingState(StateResult previousStateResult, int worldSeed) : base(previousStateResult)
    {
        _playerData = previousStateResult.PlayerData;
        _peersData = previousStateResult.PeerData;

        _backgroundTileEngine = new(WORLD_WIDTH, WORLD_HEIGHT, 1);
        _backgroundTileEngine.GenerateWorld(worldSeed);

        _tileEngine = new(WORLD_WIDTH, WORLD_HEIGHT, 1);
        //_tileEngine.GenerateOres(worldSeed);

        _tileSystems.Add(new CoolnessSystem());
    }

    public override void Update(double deltaTime)
    {
        if (_preferredZoom != _zoom)
        {
            _zoom = MathHelper.Lerp(_zoom, _preferredZoom, 30 * (float)deltaTime);
            _backgroundTileEngine.SetZoom(_zoom);
            _tileEngine.SetZoom(_zoom);
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

        while (_placeTileQueue.Count > 0)
        {
            PlaceTilePacket placeTilePacket = _placeTileQueue.Dequeue();
            _tileEngine.PlaceTileAt(AssetManager.GetTexture(placeTilePacket.TextureName), new Vector2(placeTilePacket.X, placeTilePacket.Y));
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
            Vector2 placePosition = InputHandler.MousePosition / _zoom + _cameraPosition;
            bool success = _tileEngine.PlaceTileAt(AssetManager.GetTexture("CoalTile"), placePosition);

            if (success)
            {
                _ = App.SendPacketTcp(new PlaceTilePacket() { Type = "place_tile", Username = _playerData.Username, TextureName = "CoalTile", X = placePosition.X, Y = placePosition.Y });
            }
        }

        foreach (ITileSystem tileSystem in _tileSystems)
        {
            tileSystem.Update(_tileEngine.ComponentManager, deltaTime);
        }
    }

    public override void Render(Renderer renderer)
    {
        renderer.Clear(Color.CornflowerBlue);

        _backgroundTileEngine.Render(renderer, _cameraPosition);
        _tileEngine.Render(renderer, _cameraPosition);

        renderer.RenderTexture(AssetManager.GetTexture("mogus"), (_playerData.Position - _cameraPosition) * _zoom, Color.White, _zoom);

        if (_peersData != null)
        {
            foreach (var peer in _peersData)
            {
                renderer.RenderTexture(AssetManager.GetTexture("mogus"), (peer.Value.Position - _cameraPosition) * _zoom, Color.White, _zoom);
            }
        }
    }

    public override void ForwardPacket(Packet packet, string json)
    {
        switch (packet.Type)
        {
            case "update_pos":
                PositionUpdatePacket positionUpdatePacket = JsonSerializer.Deserialize<PositionUpdatePacket>(json)!;
                _peersData![positionUpdatePacket.Username].X = positionUpdatePacket.X;
                _peersData![positionUpdatePacket.Username].Y = positionUpdatePacket.Y;
                break;

            case "place_tile":
                PlaceTilePacket placeTilePacket = JsonSerializer.Deserialize<PlaceTilePacket>(json)!;
                _placeTileQueue.Enqueue(placeTilePacket);
                break;
        }

        base.ForwardPacket(packet, json);
    }
}