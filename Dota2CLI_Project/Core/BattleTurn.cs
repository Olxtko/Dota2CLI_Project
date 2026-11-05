namespace Dota2CLI.Core;

public sealed record ScheduledAction(bool IsPlayer, decimal TimePoint, int ActionNumber, int Speed);

public sealed class BattleTurn
{
    public int Number { get; }
    public IReadOnlyList<ScheduledAction> Actions { get; }

    public BattleTurn(int number, int playerSpeed, int opponentSpeed)
    {
        if (playerSpeed <= 0 || opponentSpeed <= 0)
            throw new ArgumentOutOfRangeException(nameof(playerSpeed), "Швидкість обох персонажів має бути більшою за 0.");

        Number = number;
        var actions = new List<ScheduledAction>();
        AddActions(actions, true, playerSpeed);
        AddActions(actions, false, opponentSpeed);

        Actions = actions
            .OrderBy(x => x.TimePoint)
            .ThenByDescending(x => x.Speed)
            .ThenByDescending(x => x.IsPlayer)
            .ToArray();
    }

    private static void AddActions(List<ScheduledAction> actions, bool isPlayer, int speed)
    {
        int count = Math.Max(1, speed / 100);
        for (int i = 1; i <= count; i++)
            actions.Add(new ScheduledAction(isPlayer, (decimal)i * 100 / speed, i, speed));
    }
}
