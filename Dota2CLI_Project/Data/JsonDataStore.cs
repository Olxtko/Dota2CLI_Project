using System.Text.Json;
using Dota2CLI.Core;

namespace Dota2CLI.Data;

public sealed class JsonDataStore : IHeroRepository, IGameStateRepository, IGameResultRepository
{
    private readonly string _directory;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public JsonDataStore(string directory)
    {
        _directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(_directory);
    }

    private string PathFor(string file) => Path.Combine(_directory, file);

    public List<Hero> LoadHeroes()
    {
        var path = PathFor("heroes.json");
        if (!File.Exists(path))
        {
            var initial = CreateDefaultHeroes();
            SaveHeroes(initial);
            return initial;
        }

        try
        {
            var heroes = JsonSerializer.Deserialize<List<Hero>>(File.ReadAllText(path), _options) ?? new();
            foreach (var hero in heroes) hero.Validate();
            return heroes;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Файл каталогу '{path}' містить некоректний JSON.", ex);
        }
    }

    public void SaveHeroes(List<Hero> heroes) => WriteJsonAtomically("heroes.json", heroes);

    public GameState? LoadGame()
    {
        var path = PathFor("game-state.json");
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<GameState>(File.ReadAllText(path), _options); }
        catch (JsonException ex) { throw new InvalidDataException($"Файл збереження '{path}' містить некоректний JSON.", ex); }
    }

    public void SaveGame(GameState state) => WriteJsonAtomically("game-state.json", state);

    public void SaveResult(string result) =>
        File.AppendAllText(PathFor("game-results.txt"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {result}{Environment.NewLine}");

    private void WriteJsonAtomically<T>(string fileName, T value)
    {
        var path = PathFor(fileName);
        var tempPath = path + ".tmp";
        var json = JsonSerializer.Serialize(value, _options);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, path, overwrite: true);
    }

    private static List<Hero> CreateDefaultHeroes() =>
    [
        new() { Id="axe", Name="Axe", Attribute="Strength", AttackType="Melee", MaxHp=140, Attack=18, Armor=8, Resist=0.10, Speed=100, ArtworkUrl="https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/axe.png" },
        new() { Id="crystal-maiden", Name="Crystal Maiden", Attribute="Intelligence", AttackType="Ranged", MaxHp=90, Attack=22, Armor=3, Resist=0.15, Speed=110, ArtworkUrl="https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/crystal_maiden.png" },
        new() { Id="juggernaut", Name="Juggernaut", Attribute="Agility", AttackType="Melee", MaxHp=110, Attack=20, Armor=5, Resist=0.10, Speed=120, ArtworkUrl="https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/juggernaut.png" },
        new() { Id="lina", Name="Lina", Attribute="Intelligence", AttackType="Ranged", MaxHp=85, Attack=25, Armor=2, Resist=0.05, Speed=105, ArtworkUrl="https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/lina.png" },
        new() { Id="phantom-assassin", Name="Phantom Assassin", Attribute="Agility", AttackType="Melee", MaxHp=100, Attack=24, Armor=4, Resist=0.10, Speed=130, ArtworkUrl="https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/phantom_assassin.png" }
    ];
}
