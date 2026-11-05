using Dota2CLI.Core;
using Dota2CLI.Data;

internal static class Program
{
    private static FakeStore _savedStore = new();
    private static FakeCatalog _savedCatalog = new(new Hero { Id="p", Name="p" }, new Hero { Id="o", Name="o" });

    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("OpenDota JSON parsing", TestApiParsing),
            ("Damage formula", TestDamage),
            ("Speed 100 vs 150 ordering", TestSpeed150),
            ("Speed 200 receives two actions", TestSpeed200),
            ("Healing is capped at MaxHp", TestHealing),
            ("Defend reduces the next hit by 50%", TestDefend),
            ("Battle uses saved hero snapshots", TestSnapshot),
            ("Save and load preserve history and state", TestSaveLoad),
            ("Finished battle does not advance the round", TestFinishedBattleRound)
        };

        int failed = 0;
        foreach (var test in tests)
        {
            try { test.Run(); Console.WriteLine($"PASS: {test.Name}"); }
            catch (Exception ex) { failed++; Console.WriteLine($"FAIL: {test.Name} — {ex.Message}"); }
        }
        Console.WriteLine($"\nРезультат: {tests.Length - failed}/{tests.Length} тестів успішно.");
        return failed == 0 ? 0 : 1;
    }


    private static void TestApiParsing()
    {
        const string json = "[{\"name\":\"npc_dota_hero_axe\",\"localized_name\":\"Axe\",\"primary_attr\":\"str\",\"attack_type\":\"Melee\"}]";
        using var http = new HttpClient(new StubHandler(json));
        using var client = new OpenDotaHeroApiClient(http);
        var heroes = client.GetHeroesAsync().GetAwaiter().GetResult();
        Assert(heroes.Count == 1, "Expected one parsed hero.");
        Assert(heroes[0].Id == "axe" && heroes[0].Name == "Axe", "ID and display name must be mapped.");
        Assert(heroes[0].Attribute == "Strength" && heroes[0].AttackType == "Melee", "API fields must be mapped.");
        Assert(!string.IsNullOrWhiteSpace(heroes[0].ArtworkUrl), "GUI artwork URL must be prepared.");
    }

    private static void TestDamage()
    {
        Assert(BattleGame.CalculateDamage(30, 10, 0.25) == 15, "30 attack, 10 armor, 25% resist must deal 15.");
        Assert(BattleGame.CalculateDamage(10, 20, 0) == 0, "Armor must block damage completely.");
        Assert(BattleGame.CalculateDamage(30, 0, 2) == 0, "Resist is clamped to 100%.");
    }

    private static void TestSpeed150()
    {
        var (game, _) = CreateGame(150, 100);
        game.Act("attack");
        var history = game.Current!.History;
        Assert(history.Count == 2, "Expected one action for each fighter.");
        Assert(history[0].Actor == "Player", "Speed 150 must act before speed 100.");
        Assert(history[1].Actor == "Opponent", "Speed 100 must act after speed 150.");
    }

    private static void TestSpeed200()
    {
        var (game, _) = CreateGame(200, 150);
        game.Act("attack");
        var history = game.Current!.History;
        Assert(history.Count == 3, "Expected two player actions and one opponent action.");
        Assert(history[0].Actor == "Player", "Speed 200 first action must be first.");
        Assert(history[1].Actor == "Opponent", "Speed 150 action must occur between speed 200 actions.");
        Assert(history[2].Actor == "Player", "Speed 200 second action must occur midway through the round.");
    }

    private static void TestHealing()
    {
        var (game, heroes) = CreateGame(100, 100);
        game.Current!.PlayerHp = 495;
        game.Current.OpponentProfile!.Attack = 0;
        game.Act("heal");
        Assert(game.Current.PlayerHp == heroes.Player.MaxHp, "Healing must not exceed MaxHp.");
        Assert(game.Current.History[0].Healing == 5, "Only missing HP should be restored.");
    }

    private static void TestDefend()
    {
        var (game, _) = CreateGame(100, 100);
        game.Act("defend");
        var record = game.Current!.History.Single(x => x.Actor == "Opponent");
        Assert(record.Damage == 5, "Expected 50% of the normal 10 damage.");
        Assert(!game.Current.PlayerDefending, "Defend flag must be consumed by the next hit.");
    }

    private static void TestSnapshot()
    {
        var (game, heroes) = CreateGame(100, 100);
        heroes.Player.Attack = 999;
        game.Act("attack");
        Assert(game.Current!.PlayerProfile!.Attack == 10, "Battle must use the profile snapshot captured at start.");
    }

    private static void TestFinishedBattleRound()
    {
        var (game, _) = CreateGame(100, 100);
        game.Current!.OpponentHp = 1;
        game.Act("attack");
        Assert(game.Current.IsFinished, "Battle must finish when opponent HP reaches zero.");
        Assert(game.Current.Turn == 1, "Round number must not advance after the battle has finished.");
    }

    private static void TestSaveLoad()
    {
        var (game, _) = CreateGame(150, 100);
        game.Act("attack");
        var saved = game.Current!;
        var newGame = new BattleGame(_savedStore, _savedStore, _savedCatalog);
        newGame.Load();
        Assert(newGame.Current!.History.Count == saved.History.Count, "History must survive load.");
        Assert(newGame.Current.Turn == saved.Turn, "Round number must survive load.");
        Assert(newGame.Current.PlayerProfile!.Speed == 150, "Hero profile snapshot must survive load.");
    }

    private static (BattleGame Game, (Hero Player, Hero Opponent) Heroes) CreateGame(int playerSpeed, int opponentSpeed)
    {
        var player = new Hero { Id="player", Name="Player", MaxHp=500, Attack=10, Armor=0, Resist=0, Speed=playerSpeed };
        var opponent = new Hero { Id="opponent", Name="Opponent", MaxHp=500, Attack=10, Armor=0, Resist=0, Speed=opponentSpeed };
        var catalog = new FakeCatalog(player, opponent);
        var store = new FakeStore();
        _savedStore = store;
        _savedCatalog = catalog;
        var game = new BattleGame(store, store, catalog);
        game.Start("player", "opponent");
        return (game, (player, opponent));
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

internal sealed class FakeCatalog(Hero player, Hero opponent) : IHeroCatalog
{
    public IReadOnlyList<HeroCardData> GetCards() => new[] { player, opponent }.Select(h => new HeroCardData(h.Id, h.Name, h.Attribute, h.AttackType, h.MaxHp, h.Attack, h.Armor, h.Resist, h.Speed, h.ArtworkUrl, h.Description)).ToArray();

    public Hero? Find(string idOrName) => idOrName.Equals(player.Id, StringComparison.OrdinalIgnoreCase) || idOrName.Equals(player.Name, StringComparison.OrdinalIgnoreCase)
        ? player : idOrName.Equals(opponent.Id, StringComparison.OrdinalIgnoreCase) || idOrName.Equals(opponent.Name, StringComparison.OrdinalIgnoreCase) ? opponent : null;
}

internal sealed class FakeStore : IGameStateRepository, IGameResultRepository
{
    public GameState? State { get; private set; }
    public GameState? LoadGame() => State is null ? null : Clone(State);
    public void SaveGame(GameState state) => State = Clone(state);
    public void SaveResult(string result) { }

    private static GameState Clone(GameState s) => new()
    {
        PlayerHeroId=s.PlayerHeroId, OpponentHeroId=s.OpponentHeroId,
        PlayerProfile=s.PlayerProfile?.Copy(), OpponentProfile=s.OpponentProfile?.Copy(),
        PlayerHp=s.PlayerHp, OpponentHp=s.OpponentHp, Turn=s.Turn,
        IsFinished=s.IsFinished, Result=s.Result, PlayerDefending=s.PlayerDefending,
        History=s.History.Select(x => new TurnRecord { Turn=x.Turn, Sequence=x.Sequence, Actor=x.Actor, Action=x.Action,
            Damage=x.Damage, Healing=x.Healing, Message=x.Message, Timestamp=x.Timestamp }).ToList()
    };
}

internal sealed class StubHandler(string json) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
        return Task.FromResult(response);
    }
}
