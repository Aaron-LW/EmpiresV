using Color = System.Drawing.Color;
using System.Numerics;
using Smash.Graphics;
using SDL3;
using Smash;
using System.Collections.Concurrent;
using System.Globalization;

public class TileEngine
{
    private readonly bool _generateChunks;

    private readonly int _mapWidth;
    private readonly int _mapHeight;

    private float _zoom = 1;

    private ConcurrentDictionary<(int x, int y), Chunk> _chunks = new();
    private HashSet<(int x, int y)> _visibleChunks = new();

    private readonly object _generatingChunksLock = new();
    private readonly HashSet<(int x, int y)> _generatingChunks = new();

    private (int x, int y)? _lastVisibleChunkOrigin = null;

    private FastNoiseLite _noise;

    public TileEngine(int mapWidth, int mapHeight, float baseZoom, int seed, bool generateChunks)
    {
        _generateChunks = generateChunks;
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

                ((int chunkX, int chunkY), Chunk chunk) = GetOrCreateChunk(x * GamingState.TILE_WIDTH, y * GamingState.TILE_HEIGHT);

                int localChunkX = x * GamingState.TILE_WIDTH - chunkX;
                int localChunkY = y * GamingState.TILE_HEIGHT - chunkY;

                chunk.PlaceTileAt(new Vector2(localChunkX, localChunkY), AssetManager.GetTexture("Grass"), brightness);
            }
        }
    }

    public void GenerateOres(int seed)
    {
        Random random = new(seed);

        int oreAmount = 100;

        for (int i = 0; i < oreAmount; i++)
        {
            int oreX = random.Next(0, _mapWidth * GamingState.TILE_WIDTH);
            int oreY = random.Next(0, _mapHeight * GamingState.TILE_HEIGHT);

            (int x, int y) tilePos = AlignToGrid(oreX, oreY);
            PlaceTile(AssetManager.GetTexture("CoalTile"), new Vector2(tilePos.x, tilePos.y));
        }
    }

    public void UpdateVisibleChunks(Vector2 offset, float zoom)
    {
        (int sx, int sy) startPos = AlignToChunkGrid((int)offset.X - Chunk.CHUNK_PIXEL_WIDTH * 2, (int)offset.Y - Chunk.CHUNK_PIXEL_HEIGHT * 2);
        if (_lastVisibleChunkOrigin == startPos) return;
        else _lastVisibleChunkOrigin = startPos;

        HashSet<(int nx, int ny)> newChunks = new();

        float pixelsX = startPos.sx + (App.WindowWidth / zoom) + (Chunk.CHUNK_PIXEL_WIDTH * 3);
        float pixelsY = startPos.sy + (App.WindowHeight / zoom) + (Chunk.CHUNK_PIXEL_HEIGHT * 3);

        for (int y = startPos.sy; y < pixelsY; y += Chunk.CHUNK_PIXEL_HEIGHT)
        {
            for (int x = startPos.sx; x < pixelsX; x += Chunk.CHUNK_PIXEL_WIDTH)
            {
                if (_chunks.ContainsKey((x, y)))
                {
                    newChunks.Add((x, y));
                }
                else if (_generateChunks)
                {
                    lock (_generatingChunksLock)
                    {
                        if (_generatingChunks.Add((x, y)))
                        {
                            int chunkX = x;
                            int chunkY = y;

                            Task.Run(async () => GenerateChunkAsync(chunkX, chunkY));
                        }
                    }
                }
            }
        }

        foreach (var chunk in _visibleChunks.Except(newChunks))
        {
            _chunks[chunk].DestroyRenderTarget();
        }

        _visibleChunks = newChunks;
    }

    public void Render(Renderer renderer, Vector2 offset)
    {
        foreach ((int x, int y) chunkPosition in _visibleChunks)
        {
            Chunk chunk = _chunks[chunkPosition];
            
            if (chunk.Dirty)
            {
                chunk.RebuildChunk(renderer);
            }

            SDL.FRect dstRect = new()
            {
                X = (chunkPosition.x - offset.X) * _zoom,
                Y = (chunkPosition.y - offset.Y) * _zoom,
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
        ((int x, int y), Chunk chunk) = GetOrCreateChunk((int)worldPosition.X, (int)worldPosition.Y);

        float chunkX = worldPosition.X - x;
        float chunkY = worldPosition.Y - y;

        if (!chunk.PlaceTileAt(new Vector2(chunkX, chunkY), texture, 1f)) return false;
        return true;
    }

    public bool RemoveTile(Vector2 worldPosition)
    {
        ((int x, int y), Chunk chunk) = GetOrCreateChunk((int)worldPosition.X, (int)worldPosition.Y);

        float chunkX = worldPosition.X - x;
        float chunkY = worldPosition.Y - y;

        if (!chunk.RemoveTileAt(new Vector2(chunkX, chunkY)))
            return false;

        return true;
    }

    public int GetChunkAmount()
    {
        return _chunks.Values.Count;
    }

    public int GetVisibleChunkAmount()
    {
        return _visibleChunks.Count;
    }

    public static (int x, int y) AlignToGrid(int worldX, int worldY)
    {
        return ((int)MathF.Floor(worldX / GamingState.TILE_WIDTH) * GamingState.TILE_WIDTH,
                (int)MathF.Floor(worldY / GamingState.TILE_HEIGHT) * GamingState.TILE_HEIGHT);
    }

    private static (int x, int y) AlignToChunkGrid(int worldX, int worldY)
    {
        return ((int)MathF.Floor(worldX / Chunk.CHUNK_PIXEL_WIDTH) * Chunk.CHUNK_PIXEL_WIDTH,
                (int)MathF.Floor(worldY / Chunk.CHUNK_PIXEL_HEIGHT) * Chunk.CHUNK_PIXEL_HEIGHT);
    }

    private ((int x, int y), Chunk) GetOrCreateChunk(int x, int y)
    {
        (int x, int y) chunkPosition = AlignToChunkGrid(x, y);

        if (_chunks.TryGetValue(chunkPosition, out Chunk? foundChunk))
        {
            return (chunkPosition, foundChunk);
        }

        Chunk chunk = new();
        _chunks.TryAdd(chunkPosition, chunk);
        _visibleChunks.Add(chunkPosition);
        return (chunkPosition, _chunks[chunkPosition]);
    }

    private Chunk GenerateChunk(int chunkX, int chunkY)
    {
        (int x, int y) chunkPosition = AlignToChunkGrid(chunkX, chunkY);

        Chunk chunk = new();
        for (int y = 0; y < Chunk.CHUNK_HEIGHT; y++)
        {
            for (int x = 0; x < Chunk.CHUNK_WIDHT; x++)
            {
                float noiseValue = _noise.GetNoise(chunkPosition.x + x * GamingState.TILE_WIDTH, chunkPosition.y + y * GamingState.TILE_HEIGHT);

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

    private async Task GenerateChunkAsync(int worldX, int worldY)
    {
        Chunk chunk = new();

        for (int y = 0; y < Chunk.CHUNK_HEIGHT; y++)
        {
            for (int x = 0; x < Chunk.CHUNK_WIDHT; x++)
            {
                float noiseValue = _noise.GetNoise(worldX + x * GamingState.TILE_WIDTH, worldY + y * GamingState.TILE_HEIGHT);

                float brightness = 1f;

                if (noiseValue < 1f) brightness = 1;
                if (noiseValue < 0.65f) brightness = 0.65f;
                if (noiseValue < 0.4f) brightness = 0.4f;

                Vector2 tileChunkPosition = new Vector2(x * GamingState.TILE_WIDTH, y * GamingState.TILE_HEIGHT);
                chunk.PlaceTileAt(tileChunkPosition, AssetManager.GetTexture("Grass"), brightness);
            }
        } 

        _chunks.TryAdd((worldX, worldY), chunk);

        lock (_generatingChunksLock)
        {
            _generatingChunks.Remove((worldX, worldY));
        }
    }
}