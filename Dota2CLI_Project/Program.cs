using Dota2CLI.Cli;
using Dota2CLI.Core;
using Dota2CLI.Data;

namespace Dota2CLI;

internal static class Program
{
    private static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var store = new JsonDataStore("Data");
        using var apiClient = new OpenDotaHeroApiClient();
        var catalog = new HeroCatalog(store, apiClient);
        var game = new BattleGame(store, store, catalog);
        var cli = new CommandLineInterface();

        cli.UseStrategy(new CatalogCommands(catalog));
        cli.UseStrategy(new GameCommands(game, catalog));
        cli.UseStrategy(new UtilityCommands(catalog));

        if (args.Length > 0)
        {
            cli.Execute(string.Join(' ', args));
            return;
        }

        Console.WriteLine("DOTA 2 CLI — каталог героїв і текстова бойова гра");
        Console.WriteLine("Введіть help для списку команд, exit для завершення.");
        while (true)
        {
            Console.Write("\ndota2> ");
            var input = Console.ReadLine();
            if (input is null || input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;
            if (!string.IsNullOrWhiteSpace(input))
                cli.Execute(input);
        }
    }
}
