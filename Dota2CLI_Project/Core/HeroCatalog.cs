using System.Net.Http;
using System.Text.Json;
using Dota2CLI.Data;

namespace Dota2CLI.Core;

public sealed class HeroCatalog(JsonDataStore store)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly List<Hero> _heroes = store.LoadHeroes();
    public IReadOnlyList<Hero> All => _heroes;

    public void Refresh()
    {
        // Fetch current hero metadata from OpenDota. Keep user-created heroes.
        var json = Http.GetStringAsync("https://api.opendota.com/api/heroes")
            .GetAwaiter().GetResult();

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Віддалене джерело повернуло неочікуваний формат каталогу.");

        var previousHeroes = _heroes.ToList();
        var refreshed = new List<Hero>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var id = item.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            var displayName = item.TryGetProperty("localized_name", out var displayProp) ? displayProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displayName))
                continue;

            const string prefix = "npc_dota_hero_";
            var slug = id.StartsWith(prefix, StringComparison.Ordinal)
                ? id[prefix.Length..]
                : id;

            var attr = item.TryGetProperty("primary_attr", out var attrProp) ? attrProp.GetString() : "unknown";
            var attackType = item.TryGetProperty("attack_type", out var attackProp) ? attackProp.GetString() : "Unknown";
            refreshed.Add(new Hero
            {
                Id = slug.Replace('_', '-'),
                Name = displayName,
                Attribute = attr switch
                {
                    "str" => "Strength",
                    "agi" => "Agility",
                    "int" => "Intelligence",
                    "all" => "Universal",
                    _ => "Unknown"
                },
                AttackType = attackType ?? "Unknown",
                // OpenDota supplies stable internal hero names, which map to the Dota 2 artwork URL.
                ArtworkUrl = $"https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/{slug}.png",
                // The API does not provide combat stats; use sensible placeholder values for this exercise.
                MaxHp = 100,
                Attack = 15
            });
        }

        if (refreshed.Count == 0)
            throw new InvalidOperationException("Віддалене джерело не повернуло жодного героя.");

        // Preserve locally stored user-created heroes that are not part of the remote catalog.
        foreach (var localHero in previousHeroes)
        {
            if (!refreshed.Any(h => h.Id.Equals(localHero.Id, StringComparison.OrdinalIgnoreCase) ||
                                    h.Name.Equals(localHero.Name, StringComparison.OrdinalIgnoreCase)))
                refreshed.Add(localHero);
        }

        // Persist first. If saving fails, the current in-memory catalog stays intact.
        store.SaveHeroes(refreshed);
        _heroes.Clear();
        _heroes.AddRange(refreshed);
    }

    public Hero? Find(string idOrName) => _heroes.FirstOrDefault(h =>
        h.Id.Equals(idOrName, StringComparison.OrdinalIgnoreCase) ||
        h.Name.Equals(idOrName, StringComparison.OrdinalIgnoreCase));

    public void Add(Hero hero)
    {
        if (Find(hero.Id) is not null || Find(hero.Name) is not null)
            throw new InvalidOperationException("Герой із таким ID або ім'ям уже існує.");
        _heroes.Add(hero);
        store.SaveHeroes(_heroes);
    }
}
