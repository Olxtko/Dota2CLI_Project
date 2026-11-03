namespace Dota2CLI.Cli;

public interface ICommandStrategy { IEnumerable<ICommand> GetCommands(); }

public interface ICommand
{
    string Name { get; }
    string Usage { get; }
    string Description { get; }
    void Execute(string[] args);
}

public sealed class CommandLineInterface
{
    private readonly Dictionary<string, ICommand> _commands = new(StringComparer.OrdinalIgnoreCase);

    public void UseStrategy(ICommandStrategy strategy)
    {
        foreach (var command in strategy.GetCommands()) _commands[command.Name] = command;
    }

    public void Execute(string rawInput)
    {
        string[] parts;
        try { parts = Parse(rawInput); }
        catch (ArgumentException ex) { Console.WriteLine($"Помилка: {ex.Message}"); return; }
        if (parts.Length == 0) return;
        if (parts[0].Equals("help", StringComparison.OrdinalIgnoreCase) ||
            parts[0].Equals("--help", StringComparison.OrdinalIgnoreCase) ||
            parts[0].Equals("-h", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var c in _commands.Values.OrderBy(c => c.Name))
                Console.WriteLine($"{c.Usage,-38} {c.Description}");
            Console.WriteLine("help                                  Показати список команд");
            Console.WriteLine("exit                                  Завершити програму");
            return;
        }
        if (!_commands.TryGetValue(parts[0], out var command))
        {
            Console.WriteLine($"Невідома команда: {parts[0]}. Введіть help.");
            return;
        }
        try { command.Execute(parts.Skip(1).ToArray()); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException)
        { Console.WriteLine($"Помилка: {ex.Message}"); }
    }

    private static string[] Parse(string input)
    {
        var parts = new List<string>();
        var token = new System.Text.StringBuilder();
        bool quoted = false;
        foreach (char ch in input.Trim())
        {
            if (ch == '"') { quoted = !quoted; continue; }
            if (char.IsWhiteSpace(ch) && !quoted)
            {
                if (token.Length > 0) { parts.Add(token.ToString()); token.Clear(); }
            }
            else token.Append(ch);
        }
        if (quoted) throw new ArgumentException("Не закрито лапки в аргументі.");
        if (token.Length > 0) parts.Add(token.ToString());
        return parts.ToArray();
    }
}
