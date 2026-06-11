using Color = System.Drawing.Color;
using System.Numerics;
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
        noise.SetFrequency(0.002f);

        for (int y = -(_mapHeight / 2); y < _mapHeight / 2; y++)
        {
            for (int x = -(_mapWidth / 2); x < _mapWidth / 2; x++)
            {
                float noiseValue = noise.GetNoise(x, y);

                float brightness = 1f;

                if (noiseValue < 1f) brightness = 1;
                if (noiseValue < 0.65f) brightness = 0.65f;
                if (noiseValue < 0.4f) brightness = 0.4f;

                (Vector2 chunkPos, Chunk chunk) = GetOrCreateChunk(new Vector2(x * GamingState.TILE_WIDTH, y * GamingState.TILE_HEIGHT));

                int chunkX = x * GamingState.TILE_WIDTH - (int)chunkPos.X;
                int chunkY = y * GamingState.TILE_HEIGHT - (int)chunkPos.Y;

                chunk.PlaceTileAt(new Vector2(chunkX, chunkY), AssetManager.GetTexture("Grass"), brightness);
            }
        }
    }

    public void GenerateOres(int seed)
    {
        Random random = new(seed);

        int oreAmount = 100;

        for (int i = 0; i < oreAmount; i++)
        {
            float oreX = random.Next(0, _mapWidth * GamingState.TILE_WIDTH);
            float oreY = random.Next(0, _mapHeight * GamingState.TILE_HEIGHT);

            PlaceTile(AssetManager.GetTexture("CoalTile"), AlignToGrid(new Vector2(oreX, oreY)));
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
                //renderer.RenderRectangle(new Rectangle((chunkPosition - offset) * _zoom, new Vector2(Chunk.CHUNK_PIXEL_WIDTH * _zoom)), Color.Red);
                    
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

    public bool PlaceTile(Texture2D texture, Vector2 worldPosition)
    {
        (Vector2 chunkPos, Chunk chunk) = GetOrCreateChunk(worldPosition);

        float chunkX = worldPosition.X - chunkPos.X;
        float chunkY = worldPosition.Y - chunkPos.Y;

        if (!chunk.PlaceTileAt(new Vector2(chunkX, chunkY), texture, 1f)) return false;
        return true;
    }

    public bool RemoveTile(Vector2 worldPosition)
    {
        (Vector2 chunkPos, Chunk chunk) = GetOrCreateChunk(worldPosition);

        float chunkX = worldPosition.X - chunkPos.X;
        float chunkY = worldPosition.Y - chunkPos.Y;

        if (!chunk.RemoveTileAt(new Vector2(chunkX, chunkY)))
            return false;

        return true;
    }

    public Vector2 AlignToGrid(Vector2 worldPosition)
    {
        return new Vector2((int)MathF.Floor(worldPosition.X / GamingState.TILE_WIDTH) * GamingState.TILE_WIDTH,
                           (int)MathF.Floor(worldPosition.Y / GamingState.TILE_HEIGHT) * GamingState.TILE_WIDTH);
    }

    private Vector2 AlignToChunkGrid(Vector2 worldPosition)
    {
        return new Vector2((int)MathF.Floor(worldPosition.X / Chunk.CHUNK_PIXEL_WIDTH) * Chunk.CHUNK_PIXEL_WIDTH,
                           (int)MathF.Floor(worldPosition.Y / Chunk.CHUNK_PIXEL_HEIGHT) * Chunk.CHUNK_PIXEL_HEIGHT);
    }

    private (Vector2, Chunk) GetOrCreateChunk(Vector2 worldPosition)
    {
        Vector2 chunkPosition = AlignToChunkGrid(worldPosition);

        if (_chunks.TryGetValue(chunkPosition, out Chunk? foundChunk))
        {
            return (chunkPosition, foundChunk);
        }

        _chunks.Add(chunkPosition, new Chunk());
        return (chunkPosition, _chunks[chunkPosition]);
    }
}