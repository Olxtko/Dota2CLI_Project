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
            ("Зчитування JSON з OpenDota API", TestApiParsing),
            ("Розрахунок бойової шкоди", TestDamage),
            ("Порядок дій за швидкості 100 і 150", TestSpeed150),
            ("Дві дії персонажа зі швидкістю 200", TestSpeed200),
            ("Лікування не перевищує максимальне HP", TestHealing),
            ("Захист зменшує наступну шкоду на 50%", TestDefend),
            ("Бій використовує збережені профілі персонажів", TestSnapshot),
            ("Збереження та завантаження відновлюють стан та історію", TestSaveLoad),
            ("JSON-файли зберігають персонажів і стан гри", TestJsonPersistence),
            ("Створений персонаж зберігається між запусками сховища", TestCreatedHeroPersistence),
            ("Бій відновлюється з файлів після повторного запуску", TestFileBackedBattleResume),
            ("Пошкоджений JSON відхиляється", TestCorruptedSave),
            ("Некоректні характеристики у збереженні відхиляються", TestInvalidHeroInSave),
            ("Оновлення API не перезаписує локального персонажа", TestRefreshKeepsLocalHero),
            ("Після завершення бою номер раунду не змінюється", TestFinishedBattleRound),
            ("Прапорець захисту відновлюється після перезапуску", TestDefendPersistence),
            ("Помилка пошкодженого збереження не завершує роботу CLI", TestCliHandlesInvalidData)
        };

        int failed = 0;
        foreach (var test in tests)
        {
            try { test.Run(); Console.WriteLine($"УСПІШНО: {test.Name}"); }
            catch (Exception ex) { failed++; Console.WriteLine($"ПОМИЛКА: {test.Name} — {ex.Message}"); }
        }
        Console.WriteLine($"\nПідсумок: {tests.Length - failed}/{tests.Length} перевірок успішно.");
        return failed == 0 ? 0 : 1;
    }


    private static void TestApiParsing()
    {
        const string json = "[{\"name\":\"npc_dota_hero_axe\",\"localized_name\":\"Axe\",\"primary_attr\":\"str\",\"attack_type\":\"Melee\"}]";
        using var http = new HttpClient(new StubHandler(json));
        using var client = new OpenDotaHeroApiClient(http);
        var heroes = client.GetHeroesAsync().GetAwaiter().GetResult();
        Assert(heroes.Count == 1, "Очікувався один персонаж у відповіді API.");
        Assert(heroes[0].Id == "axe" && heroes[0].Name == "Axe", "ID та ім’я персонажа мають бути правильно зчитані.");
        Assert(heroes[0].Attribute == "Strength" && heroes[0].AttackType == "Melee", "Поля API мають бути правильно перетворені.");
        Assert(!string.IsNullOrWhiteSpace(heroes[0].ArtworkUrl), "Посилання на графіку має бути сформоване.");
    }

    private static void TestDamage()
    {
        Assert(BattleGame.CalculateDamage(30, 10, 0.25) == 15, "За атаки 30, броні 10 та опору 25% шкода має дорівнювати 15.");
        Assert(BattleGame.CalculateDamage(10, 20, 0) == 0, "Броня має повністю блокувати шкоду, якщо її достатньо.");
        Assert(BattleGame.CalculateDamage(30, 0, 2) == 0, "Опір має обмежуватися значенням 100%.");
    }

    private static void TestSpeed150()
    {
        var (game, _) = CreateGame(150, 100);
        game.Act("attack");
        var history = game.Current!.History;
        Assert(history.Count == 2, "Кожен учасник має виконати одну дію.");
        Assert(history[0].Actor == "Player", "Персонаж зі швидкістю 150 має діяти раніше за персонажа зі швидкістю 100.");
        Assert(history[1].Actor == "Opponent", "Персонаж зі швидкістю 100 має діяти після персонажа зі швидкістю 150.");
    }

    private static void TestSpeed200()
    {
        var (game, _) = CreateGame(200, 150);
        game.Act("attack");
        var history = game.Current!.History;
        Assert(history.Count == 3, "Очікуються дві дії гравця та одна дія суперника.");
        Assert(history[0].Actor == "Player", "Перша дія персонажа зі швидкістю 200 має бути першою.");
        Assert(history[1].Actor == "Opponent", "Дія персонажа зі швидкістю 150 має відбутися між двома діями персонажа зі швидкістю 200.");
        Assert(history[2].Actor == "Player", "Друга дія персонажа зі швидкістю 200 має відбутися після дії суперника.");
    }

    private static void TestHealing()
    {
        var (game, heroes) = CreateGame(100, 100);
        game.Current!.PlayerHp = 495;
        game.Current.OpponentProfile!.Attack = 0;
        game.Act("heal");
        Assert(game.Current.PlayerHp == heroes.Player.MaxHp, "Лікування не повинно перевищувати максимальне HP.");
        Assert(game.Current.History[0].Healing == 5, "Має відновлюватися лише відсутня кількість HP.");
    }

    private static void TestDefend()
    {
        var (game, _) = CreateGame(100, 100);
        game.Act("defend");
        var record = game.Current!.History.Single(x => x.Actor == "Opponent");
        Assert(record.Damage == 5, "Захист має зменшити звичайну шкоду 10 до 5.");
        Assert(!game.Current.PlayerDefending, "Прапорець захисту має скидатися після наступного удару.");
    }

    private static void TestSnapshot()
    {
        var (game, heroes) = CreateGame(100, 100);
        heroes.Player.Attack = 999;
        game.Act("attack");
        Assert(game.Current!.PlayerProfile!.Attack == 10, "Бій має використовувати копію профілю, створену на початку.");
    }

    private static void TestJsonPersistence()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Dota2CLI-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonDataStore(directory);
            var customHero = new Hero
            {
                Id = "custom-test", Name = "Custom Test", Attribute = "Strength",
                AttackType = "Melee", MaxHp = 120, Attack = 25, Armor = 4,
                Resist = 0.1, Speed = 120, IsUserCreated = true
            };
            store.SaveHeroes(new List<Hero> { customHero });

            var loadedHeroes = store.LoadHeroes();
            Assert(loadedHeroes.Count == 1 && loadedHeroes[0].Id == "custom-test" && loadedHeroes[0].IsUserCreated,
                "Створений користувачем персонаж та його ознака мають зберігатися після перезапуску.");

            var state = new GameState
            {
                PlayerHeroId = "custom-test", OpponentHeroId = "opponent",
                PlayerProfile = customHero.Copy(),
                OpponentProfile = new Hero { Id = "opponent", Name = "Opponent", MaxHp = 100, Attack = 10, Speed = 100 },
                PlayerHp = 95, OpponentHp = 80, Turn = 3,
                History = new List<TurnRecord> { new() { Turn = 2, Sequence = 1, Actor = "Custom Test", Action = "attack", Damage = 20, Message = "test" } }
            };
            store.SaveGame(state);
            Assert(store.HasSavedGame, "Файл останнього збереження має існувати.");
            var restored = store.LoadGame();
            Assert(restored is not null && restored.Turn == 3 && restored.PlayerHp == 95,
                "Останній стан гри має зберігатися після перезапуску.");
            Assert(restored!.History.Count == 1 && restored.PlayerProfile!.Id == "custom-test",
                "Історія та копії профілів мають зберігатися після перезапуску.");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }


    private static void TestCreatedHeroPersistence()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Dota2CLI-heroes-" + Guid.NewGuid().ToString("N"));
        try
        {
            var firstStore = new JsonDataStore(directory);
            using var api = new OpenDotaHeroApiClient(new HttpClient(new StubHandler("[]")));
            var catalog = new HeroCatalog(firstStore, api);
            catalog.Add(new Hero
            {
                Id = "my-created-hero", Name = "My Created Hero", Attribute = "Agility",
                AttackType = "Ranged", MaxHp = 150, Attack = 30, Armor = 7, Resist = 0.2, Speed = 150
            });

            var secondStore = new JsonDataStore(directory);
            var heroesAfterRestart = secondStore.LoadHeroes();
            var restored = heroesAfterRestart.SingleOrDefault(x => x.Id == "my-created-hero");
            Assert(restored is not null && restored.Name == "My Created Hero" && restored.Speed == 150,
                "Персонаж, створений через каталог застосунку, має зберігатися у heroes.json.");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static void TestFileBackedBattleResume()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Dota2CLI-resume-" + Guid.NewGuid().ToString("N"));
        try
        {
            var firstStore = new JsonDataStore(directory);
            using var api = new OpenDotaHeroApiClient(new HttpClient(new StubHandler("[]")));
            var catalog = new HeroCatalog(firstStore, api);
            catalog.Add(new Hero { Id = "resume-player", Name = "Resume Player", MaxHp = 500, Attack = 10, Armor = 0, Resist = 0, Speed = 150 });
            catalog.Add(new Hero { Id = "resume-opponent", Name = "Resume Opponent", MaxHp = 500, Attack = 10, Armor = 0, Resist = 0, Speed = 100 });

            var firstRunGame = new BattleGame(firstStore, firstStore, catalog);
            firstRunGame.Start("resume-player", "resume-opponent");
            firstRunGame.Act("attack");
            var expectedTurn = firstRunGame.Current!.Turn;
            var expectedHistoryCount = firstRunGame.Current.History.Count;
            var expectedPlayerHp = firstRunGame.Current.PlayerHp;

            var secondStore = new JsonDataStore(directory);
            using var secondApi = new OpenDotaHeroApiClient(new HttpClient(new StubHandler("[]")));
            var secondCatalog = new HeroCatalog(secondStore, secondApi);
            var secondRunGame = new BattleGame(secondStore, secondStore, secondCatalog);
            secondRunGame.Load();

            Assert(secondRunGame.Current!.Turn == expectedTurn, "Номер раунду має відновлюватися після перезапуску.");
            Assert(secondRunGame.Current.History.Count == expectedHistoryCount, "Історія дій має відновлюватися після перезапуску.");
            Assert(secondRunGame.Current.PlayerHp == expectedPlayerHp, "Поточне HP має відновлюватися після перезапуску.");
            Assert(secondCatalog.Find("resume-player") is not null, "Створений користувачем персонаж має залишатися доступним після перезапуску.");
            secondRunGame.Act("attack");
            Assert(secondRunGame.Current!.Turn > expectedTurn, "Після відновлення має бути можливість продовжити бій.");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static void TestCorruptedSave()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Dota2CLI-corrupt-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "game-state.json"), "{ broken json");
            var store = new JsonDataStore(directory);
            bool rejected = false;
            try { store.LoadGame(); }
            catch (InvalidDataException) { rejected = true; }
            Assert(rejected, "Некоректний JSON має визначатися як пошкоджене збереження.");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }


    private static void TestInvalidHeroInSave()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Dota2CLI-invalid-state-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonDataStore(directory);
            store.SaveGame(new GameState
            {
                PlayerHeroId = "bad-player",
                OpponentHeroId = "opponent",
                PlayerProfile = new Hero { Id = "bad-player", Name = "Bad Player", MaxHp = 0 },
                OpponentProfile = new Hero { Id = "opponent", Name = "Opponent" },
                PlayerHp = 10,
                OpponentHp = 10,
                Turn = 1
            });
            using var api = new OpenDotaHeroApiClient(new HttpClient(new StubHandler("[]")));
            var catalog = new HeroCatalog(store, api);
            var game = new BattleGame(store, store, catalog);
            bool rejected = false;
            try { game.Load(); }
            catch (InvalidDataException) { rejected = true; }
            Assert(rejected, "Некоректні характеристики персонажа мають відхилятися під час завантаження.");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static void TestRefreshKeepsLocalHero()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Dota2CLI-refresh-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonDataStore(directory);
            using var api = new OpenDotaHeroApiClient(new HttpClient(new StubHandler(
                "[{\"name\":\"npc_dota_hero_custom_test\",\"localized_name\":\"Custom Test\",\"primary_attr\":\"str\",\"attack_type\":\"Melee\"}]")));
            var catalog = new HeroCatalog(store, api);
            catalog.Add(new Hero
            {
                Id = "custom-test", Name = "Мій персонаж", Attribute = "Strength",
                AttackType = "Melee", MaxHp = 222, Attack = 33, Armor = 9, Resist = 0.2, Speed = 120
            });
            catalog.Refresh();
            var savedHero = catalog.Find("custom-test");
            Assert(savedHero is not null && savedHero.Name == "Мій персонаж" && savedHero.MaxHp == 222 && savedHero.IsUserCreated,
                "Оновлення каталогу не повинно перезаписувати локального персонажа.");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }


    private static void TestDefendPersistence()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Dota2CLI-defend-" + Guid.NewGuid().ToString("N"));
        try
        {
            var firstStore = new JsonDataStore(directory);
            using var firstApi = new OpenDotaHeroApiClient(new HttpClient(new StubHandler("[]")));
            var firstCatalog = new HeroCatalog(firstStore, firstApi);
            firstCatalog.Add(new Hero { Id = "defender", Name = "Захисник", MaxHp = 500, Attack = 10, Armor = 0, Resist = 0, Speed = 100 });
            firstCatalog.Add(new Hero { Id = "fast-opponent", Name = "Швидкий суперник", MaxHp = 500, Attack = 10, Armor = 0, Resist = 0, Speed = 200 });
            var firstGame = new BattleGame(firstStore, firstStore, firstCatalog);
            firstGame.Start("defender", "fast-opponent");
            firstGame.Act("defend");
            Assert(firstGame.Current!.PlayerDefending, "Після останньої дії раунду прапорець захисту має залишатися активним.");

            var secondStore = new JsonDataStore(directory);
            using var secondApi = new OpenDotaHeroApiClient(new HttpClient(new StubHandler("[]")));
            var secondCatalog = new HeroCatalog(secondStore, secondApi);
            var secondGame = new BattleGame(secondStore, secondStore, secondCatalog);
            secondGame.Load();
            Assert(secondGame.Current!.PlayerDefending, "Прапорець захисту має відновитися з файлу.");
            var resumedTurn = secondGame.Current!.Turn;
            secondGame.Act("attack");
            var opponentRecord = secondGame.Current!.History.First(x =>
                x.Turn == resumedTurn && x.Actor == "Швидкий суперник");
            Assert(opponentRecord.Damage == 5, "Відновлений захист має зменшити наступний удар удвічі.");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static void TestCliHandlesInvalidData()
    {
        var cli = new Dota2CLI.Cli.CommandLineInterface();
        cli.UseStrategy(new InvalidDataCommandStrategy());
        var originalOutput = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            cli.Execute("invalid-data");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }
        Assert(output.ToString().Contains("Помилка:", StringComparison.Ordinal),
            "CLI має показати повідомлення про некоректні дані замість аварійного завершення.");
    }

    private static void TestFinishedBattleRound()
    {
        var (game, _) = CreateGame(100, 100);
        game.Current!.OpponentHp = 1;
        game.Act("attack");
        Assert(game.Current.IsFinished, "Бій має завершуватися, коли HP суперника дорівнює нулю.");
        Assert(game.Current.Turn == 1, "Після завершення бою номер раунду не повинен збільшуватися.");
    }

    private static void TestSaveLoad()
    {
        var (game, _) = CreateGame(150, 100);
        game.Act("attack");
        var saved = game.Current!;
        var newGame = new BattleGame(_savedStore, _savedStore, _savedCatalog);
        newGame.Load();
        Assert(newGame.Current!.History.Count == saved.History.Count, "Історія має зберігатися після завантаження.");
        Assert(newGame.Current.Turn == saved.Turn, "Номер раунду має зберігатися після завантаження.");
        Assert(newGame.Current.PlayerProfile!.Speed == 150, "Копія профілю персонажа має зберігатися після завантаження.");
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

internal sealed class InvalidDataCommandStrategy : Dota2CLI.Cli.ICommandStrategy
{
    public IEnumerable<Dota2CLI.Cli.ICommand> GetCommands() => [new InvalidDataCommand()];
}

internal sealed class InvalidDataCommand : Dota2CLI.Cli.ICommand
{
    public string Name => "invalid-data";
    public string Usage => "invalid-data";
    public string Description => "Перевірка обробки помилки.";
    public void Execute(string[] args) => throw new InvalidDataException("Пошкоджене збереження.");
}
