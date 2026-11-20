using Dota2CLI.Core;
using Dota2CLI.Data;

namespace Dota2CLI.Cli;

public sealed class UtilityCommands(HeroCatalog catalog, JsonDataStore store) : ICommandStrategy
{
    public IEnumerable<ICommand> GetCommands() => [new SearchHeroes(catalog), new ShowResults(store.StorageDirectory)];
}

internal sealed class SearchHeroes(HeroCatalog catalog) : ICommand
{
    public string Name => "search";
    public string Usage => "search <text>";
    public string Description => "Шукати за ім'ям, ID, атрибутом або типом атаки.";
    public void Execute(string[] args)
    {
        if (args.Length == 0) throw new ArgumentException("Приклад: search agility");
        var q = string.Join(' ', args);
        var found = catalog.All.Where(h => h.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
            || h.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
            || h.Attribute.Contains(q, StringComparison.OrdinalIgnoreCase)
            || h.AttackType.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        if (found.Count == 0) { Console.WriteLine("Героїв не знайдено."); return; }
        foreach (var h in found) Console.WriteLine($"{h.Name} [{h.Id}] — {h.Attribute}, {h.AttackType}; {h.ArtworkUrl}");
    }
}

internal sealed class ShowResults(string storageDirectory) : ICommand
{
    public string Name => "results";
    public string Usage => "results";
    public string Description => "Показати історію результатів боїв.";
    public void Execute(string[] args)
    {
        var path = Path.Combine(storageDirectory, "game-results.txt");
        Console.WriteLine(File.Exists(path) ? File.ReadAllText(path) : "Збережених результатів ще немає.");
    }
}
