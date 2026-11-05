using System.Net.Http;
using System.Text.Json;
using Dota2CLI.Core;

namespace Dota2CLI.Data;

public sealed class OpenDotaHeroApiClient : IHeroApiClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;
    private const string Endpoint = "https://api.opendota.com/api/heroes";

    public OpenDotaHeroApiClient(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _ownsClient = httpClient is null;
    }

    public async Task<IReadOnlyList<Hero>> GetHeroesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(Endpoint, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("API повернуло JSON неочікуваного формату: очікувався масив героїв.");

        var heroes = new List<Hero>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var apiId = ReadString(item, "name");
            var displayName = ReadString(item, "localized_name");
            if (string.IsNullOrWhiteSpace(apiId) || string.IsNullOrWhiteSpace(displayName)) continue;

            const string prefix = "npc_dota_hero_";
            var slug = apiId.StartsWith(prefix, StringComparison.Ordinal) ? apiId[prefix.Length..] : apiId;
            var attribute = ReadString(item, "primary_attr") ?? "unknown";
            var attackType = ReadString(item, "attack_type") ?? "Unknown";

            var hero = new Hero
            {
                Id = slug.Replace('_', '-'),
                Name = displayName,
                Attribute = attribute switch
                {
                    "str" => "Strength", "agi" => "Agility", "int" => "Intelligence",
                    "all" => "Universal", _ => "Unknown"
                },
                AttackType = attackType,
                ArtworkUrl = $"https://cdn.cloudflare.steamstatic.com/apps/dota2/images/dota_react/heroes/{slug}.png",
                Description = $"Персонаж Dota 2. Основний атрибут: {attribute}; тип атаки: {attackType}.",
                MaxHp = 100, Attack = 15, Armor = 5, Resist = 0.10, Speed = 100
            };
            hero.Validate();
            heroes.Add(hero);
        }

        if (heroes.Count == 0)
            throw new InvalidOperationException("API не повернуло жодного коректного персонажа.");
        return heroes;
    }

    private static string? ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    public void Dispose()
    {
        if (_ownsClient) _http.Dispose();
    }
}
