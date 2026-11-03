using Dota2CLI.Core;

namespace Dota2CLI.Cli;

public sealed class GameCommands(BattleGame game, HeroCatalog catalog) : ICommandStrategy
{
    public IEnumerable<ICommand> GetCommands() =>
    [
        new StartBattle(game, catalog), new BattleAction(game), new BattleStatus(game)
    ];
}

internal sealed class StartBattle(BattleGame game, HeroCatalog catalog) : ICommand
{
    public string Name => "play";
    public string Usage => "play <hero-id> <opponent-id>";
    public string Description => "Почати текстовий бій між двома героями.";
    public void Execute(string[] args)
    {
        string playerId;
        string opponentId;
        if (args.Length == 2)
        {
            playerId = args[0];
            opponentId = args[1];
        }
        else if (args.Length == 0)
        {
            Console.WriteLine("Доступні герої:");
            foreach (var h in catalog.All) Console.WriteLine($"- {h.Id} ({h.Name})");
            Console.Write("Введіть ID свого героя: ");
            playerId = Console.ReadLine()?.Trim() ?? "";
            Console.Write("Введіть ID суперника: ");
            opponentId = Console.ReadLine()?.Trim() ?? "";
        }
        else throw new ArgumentException("Використання: play або play <id героя> <id суперника>.");
        game.Start(playerId, opponentId);
        Console.WriteLine("Бій розпочато!\n" + game.Status());
        Console.WriteLine("Дії: act attack | act heal | act status");
    }
}

internal sealed class BattleAction(BattleGame game) : ICommand
{
    public string Name => "act";
    public string Usage => "act <attack|heal|status>";
    public string Description => "Виконати дію під час бою.";
    public void Execute(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Приклад: act attack");
        Console.WriteLine(game.Act(args[0]));
    }
}

internal sealed class BattleStatus(BattleGame game) : ICommand
{
    public string Name => "status";
    public string Usage => "status";
    public string Description => "Показати стан останнього бою.";
    public void Execute(string[] args) => Console.WriteLine(game.Status());
}
