using Color = System.Drawing.Color;
using System.Numerics;
using Smash.Graphics;
using SDL3;
using Smash;
using System.Diagnostics;

public class TileEngine
{
    private readonly int _mapWidth;
    private readonly int _mapHeight;

    private float _zoom = 1;

    private Dictionary<Vector2, Chunk> _chunks = new();
    private List<(Vector2, Chunk)> _visibleChunks = new();

    private FastNoiseLite _noise;

    public TileEngine(int mapWidth, int mapHeight, float baseZoom, int seed)
    {
        _zoom = baseZoom;

        _mapWidth = mapWidth;
        _mapHeight = mapHeight;

        _noise = new(seed);
        _noise.SetNoiseType(FastNoiseLite.NoiseType.Cellular);
        _noise.SetCellularDistanceFunction(FastNoiseLite.CellularDistanceFunction.EuclideanSq);
        _noise.SetCellularReturnType(FastNoiseLite.CellularReturnType.CellValue);
        _noise.SetCellularJitter(0.5f);
        _noise.SetFrequency(0.002f);
    }

    public void GenerateWorld()
    {
        for (int y = -(_mapHeight / 2); y < _mapHeight / 2; y++)
        {
            for (int x = -(_mapWidth / 2); x < _mapWidth / 2; x++)
            {
                float noiseValue = _noise.GetNoise(x, y);

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

    public void UpdateVisibleChunks(Vector2 offset, float zoom)
    {
        _visibleChunks.Clear();
        Vector2 startPos = AlignToChunkGrid(offset) - new Vector2(Chunk.CHUNK_PIXEL_WIDTH);

        float pixelsX = startPos.X + ((App.WindowWidth + Chunk.CHUNK_PIXEL_WIDTH * 3) / zoom);
        float pixelsY = startPos.Y + ((App.WindowHeight + Chunk.CHUNK_PIXEL_HEIGHT * 3) / zoom);

        for (float y = startPos.Y; y < pixelsY; y += Chunk.CHUNK_PIXEL_HEIGHT)
        {
            for (float x = startPos.X; x < pixelsX; x += Chunk.CHUNK_PIXEL_WIDTH)
            {
                Vector2 chunkPosition = new Vector2(x, y);
                if (_chunks.TryGetValue(chunkPosition, out Chunk? chunk))
                {
                    _visibleChunks.Add((chunkPosition, chunk));
                }
            }
        }
    }

    public void Render(Renderer renderer, Vector2 offset)
    {
        foreach ((Vector2 chunkPosition, Chunk chunk) in _visibleChunks)
        {
            if (chunk.Dirty)
            {
                chunk.RebuildChunk(renderer);
            }

            SDL.FRect dstRect = new()
            {
                X = (chunkPosition.X - offset.X) * _zoom,
                Y = (chunkPosition.Y - offset.Y) * _zoom,
                W = Chunk.CHUNK_PIXEL_WIDTH * _zoom,
                H = Chunk.CHUNK_PIXEL_HEIGHT * _zoom
            };

            SDL.RenderTexture(renderer.Handle, chunk.RenderTarget, IntPtr.Zero, dstRect);
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

    public void PlaceChunk(Vector2 worldPosition)
    {
        Vector2 chunkPosition = AlignToChunkGrid(worldPosition);

        if (_chunks.ContainsKey(chunkPosition))
            return;

        Chunk chunk = GenerateChunk(chunkPosition);
        _visibleChunks.Add((chunkPosition, chunk));
        _chunks.Add(chunkPosition, chunk);
    }

    public static Vector2 AlignToGrid(Vector2 worldPosition)
    {
        return new Vector2((int)MathF.Floor(worldPosition.X / GamingState.TILE_WIDTH) * GamingState.TILE_WIDTH,
                           (int)MathF.Floor(worldPosition.Y / GamingState.TILE_HEIGHT) * GamingState.TILE_WIDTH);
    }

    private static Vector2 AlignToChunkGrid(Vector2 worldPosition)
    {
        return new Vector2((int)Math.Floor(worldPosition.X / Chunk.CHUNK_PIXEL_WIDTH) * Chunk.CHUNK_PIXEL_WIDTH,
                           (int)Math.Floor(worldPosition.Y / Chunk.CHUNK_PIXEL_HEIGHT) * Chunk.CHUNK_PIXEL_HEIGHT);
    }

    private (Vector2, Chunk) GetOrCreateChunk(Vector2 worldPosition)
    {
        Vector2 chunkPosition = AlignToChunkGrid(worldPosition);

        if (_chunks.TryGetValue(chunkPosition, out Chunk? foundChunk))
        {
            return (chunkPosition, foundChunk);
        }

        Chunk chunk = new();
        _chunks.Add(chunkPosition, chunk);
        _visibleChunks.Add((chunkPosition, chunk));
        return (chunkPosition, _chunks[chunkPosition]);
    }

    private Chunk GenerateChunk(Vector2 worldPosition)
    {
        Vector2 chunkPosition = AlignToChunkGrid(worldPosition);

        Chunk chunk = new();
        for (int y = 0; y < Chunk.CHUNK_HEIGHT; y++)
        {
            for (int x = 0; x < Chunk.CHUNK_WIDHT; x++)
            {
                float noiseValue = _noise.GetNoise(chunkPosition.X + x * GamingState.TILE_WIDTH, chunkPosition.Y + y * GamingState.TILE_HEIGHT);

                float brightness = 1f;

                if (noiseValue < 1f) brightness = 1;
                if (noiseValue < 0.65f) brightness = 0.65f;
                if (noiseValue < 0.4f) brightness = 0.4f;

                Vector2 tileChunkPosition = new Vector2(x * GamingState.TILE_WIDTH, y * GamingState.TILE_HEIGHT);
                chunk.PlaceTileAt(tileChunkPosition, AssetManager.GetTexture("Grass"), brightness);
            }
        }

        return chunk;
    }
}