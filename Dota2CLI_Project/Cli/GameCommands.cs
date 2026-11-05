using Dota2CLI.Core;

namespace Dota2CLI.Cli;

public sealed class GameCommands(BattleGame game, HeroCatalog catalog) : ICommandStrategy
{
    public IEnumerable<ICommand> GetCommands() =>
    [
        new StartBattle(game, catalog), new BattleAction(game), new BattleStatus(game), new ShowHistory(game), new SaveBattle(game), new LoadBattle(game)
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
        Console.WriteLine("Дії: act attack | act heal | act defend | act status");
    }
}

internal sealed class BattleAction(BattleGame game) : ICommand
{
    public string Name => "act";
    public string Usage => "act <attack|heal|defend|status>";
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


internal sealed class ShowHistory(BattleGame game) : ICommand
{
    public string Name => "history";
    public string Usage => "history";
    public string Description => "Показати повну історію дій поточного або останнього бою.";
    public void Execute(string[] args) => Console.WriteLine(game.History());
}

internal sealed class SaveBattle(BattleGame game) : ICommand
{
    public string Name => "save";
    public string Usage => "save";
    public string Description => "Зберегти поточний стан бою.";
    public void Execute(string[] args) { game.Save(); Console.WriteLine("Стан бою збережено."); }
}

internal sealed class LoadBattle(BattleGame game) : ICommand
{
    public string Name => "load";
    public string Usage => "load";
    public string Description => "Завантажити останній збережений бій.";
    public void Execute(string[] args) { game.Load(); Console.WriteLine("Бій завантажено." + Environment.NewLine + game.Status()); }
}
