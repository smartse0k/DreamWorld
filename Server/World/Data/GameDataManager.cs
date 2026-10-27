using System.Text.Json;
using DreamWorld.Common;
using DreamWorld.Common.Models;
using DreamWorld.World.AI;

namespace DreamWorld.World.Data;

/// <summary>
/// 게임 정적 데이터를 로드하고 관리하는 매니저.
/// </summary>
public class GameDataManager
{
    private readonly Dictionary<int, GameMap> _maps = new();
    private readonly Dictionary<int, SkillData> _skills = new();
    private readonly Dictionary<string, BtNode<MonsterAiContext>> _aiTrees = new();
    private readonly Dictionary<string, MonsterData> _monsters = new();

    public IReadOnlyDictionary<int, GameMap> Maps => _maps;
    public IReadOnlyDictionary<int, SkillData> Skills => _skills;
    public IReadOnlyDictionary<string, BtNode<MonsterAiContext>> AiTrees => _aiTrees;
    public IReadOnlyDictionary<string, MonsterData> Monsters => _monsters;

    public void LoadAll(string? baseDir = null)
    {
        string dataDir = FindDataDirectory(baseDir ?? AppContext.BaseDirectory);
        LoadSkills(Path.Combine(dataDir, "Skills", "skills.json"));
        LoadAiTrees(Path.Combine(dataDir, "AI"));
        LoadMonsters(Path.Combine(dataDir, "Monsters"));
        LoadMaps(Path.Combine(dataDir, "Maps"));
    }

    private void LoadSkills(string skillsPath)
    {
        _skills.Clear();

        if (File.Exists(skillsPath))
        {
            string json = File.ReadAllText(skillsPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
            var list = JsonSerializer.Deserialize<List<SkillData>>(json, options);
            if (list != null)
            {
                foreach (var skill in list)
                {
                    _skills[skill.SkillId] = skill;
                }
            }
        }

        // 기본 평타(SkillId = 1) 폴백 보장
        if (!_skills.ContainsKey(1))
        {
            _skills[1] = new SkillData
            {
                SkillId = 1,
                Name = "기본 베기 (전방 1타일)",
                TargetingType = AttackTargetingType.FrontTile,
                TileRange = 1,
                WorldRange = 1.5f,
                Damage = 15,
                CooldownMs = 500,
                MaxTargets = 1
            };
        }

        Console.WriteLine($"[GameDataManager] Loaded {_skills.Count} skills.");
    }

    private void LoadAiTrees(string aiDir)
    {
        _aiTrees.Clear();

        if (Directory.Exists(aiDir))
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            foreach (string file in Directory.GetFiles(aiDir, "*.json", SearchOption.AllDirectories).OrderBy(f => f))
            {
                string json = File.ReadAllText(file);
                var config = JsonSerializer.Deserialize<BtNodeConfig>(json, options);
                if (config == null)
                {
                    continue;
                }

                string aiKey = Path.GetFileNameWithoutExtension(file);
                _aiTrees[aiKey] = MonsterBehaviorTrees.BuildFromConfig(config);
                Console.WriteLine($"[GameDataManager] Loaded AI Tree '{aiKey}' (Root: {config.Type})");
            }
        }

        Console.WriteLine($"[GameDataManager] Loaded {_aiTrees.Count} AI trees.");
    }

    private void LoadMonsters(string monstersDir)
    {
        _monsters.Clear();

        if (Directory.Exists(monstersDir))
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            foreach (string file in Directory.GetFiles(monstersDir, "*.json", SearchOption.AllDirectories).OrderBy(f => f))
            {
                string json = File.ReadAllText(file);
                var monsterData = JsonSerializer.Deserialize<MonsterData>(json, options);
                if (monsterData == null)
                {
                    continue;
                }

                string monsterKey = Path.GetFileNameWithoutExtension(file);
                monsterData.MonsterKey = monsterKey;

                _monsters[monsterKey] = monsterData;
                Console.WriteLine(
                    $"[GameDataManager] Loaded Monster '{monsterKey}' ('{monsterData.Name}', " +
                    $"HP: {monsterData.MaxHp}, AI: {monsterData.AiKey}, Dmg: {monsterData.AttackDamage})");
            }
        }

        Console.WriteLine($"[GameDataManager] Loaded {_monsters.Count} monster templates.");
    }

    private void LoadMaps(string mapsDir)
    {
        _maps.Clear();

        if (Directory.Exists(mapsDir))
        {
            foreach (string mapFile in Directory.GetFiles(mapsDir, "*.json").OrderBy(f => f))
            {
                var gameMap = GameMap.LoadFromFile(mapFile);
                _maps[gameMap.MapId] = gameMap;
                Console.WriteLine(
                    $"[GameDataManager] Loaded Map [{gameMap.MapId}] '{gameMap.MapName}' " +
                    $"({gameMap.Width}x{gameMap.Height}, Portals: {gameMap.Portals.Count}, Spawns: {gameMap.MonsterSpawns.Count})");
            }
        }

        if (!_maps.ContainsKey(1))
        {
            var fallbackMap = GameMap.LoadFromFile("nonexistent");
            _maps[1] = fallbackMap;
        }
    }

    private static string FindDataDirectory(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "Data");
            if (Directory.Exists(Path.Combine(candidate, "Maps")))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        return Path.Combine(startDir, "Data");
    }
}
