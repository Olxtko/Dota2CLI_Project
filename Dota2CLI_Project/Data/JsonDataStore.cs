using System.Text.Json;
using Dota2CLI.Core;

namespace Dota2CLI.Data;

public sealed class JsonDataStore
{
    private readonly string _directory;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

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
            var initial = new List<Hero>
            {
                new() { Id="axe", Name="Axe", Attribute="Strength", AttackType="Melee", MaxHp=140, Attack=18, ArtworkUrl="https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/axe.png" },
                new() { Id="crystal-maiden", Name="Crystal Maiden", Attribute="Intelligence", AttackType="Ranged", MaxHp=90, Attack=22, ArtworkUrl="https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/crystal_maiden.png" },
                new() { Id="juggernaut", Name="Juggernaut", Attribute="Agility", AttackType="Melee", MaxHp=110, Attack=20, ArtworkUrl="https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/juggernaut.png" },
                new() { Id="lina", Name="Lina", Attribute="Intelligence", AttackType="Ranged", MaxHp=85, Attack=25, ArtworkUrl="https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/lina.png" },
                new() { Id="phantom-assassin", Name="Phantom Assassin", Attribute="Agility", AttackType="Melee", MaxHp=100, Attack=24, ArtworkUrl="https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/phantom_assassin.png" }
            };
            SaveHeroes(initial);
            return initial;
        }
        return JsonSerializer.Deserialize<List<Hero>>(File.ReadAllText(path), _options) ?? new();
    }

    public void SaveHeroes(List<Hero> heroes) =>
        File.WriteAllText(PathFor("heroes.json"), JsonSerializer.Serialize(heroes, _options));

    public GameState? LoadGame()
    {
        var path = PathFor("game-state.json");
        return File.Exists(path) ? JsonSerializer.Deserialize<GameState>(File.ReadAllText(path), _options) : null;
    }

    public void SaveGame(GameState state) =>
        File.WriteAllText(PathFor("game-state.json"), JsonSerializer.Serialize(state, _options));

    public void SaveResult(string result) =>
        File.AppendAllText(PathFor("game-results.txt"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {result}{Environment.NewLine}");
}
