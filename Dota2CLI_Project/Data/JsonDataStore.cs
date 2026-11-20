using System.Text.Json;
using Dota2CLI.Core;

namespace Dota2CLI.Data;

public sealed class JsonDataStore : IHeroRepository, IGameStateRepository, IGameResultRepository
{
    private readonly string _directory;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public JsonDataStore(string? directory = null)
    {
        _directory = Path.GetFullPath(directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dota2CLI"));
        Directory.CreateDirectory(_directory);
    }

    public string StorageDirectory => _directory;

    private string PathFor(string file) => Path.Combine(_directory, file);

    public bool HasSavedGame => File.Exists(PathFor("game-state.json"));

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
            var heroes = JsonSerializer.Deserialize<List<Hero>>(File.ReadAllText(path), _options)
                ?? throw new InvalidDataException("Файл каталогу не містить списку персонажів.");
            if (heroes.Any(hero => hero is null))
                throw new InvalidDataException("Файл каталогу містить порожній запис персонажа.");
            foreach (var hero in heroes)
            {
                try
                {
                    hero.Validate();
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidDataException("Файл каталогу містить персонажа з некоректними характеристиками.", ex);
                }
            }
            if (heroes.Select(h => h.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != heroes.Count)
                throw new InvalidDataException("Файл каталогу містить повторювані ID персонажів.");
            return heroes;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Файл каталогу '{path}' містить некоректний JSON.", ex);
        }
    }

    public void SaveHeroes(List<Hero> heroes)
    {
        ArgumentNullException.ThrowIfNull(heroes);
        foreach (var hero in heroes)
        {
            ArgumentNullException.ThrowIfNull(hero);
            hero.Validate();
        }
        if (heroes.Select(h => h.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != heroes.Count)
            throw new InvalidDataException("Неможливо зберегти каталог: ID персонажів мають бути унікальними.");
        WriteJsonAtomically("heroes.json", heroes);
    }

    public GameState? LoadGame()
    {
        var path = PathFor("game-state.json");
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<GameState>(File.ReadAllText(path), _options)
                ?? throw new InvalidDataException($"Файл збереження '{path}' порожній або не містить стану гри.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Файл збереження '{path}' містить некоректний JSON.", ex);
        }
    }

    public void SaveGame(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        WriteJsonAtomically("game-state.json", state);
    }

    public void SaveResult(string result) =>
        File.AppendAllText(PathFor("game-results.txt"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {result}{Environment.NewLine}");

    private void WriteJsonAtomically<T>(string fileName, T value)
    {
        var path = PathFor(fileName);
        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(value, _options);
            File.WriteAllText(tempPath, json, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
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
