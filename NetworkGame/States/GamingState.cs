using System.Drawing;
using System.Numerics;
using SDL3;
using Smash;
using Smash.Graphics;
using Smash.Input;

public class GamingState : GameState
{
    private const int TILE_WIDTH = 16;
    private const int TILE_HEIGHT = 16;

    private const int WORLD_WIDTH = 200;
    private const int WORLD_HEIGHT = 200;

    private const int CAMERA_SPEED = 500;

    private float _zoom = 1;
    private float _preferredZoom = 1;

    private TileEngine _tileEngine;

    private float _previousWindowWidth;
    private float _previousWindowHeight;

    private float _cameraX;
    private float _cameraY;
    private Vector2 _cameraPosition => new Vector2(_cameraX, _cameraY);

    public GamingState(StateResult previousStateResult, int worldSeed) : base(previousStateResult)
    {
        _tileEngine = new(TILE_WIDTH, TILE_HEIGHT, WORLD_WIDTH, WORLD_HEIGHT, 1);
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

        if (App.WindowWidth != _previousWindowWidth)
        {
            _tileEngine.RecalculateArrayBounds();
            _previousWindowWidth = App.WindowWidth;
        }

        if (App.WindowHeight != _previousWindowHeight)
        {
            _tileEngine.RecalculateArrayBounds();
            _previousWindowHeight = App.WindowHeight;
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
            _preferredZoom = Math.Max(_preferredZoom, 0.1f);
        }
    }

    public override void Render(Renderer renderer)
    {
        renderer.Clear(Color.CornflowerBlue);

        _tileEngine.Render(renderer, _cameraPosition);
    }
}