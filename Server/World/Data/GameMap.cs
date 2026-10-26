using System.Text.Json;
using DreamWorld.Common;

namespace DreamWorld.World.Data;

public class PortalConfig
{
    public int TileX { get; set; }
    public int TileY { get; set; }
    public int TargetMapId { get; set; }
    public float TargetSpawnX { get; set; }
    public float TargetSpawnY { get; set; }
    public string Name { get; set; } = "Portal";
}

public class MonsterSpawnConfig
{
    public int MonsterTemplateId { get; set; }
    public string Name { get; set; } = "Monster";
    public int MaxHp { get; set; } = 50;
    public int TileX { get; set; }
    public int TileY { get; set; }
}

public class MapJsonConfig
{
    public int MapId { get; set; }
    public string MapName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public float SpawnX { get; set; } = 3.5f;
    public float SpawnY { get; set; } = 3.5f;
    public int[][] Tiles { get; set; } = Array.Empty<int[]>();
    public List<PortalConfig> Portals { get; set; } = new();
    public List<MonsterSpawnConfig> MonsterSpawns { get; set; } = new();
}

/// <summary>
/// 타일맵 지형 및 충돌 판정을 관리하는 클래스.
/// </summary>
public class GameMap
{
    public int MapId { get; }
    public string MapName { get; }
    public int Width { get; }
    public int Height { get; }
    public float SpawnX { get; }
    public float SpawnY { get; }

    private readonly TileType[,] _tiles;
    private readonly byte[] _flattenedTiles;
    public IReadOnlyList<PortalConfig> Portals { get; }
    public IReadOnlyList<MonsterSpawnConfig> MonsterSpawns { get; }

    public GameMap(MapJsonConfig config)
    {
        MapId = config.MapId;
        MapName = config.MapName;
        Width = config.Width;
        Height = config.Height;
        SpawnX = config.SpawnX;
        SpawnY = config.SpawnY;
        Portals = config.Portals;
        MonsterSpawns = config.MonsterSpawns;

        _tiles = new TileType[Width, Height];
        _flattenedTiles = new byte[Width * Height];

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int raw = (y < config.Tiles.Length && x < config.Tiles[y].Length)
                    ? config.Tiles[y][x]
                    : 1;
                var tileType = (TileType)raw;
                _tiles[x, y] = tileType;
                _flattenedTiles[y * Width + x] = (byte)tileType;
            }
        }

        foreach (var p in Portals)
        {
            if (p.TileX >= 0 && p.TileX < Width && p.TileY >= 0 && p.TileY < Height)
            {
                _tiles[p.TileX, p.TileY] = TileType.Portal;
                _flattenedTiles[p.TileY * Width + p.TileX] = (byte)TileType.Portal;
            }
        }
    }

    public byte[] GetFlattenedTiles()
    {
        return _flattenedTiles;
    }

    public static GameMap LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"[GameMap] File not found at '{path}'. Creating fallback 16x16 map.");
            return CreateFallbackMap();
        }

        string json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var config = JsonSerializer.Deserialize<MapJsonConfig>(json, options)
                     ?? throw new InvalidOperationException("Failed to deserialize map JSON.");
        return new GameMap(config);
    }

    public TileType GetTile(int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= Width || tileY < 0 || tileY >= Height)
        {
            return TileType.Wall;
        }
        return _tiles[tileX, tileY];
    }

    public bool IsTileWalkable(int tileX, int tileY)
    {
        TileType t = GetTile(tileX, tileY);
        return t == TileType.Walkable || t == TileType.Portal;
    }

    public bool IsTileWall(int tileX, int tileY)
    {
        return GetTile(tileX, tileY) == TileType.Wall;
    }

    public bool CanStandAt(float worldX, float worldY, float radius = GridMath.DefaultEntityRadius)
    {
        return GridMath.CanStandAt(worldX, worldY, radius, IsTileWalkable);
    }

    public bool TryFindTouchedPortal(float worldX, float worldY, out PortalConfig touchedPortal)
    {
        var (curTx, curTy) = GridMath.WorldToTile(worldX, worldY);
        foreach (var portal in Portals)
        {
            if (portal.TileX == curTx && portal.TileY == curTy)
            {
                touchedPortal = portal;
                return true;
            }

            var (pcx, pcy) = GridMath.TileToWorldCenter(portal.TileX, portal.TileY);
            if (GridMath.Distance(worldX, worldY, pcx, pcy) <= 0.55f)
            {
                touchedPortal = portal;
                return true;
            }
        }

        touchedPortal = null!;
        return false;
    }

    private static GameMap CreateFallbackMap()
    {
        int w = 16, h = 16;
        int[][] tiles = new int[h][];
        for (int y = 0; y < h; y++)
        {
            tiles[y] = new int[w];
            for (int x = 0; x < w; x++)
            {
                tiles[y][x] = (x == 0 || y == 0 || x == w - 1 || y == h - 1) ? 1 : 0;
            }
        }

        return new GameMap(new MapJsonConfig
        {
            MapId = 1,
            MapName = "Fallback Field",
            Width = w,
            Height = h,
            SpawnX = 3.5f,
            SpawnY = 3.5f,
            Tiles = tiles,
            MonsterSpawns = new List<MonsterSpawnConfig>
            {
                new() { MonsterTemplateId = 101, Name = "꿈결 슬라임", MaxHp = 50, TileX = 4, TileY = 4 },
                new() { MonsterTemplateId = 103, Name = "고대 허수아비", MaxHp = 500, TileX = 3, TileY = 6 }
            }
        });
    }
}
