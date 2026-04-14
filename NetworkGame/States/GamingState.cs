using System.Drawing;
using System.Numerics;
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

    private TileEngine _tileEngine;

    private float _cameraX;
    private float _cameraY;
    private Vector2 _cameraPosition => new Vector2(_cameraX, _cameraY);

    public GamingState(StateResult previousStateResult, int worldSeed) : base(previousStateResult)
    {
        _tileEngine = new(WORLD_WIDTH, WORLD_HEIGHT, 1);
        _tileEngine.GenerateWorld(worldSeed);
    }

    public override void Update(double deltaTime)
    {
        if (_preferredZoom != _zoom)
        {
            Vector2 beforeZoomMouseWorldPos = (InputHandler.MousePosition / _zoom) + _cameraPosition;

            _zoom = MathHelper.Lerp(_zoom, _preferredZoom, 30 * (float)deltaTime);
            _tileEngine.SetZoom(_zoom);

            Vector2 afterZoomMouseWorldPos = (InputHandler.MousePosition / _zoom) + _cameraPosition;

            _cameraX += beforeZoomMouseWorldPos.X - afterZoomMouseWorldPos.X;
            _cameraY += beforeZoomMouseWorldPos.Y - afterZoomMouseWorldPos.Y;
        }

        Vector2 movementVector = new();
        if (InputHandler.IsKeyDown(SDL.Keycode.D)) movementVector.X += 1;
        if (InputHandler.IsKeyDown(SDL.Keycode.A)) movementVector.X -= 1;
        if (InputHandler.IsKeyDown(SDL.Keycode.W)) movementVector.Y -= 1;
        if (InputHandler.IsKeyDown(SDL.Keycode.S)) movementVector.Y += 1;
        _cameraX += movementVector.X * CAMERA_SPEED * (float)deltaTime;
        _cameraY += movementVector.Y * CAMERA_SPEED * (float)deltaTime;

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

        _tileEngine.Render(renderer, _cameraPosition);
        renderer.RenderText(App.Font, _zoom.ToString(), new Vector2(20), Color.White);
    }
}