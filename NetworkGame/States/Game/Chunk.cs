using System.Numerics;
using SDL3;
using Smash.Graphics;

public class Chunk
{
    public const int CHUNK_WIDHT = 48;
    public const int CHUNK_HEIGHT = 48;
    private int MAX_TILES => CHUNK_WIDHT * CHUNK_HEIGHT;

    public static int CHUNK_PIXEL_WIDTH => CHUNK_WIDHT * GamingState.TILE_WIDTH;
    public static int CHUNK_PIXEL_HEIGHT => CHUNK_HEIGHT * GamingState.TILE_HEIGHT;

    public bool Dirty { get; private set; } = true;
    public nint RenderTarget { get; private set; }

    private Tile[] _tiles = new Tile[CHUNK_WIDHT * CHUNK_HEIGHT];

    private SDL.Vertex[] _vertices = [];
    private int[] _indices = [];

    public Chunk()
    {
        FillBuffers();
    }

    public void RebuildChunk(Renderer renderer)
    {
        RecalculateVertices();
        RenderTarget = SDL.CreateTexture(renderer.Handle, SDL.PixelFormat.RGBA128Float, SDL.TextureAccess.Target, CHUNK_WIDHT * GamingState.TILE_WIDTH, CHUNK_HEIGHT * GamingState.TILE_HEIGHT);
        SDL.SetTextureScaleMode(RenderTarget, SDL.ScaleMode.Nearest);

        SDL.SetRenderTarget(renderer.Handle, RenderTarget);
        SDL.RenderGeometry(renderer.Handle, AssetManager.GetTexture("TextureAtlas").Handle, _vertices, _vertices.Length, _indices, _indices.Length);
        SDL.SetRenderTarget(renderer.Handle, IntPtr.Zero);

        Dirty = false;
    }

    public bool PlaceTileAt(Vector2 chunkPosition, Texture2D texture, float brightness)
    {
        int tileX = (int)MathF.Floor(chunkPosition.X / GamingState.TILE_WIDTH);
        int tileY = (int)MathF.Floor(chunkPosition.Y / GamingState.TILE_HEIGHT);
        if (_tiles[tileY * CHUNK_WIDHT + tileX] != null) return false;

        _tiles[tileY * CHUNK_WIDHT + tileX] = new Tile(tileX * GamingState.TILE_WIDTH, tileY * GamingState.TILE_HEIGHT, texture, brightness);
        Dirty = true;
        return true;
    }

    private void RecalculateVertices()
    {
        int drawnTiles = 0;

        Texture2D textureAtlas = AssetManager.GetTexture("TextureAtlas");

        float texWidth = 16f / (float)textureAtlas.Width;
        float texHeight = 16f / (float)textureAtlas.Height;

        for (int y = 0; y < CHUNK_HEIGHT; y++)
        {
            for (int x = 0; x < CHUNK_WIDHT; x++)
            {
                Tile? tile = _tiles[y * CHUNK_WIDHT + x];
                if (tile == null) continue;

                int baseIndex = drawnTiles * 4;

                //Positions
                _vertices[baseIndex + 0].Position.X = tile.X;
                _vertices[baseIndex + 0].Position.Y = tile.Y;

                _vertices[baseIndex + 1].Position.X = tile.X + GamingState.TILE_WIDTH;
                _vertices[baseIndex + 1].Position.Y = tile.Y;

                _vertices[baseIndex + 2].Position.X = tile.X + GamingState.TILE_WIDTH;
                _vertices[baseIndex + 2].Position.Y = tile.Y + GamingState.TILE_HEIGHT;

                _vertices[baseIndex + 3].Position.X = tile.X;
                _vertices[baseIndex + 3].Position.Y = tile.Y + GamingState.TILE_HEIGHT;

                SDL.FColor topVertexColor = new SDL.FColor { R = tile.Brightness, G = tile.Brightness, B = tile.Brightness, A = 1.0f };
                SDL.FColor bottomVertexColor = new SDL.FColor { R = tile.Brightness, G = tile.Brightness, B = tile.Brightness, A = 1.0f};

                //Color
                _vertices[baseIndex + 0].Color = topVertexColor;
                _vertices[baseIndex + 1].Color = topVertexColor;
                _vertices[baseIndex + 2].Color = bottomVertexColor;
                _vertices[baseIndex + 3].Color = bottomVertexColor;

                //TexCoords
                _vertices[baseIndex + 0].TexCoord = new SDL.FPoint { X = tile.TexX,            Y = tile.TexY};
                _vertices[baseIndex + 1].TexCoord = new SDL.FPoint { X = tile.TexX + texWidth, Y = tile.TexY};
                _vertices[baseIndex + 2].TexCoord = new SDL.FPoint { X = tile.TexX + texWidth, Y = tile.TexY + texHeight};
                _vertices[baseIndex + 3].TexCoord = new SDL.FPoint { X = tile.TexX,            Y = tile.TexY + texHeight};

                drawnTiles++;
            }
        }
    }

    private void FillBuffers()
    {
        _vertices = new SDL.Vertex[MAX_TILES * 4];
        _indices = new int[MAX_TILES * 6];

        for (int i = 0; i < MAX_TILES; i++)
        {
            int baseIndex = i * 4;
            _indices[i * 6 + 0] = baseIndex + 0;
            _indices[i * 6 + 1] = baseIndex + 1;
            _indices[i * 6 + 2] = baseIndex + 2;
            _indices[i * 6 + 3] = baseIndex + 0;
            _indices[i * 6 + 4] = baseIndex + 2;
            _indices[i * 6 + 5] = baseIndex + 3;
        }
    }
}