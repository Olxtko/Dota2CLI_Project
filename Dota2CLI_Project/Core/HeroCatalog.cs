namespace Dota2CLI.Core;

public sealed class HeroCatalog : IHeroCatalog
{
    private readonly IHeroRepository _repository;
    private readonly IHeroApiClient _apiClient;
    private readonly List<Hero> _heroes;

    public HeroCatalog(IHeroRepository repository, IHeroApiClient apiClient)
    {
        _repository = repository;
        _apiClient = apiClient;
        _heroes = repository.LoadHeroes();
        foreach (var hero in _heroes) hero.Validate();
    }

    public IReadOnlyList<Hero> All => _heroes.AsReadOnly();

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var remoteHeroes = (await _apiClient.GetHeroesAsync(cancellationToken).ConfigureAwait(false))
            .Select(x => x.Copy()).ToList();
        var previousHeroes = _heroes.ToList();

        foreach (var localHero in previousHeroes)
        {
            int existingIndex = remoteHeroes.FindIndex(h =>
                h.Id.Equals(localHero.Id, StringComparison.OrdinalIgnoreCase) ||
                h.Name.Equals(localHero.Name, StringComparison.OrdinalIgnoreCase));
            if (localHero.IsUserCreated)
            {
                if (existingIndex >= 0)
                    remoteHeroes[existingIndex] = localHero.Copy();
                else
                    remoteHeroes.Add(localHero.Copy());
            }
            else if (existingIndex < 0)
            {
                remoteHeroes.Add(localHero.Copy());
            }
        }

        _repository.SaveHeroes(remoteHeroes);
        _heroes.Clear();
        _heroes.AddRange(remoteHeroes);
    }

    public void Refresh() => RefreshAsync().GetAwaiter().GetResult();

    public Hero? Find(string idOrName)
    {
        if (string.IsNullOrWhiteSpace(idOrName)) return null;
        return _heroes.FirstOrDefault(h =>
            h.Id.Equals(idOrName, StringComparison.OrdinalIgnoreCase) ||
            h.Name.Equals(idOrName, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<HeroCardData> GetCards() => _heroes.Select(h => new HeroCardData(
        h.Id, h.Name, h.Attribute, h.AttackType, h.MaxHp, h.Attack, h.Armor,
        h.Resist, h.Speed, h.ArtworkUrl, h.Description)).ToArray();

    public void Add(Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);
        hero.Validate();
        if (Find(hero.Id) is not null || Find(hero.Name) is not null)
            throw new InvalidOperationException("Герой із таким ID або ім'ям уже існує.");
        var createdHero = hero.Copy();
        createdHero.IsUserCreated = true;
        var updated = _heroes.Select(x => x.Copy()).Append(createdHero).ToList();
        _repository.SaveHeroes(updated);
        _heroes.Add(createdHero.Copy());
    }
}
