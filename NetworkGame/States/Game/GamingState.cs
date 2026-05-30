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

    private const int WORLD_WIDTH = 800;
    private const int WORLD_HEIGHT = 800;

    private const int CAMERA_SPEED = 1000;

    private float _zoom = 1;
    private float _preferredZoom = 1;

    private TileEngine _backgroundTileEngine;

    private float _cameraX;
    private float _cameraY;
    private Vector2 _cameraPosition => new Vector2(_cameraX, _cameraY);

    private PlayerData _playerData;
    private Dictionary<string, PlayerData>? _peersData;

    private List<Entity> _entities = new();

    private ComponentManager _componentManager = new();
    private List<ISystem> _systems = new();

    public GamingState(StateResult previousStateResult, int worldSeed) : base(previousStateResult)
    {
        _playerData = previousStateResult.PlayerData;
        _peersData = previousStateResult.PeerData;

        _backgroundTileEngine = new(WORLD_WIDTH, WORLD_HEIGHT, 1);
        _backgroundTileEngine.GenerateWorld(worldSeed);
    }

    public override void Update(double deltaTime)
    {
        if (_preferredZoom != _zoom)
        {
            _zoom = MathHelper.Lerp(_zoom, _preferredZoom, 30 * (float)deltaTime);
            _backgroundTileEngine.SetZoom(_zoom);
        }

        Vector2 movementVector = new();
        if (InputHandler.IsKeyDown(SDL.Keycode.D)) movementVector.X += 1;
        if (InputHandler.IsKeyDown(SDL.Keycode.A)) movementVector.X -= 1;
        if (InputHandler.IsKeyDown(SDL.Keycode.W)) movementVector.Y -= 1;
        if (InputHandler.IsKeyDown(SDL.Keycode.S)) movementVector.Y += 1;
        _playerData.X += movementVector.X * CAMERA_SPEED * (float)deltaTime;
        _playerData.Y += movementVector.Y * CAMERA_SPEED * (float)deltaTime;

        Texture2D playerTexture = AssetManager.GetTexture("mogus");
        _cameraX = _playerData.X - (App.WindowWidth / 2 / _zoom) + (playerTexture.Width / 2);
        _cameraY = _playerData.Y - (App.WindowHeight / 2 / _zoom) + (playerTexture.Height / 2);

        if (movementVector != Vector2.Zero)
        {
            if (Program.TCPOnly)
                _ = App.SendPacketTcp(new PositionUpdatePacket() { Type = "update_pos", Username = _playerData.Username, X = _playerData.X, Y = _playerData.Y });
            else
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
    }

    public override void Render(Renderer renderer)
    {
        renderer.Clear(Color.CornflowerBlue);

        _backgroundTileEngine.Render(renderer, _cameraPosition);

        foreach (Entity entity in _entities)
        {
            TextureComponent? textureComponent = _componentManager.Query<TextureComponent>(entity.Id);
            PositionComponent? positionComponent = _componentManager.Query<PositionComponent>(entity.Id);

            if (textureComponent != null && positionComponent != null)
            {
                renderer.RenderTexture(textureComponent.Texture, (positionComponent.Position - _cameraPosition) * _zoom, Color.White, textureComponent.Scale * _zoom);
            }
        }

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
        }

        base.ForwardPacket(packet, json);
    }
}