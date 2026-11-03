using System.Text.Json;
using Dota2CLI.Core;

namespace Dota2CLI.Cli;

public sealed class CatalogCommands(HeroCatalog catalog) : ICommandStrategy
{
    public IEnumerable<ICommand> GetCommands() =>
    [
        new RefreshHeroes(catalog), new ListHeroes(catalog), new ShowHero(catalog), new CreateHero(catalog)
    ];
}

internal sealed class RefreshHeroes(HeroCatalog catalog) : ICommand
{
    public string Name => "refresh";
    public string Usage => "refresh";
    public string Description => "Оновити список героїв з OpenDota API та зберегти каталог локально.";
    public void Execute(string[] args)
    {
        try
        {
            catalog.Refresh();
            Console.WriteLine($"Каталог оновлено з OpenDota API. Героїв: {catalog.All.Count}");
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Не вдалося отримати каталог з інтернету: {ex.Message}");
            Console.WriteLine("Локальний каталог збережено без змін. Перевірте з'єднання та спробуйте refresh ще раз.");
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Час очікування відповіді OpenDota API вичерпано. Локальний каталог не змінено.");
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Не вдалося прочитати відповідь API: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Не вдалося оновити каталог: {ex.Message}");
        }
    }
}

internal sealed class ListHeroes(HeroCatalog catalog) : ICommand
{
    public string Name => "heroes";
    public string Usage => "heroes";
    public string Description => "Показати всіх героїв каталогу.";
    public void Execute(string[] args)
    {
        foreach (var h in catalog.All.OrderBy(h => h.Name))
            Console.WriteLine($"{h.Id,-22} {h.Name,-24} {h.Attribute,-14} {h.AttackType}");
        Console.WriteLine($"Усього героїв: {catalog.All.Count}");
    }
}

internal sealed class ShowHero(HeroCatalog catalog) : ICommand
{
    public string Name => "hero";
    public string Usage => "hero <id|name>";
    public string Description => "Показати характеристики героя та URL графіки.";
    public void Execute(string[] args)
    {
        if (args.Length == 0) throw new ArgumentException("Приклад: hero axe або hero \"Crystal Maiden\"");
        var h = catalog.Find(string.Join(' ', args)) ?? throw new InvalidOperationException("Героя не знайдено.");
        Console.WriteLine($"ID: {h.Id}\nІм'я: {h.Name}\nАтрибут: {h.Attribute}\nТип атаки: {h.AttackType}\nHP: {h.MaxHp}\nАтака: {h.Attack}\nГрафіка: {h.ArtworkUrl}");
    }
}

internal sealed class CreateHero(HeroCatalog catalog) : ICommand
{
    public string Name => "create";
    public string Usage => "create";
    public string Description => "Створити власного героя через діалог.";
    public void Execute(string[] args)
    {
        string Ask(string prompt) { Console.Write(prompt); return Console.ReadLine()?.Trim() ?? ""; }
        var name = Ask("Ім'я героя: ");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Ім'я не може бути порожнім.");
        var id = Ask("ID (латиниця, цифри, дефіс): ").ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(id) || id.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch == '-')))
            throw new ArgumentException("ID може містити латинські літери, цифри та дефіс.");
        var attribute = Ask("Основний атрибут (Strength/Agility/Intelligence/Universal): ");
        if (!new[] { "Strength", "Agility", "Intelligence", "Universal" }.Contains(attribute, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Вкажіть Strength, Agility, Intelligence або Universal.");
        var type = Ask("Тип атаки (Melee/Ranged): ");
        if (!new[] { "Melee", "Ranged" }.Contains(type, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Вкажіть Melee або Ranged.");
        if (!int.TryParse(Ask("Максимальне HP: "), out int hp) || hp < 1)
            throw new ArgumentException("HP має бути додатним цілим числом.");
        if (!int.TryParse(Ask("Сила атаки: "), out int attack) || attack < 1)
            throw new ArgumentException("Атака має бути додатним цілим числом.");
        var url = Ask("Посилання на графіку (необов'язково): ");
        catalog.Add(new Hero { Id=id, Name=name, Attribute=attribute, AttackType=type, MaxHp=hp, Attack=attack, ArtworkUrl=url });
        Console.WriteLine($"Героя {name} додано та збережено.");
    }
}
