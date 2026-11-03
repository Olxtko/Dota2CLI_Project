using Dota2CLI.Data;

namespace Dota2CLI.Core;

public sealed class BattleGame(JsonDataStore store, HeroCatalog catalog)
{
    private GameState? _state;
    public GameState? Current => _state;

    public void Start(string playerId, string opponentId)
    {
        var player = catalog.Find(playerId) ?? throw new InvalidOperationException("Гравця не знайдено в каталозі.");
        var opponent = catalog.Find(opponentId) ?? throw new InvalidOperationException("Суперника не знайдено в каталозі.");
        if (player.Id.Equals(opponent.Id, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Оберіть двох різних героїв.");

        _state = new GameState
        {
            PlayerHeroId = player.Id,
            OpponentHeroId = opponent.Id,
            PlayerHp = player.MaxHp,
            OpponentHp = opponent.MaxHp,
            Turn = 1
        };
        store.SaveGame(_state);
    }

    public string Act(string action)
    {
        _state ??= store.LoadGame();
        if (_state is null) throw new InvalidOperationException("Спочатку запустіть гру командою play.");
        if (_state.IsFinished) throw new InvalidOperationException("Бій завершено. Запустіть новий.");

        var player = catalog.Find(_state.PlayerHeroId)!;
        var opponent = catalog.Find(_state.OpponentHeroId)!;
        if (action.Equals("status", StringComparison.OrdinalIgnoreCase)) return Status();

        string message;
        if (action.Equals("attack", StringComparison.OrdinalIgnoreCase))
        {
            _state.OpponentHp = Math.Max(0, _state.OpponentHp - player.Attack);
            message = $"{player.Name} атакує та завдає {player.Attack} шкоди.";
        }
        else if (action.Equals("heal", StringComparison.OrdinalIgnoreCase))
        {
            int oldHp = _state.PlayerHp;
            _state.PlayerHp = Math.Min(player.MaxHp, _state.PlayerHp + 15);
            message = $"{player.Name} відновлює { _state.PlayerHp - oldHp } HP.";
        }
        else throw new InvalidOperationException("Доступні дії: attack, heal, status.");

        if (_state.OpponentHp == 0)
        {
            _state.IsFinished = true;
            _state.Result = $"Переміг {player.Name}!";
        }
        else
        {
            _state.PlayerHp = Math.Max(0, _state.PlayerHp - opponent.Attack);
            message += $"\n{opponent.Name} відповідає та завдає {opponent.Attack} шкоди.";
            if (_state.PlayerHp == 0)
            {
                _state.IsFinished = true;
                _state.Result = $"Переміг {opponent.Name}!";
            }
        }

        _state.Turn++;
        store.SaveGame(_state);
        if (_state.IsFinished) store.SaveResult(_state.Result);
        return message + "\n" + Status() + (_state.IsFinished ? $"\nРезультат: {_state.Result}" : "");
    }

    public string Status()
    {
        _state ??= store.LoadGame();
        if (_state is null) return "Збереженого стану гри немає.";
        var player = catalog.Find(_state.PlayerHeroId)?.Name ?? _state.PlayerHeroId;
        var opponent = catalog.Find(_state.OpponentHeroId)?.Name ?? _state.OpponentHeroId;
        return $"Хід: {_state.Turn}\n{player}: {_state.PlayerHp} HP\n{opponent}: {_state.OpponentHp} HP\nСтан: {(_state.IsFinished ? _state.Result : "бій триває")}";
    }
}
