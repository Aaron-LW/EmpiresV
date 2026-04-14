using System.Drawing;
using System.Numerics;
using SDL3;
using Smash.Graphics;

public class TileEngine
{
    private readonly int _mapWidth;
    private readonly int _mapHeight;

    private float _zoom = 1;

    private Dictionary<Vector2, Chunk> _chunks = new();

    public TileEngine(int mapWidth, int mapHeight, float baseZoom)
    {
        _zoom = baseZoom;

        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
    }

    public void GenerateWorld(int seed)
    {
        FastNoiseLite noise = new(seed);
        noise.SetNoiseType(FastNoiseLite.NoiseType.Cellular);
        noise.SetCellularDistanceFunction(FastNoiseLite.CellularDistanceFunction.EuclideanSq);
        noise.SetCellularReturnType(FastNoiseLite.CellularReturnType.CellValue);
        noise.SetCellularJitter(0.5f);
        noise.SetFrequency(0.03f);

        for (int y = 0; y < _mapHeight; y++)
        {
            for (int x = 0; x < _mapWidth; x++)
            {
                float brightness = noise.GetNoise(x, y);
                if (brightness < 0.4f) brightness = 0.4f;
                if (brightness > 0.4f) brightness = 1f;

                Chunk chunk = GetOrCreateChunk(new Vector2(x * GamingState.TILE_WIDTH, y * GamingState.TILE_HEIGHT));

                int chunkX = x * GamingState.TILE_WIDTH % Chunk.CHUNK_PIXEL_WIDTH;
                int chunkY = y * GamingState.TILE_HEIGHT % Chunk.CHUNK_PIXEL_HEIGHT;

                chunk.PlaceTileAt(new Vector2(chunkX, chunkY), AssetManager.GetTexture("Grass"), brightness);
            }
        }
    }

    public void Render(Renderer renderer, Vector2 offset)
    {
        Vector2 startPos = offset + Vector2.One;

        float pixelsX = startPos.X + ((App.WindowWidth + Chunk.CHUNK_PIXEL_WIDTH * 2) / _zoom);
        float pixelsY = startPos.Y + ((App.WindowHeight + Chunk.CHUNK_PIXEL_HEIGHT * 2) / _zoom);

        for (float y = startPos.Y; y < pixelsY; y += Chunk.CHUNK_PIXEL_HEIGHT)
        {
            for (float x = startPos.X; x < pixelsX; x += Chunk.CHUNK_PIXEL_WIDTH)
            {
                Vector2 chunkPosition = AlignToChunkGrid(new Vector2(x, y));
                    
                if (_chunks.TryGetValue(chunkPosition, out Chunk? chunk))
                {
                    if (chunk.Dirty)
                    {
                        chunk.RebuildChunk(renderer);
                    }

                    renderer.RenderTexture(new Texture2D(chunk.RenderTarget, "lol"), (chunkPosition - offset) * _zoom, Color.White, _zoom);
                }
            }
        }
    }

    public void SetZoom(float newZoom)
    {
        _zoom = newZoom;
    }

    private Vector2 AlignToChunkGrid(Vector2 worldPosition)
    {
        return new Vector2((int)MathF.Floor(worldPosition.X / Chunk.CHUNK_PIXEL_WIDTH) * Chunk.CHUNK_PIXEL_WIDTH,
                           (int)MathF.Floor(worldPosition.Y / Chunk.CHUNK_PIXEL_HEIGHT) * Chunk.CHUNK_PIXEL_HEIGHT);
    }

    private Chunk GetOrCreateChunk(Vector2 worldPosition)
    {
        Vector2 chunkPosition = AlignToChunkGrid(worldPosition);

        if (_chunks.TryGetValue(chunkPosition, out Chunk? foundChunk))
        {
            return foundChunk;
        }

        _chunks.Add(chunkPosition, new Chunk());
        return _chunks[chunkPosition];
    }

    //private int RecalculateVertices(Vector2 offset)
    //{
    //    int startTileX = (int)offset.X / _tileWidht;
    //    int startTileY = (int)offset.Y / _tileHeight;

    //    int tilesX = ((int)App.WindowWidth / (int)(_tileWidht * _zoom)) + 2;
    //    int tilesY = ((int)App.WindowHeight / (int)(_tileHeight * _zoom)) + 2;

    //    int drawnTiles = 0;

    //    float tileWidth = _tileWidht * _zoom;
    //    float tileHeight = _tileHeight * _zoom;

    //    for (int y = startTileY; y < startTileY + tilesY; y++)
    //    {
    //        for (int x = startTileX; x < startTileX + tilesX; x++)
    //        {
    //            if (x >= _mapWidth || y >= _mapHeight || x < 0 || y < 0) continue;
    //            
    //            Tile? tile = _tiles[y * _mapWidth + x];
    //            if (tile == null) continue;

    //            int baseIndex = drawnTiles * 4;

    //            float xPos = (tile.X - offset.X) * _zoom;
    //            float yPos = (tile.Y - offset.Y) * _zoom;

    //            //Positions
    //            _vertices[baseIndex + 0].Position.X = xPos;
    //            _vertices[baseIndex + 0].Position.Y = yPos;

    //            _vertices[baseIndex + 1].Position.X = xPos + tileWidth;
    //            _vertices[baseIndex + 1].Position.Y = yPos;

    //            _vertices[baseIndex + 2].Position.X = xPos + tileWidth;
    //            _vertices[baseIndex + 2].Position.Y = yPos + tileHeight;

    //            _vertices[baseIndex + 3].Position.X = xPos;
    //            _vertices[baseIndex + 3].Position.Y = yPos + tileHeight;

    //            SDL.FColor topVertexColor = new SDL.FColor { R = tile.Brightness, G = tile.Brightness, B = tile.Brightness, A = 1.0f };
    //            SDL.FColor bottomVertexColor = new SDL.FColor { R = tile.Brightness, G = tile.Brightness, B = tile.Brightness, A = 1.0f};

    //            //Color
    //            _vertices[baseIndex + 0].Color = topVertexColor;
    //            _vertices[baseIndex + 1].Color = topVertexColor;
    //            _vertices[baseIndex + 2].Color = bottomVertexColor;
    //            _vertices[baseIndex + 3].Color = bottomVertexColor;

    //            //TexCoords
    //            _vertices[baseIndex + 0].TexCoord = new SDL.FPoint { X = tile.TexX, Y = tile.TexY};
    //            _vertices[baseIndex + 1].TexCoord = new SDL.FPoint { X = tile.TexX + 0.5f, Y = tile.TexY};
    //            _vertices[baseIndex + 2].TexCoord = new SDL.FPoint { X = tile.TexX + 0.5f, Y = tile.TexY + 0.5f};
    //            _vertices[baseIndex + 3].TexCoord = new SDL.FPoint { X = tile.TexX, Y = tile.TexY + 0.5f};

    //            drawnTiles++;
    //        }
    //    }

    //    return drawnTiles;
    //}

    //public void RecalculateArrayBounds()
    //{
    //    _vertices = new SDL.Vertex[MAX_TILES * 4];
    //    _indices = new int[MAX_TILES * 6];

    //    for (int i = 0; i < MAX_TILES; i++)
    //    {
    //        int baseIndex = i * 4;
    //        _indices[i * 6 + 0] = baseIndex + 0;
    //        _indices[i * 6 + 1] = baseIndex + 1;
    //        _indices[i * 6 + 2] = baseIndex + 2;
    //        _indices[i * 6 + 3] = baseIndex + 0;
    //        _indices[i * 6 + 4] = baseIndex + 2;
    //        _indices[i * 6 + 5] = baseIndex + 3;
    //    }
    //}
}