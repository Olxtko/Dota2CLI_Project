namespace Dota2CLI.Core;

public interface IHeroLookup
{
    Hero? Find(string idOrName);
}

public interface IHeroCatalog : IHeroLookup
{
    IReadOnlyList<HeroCardData> GetCards();
}

public interface IHeroRepository
{
    List<Hero> LoadHeroes();
    void SaveHeroes(List<Hero> heroes);
}

public interface IGameStateRepository
{
    GameState? LoadGame();
    void SaveGame(GameState state);
}

public interface IGameResultRepository
{
    void SaveResult(string result);
}

public interface IHeroApiClient
{
    Task<IReadOnlyList<Hero>> GetHeroesAsync(CancellationToken cancellationToken = default);
}
