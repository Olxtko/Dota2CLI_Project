namespace Dota2CLI.Core;

public sealed class BattleGame(
    IGameStateRepository stateRepository,
    IGameResultRepository resultRepository,
    IHeroLookup catalog)
{
    private GameState? _state;
    public GameState? Current => _state;

    public void Start(string playerId, string opponentId)
    {
        var player = catalog.Find(playerId) ?? throw new InvalidOperationException("Гравця не знайдено в каталозі.");
        var opponent = catalog.Find(opponentId) ?? throw new InvalidOperationException("Суперника не знайдено в каталозі.");
        if (player.Id.Equals(opponent.Id, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Оберіть двох різних персонажів.");
        player.Validate();
        opponent.Validate();

        _state = new GameState
        {
            PlayerHeroId = player.Id,
            OpponentHeroId = opponent.Id,
            PlayerProfile = player.Copy(),
            OpponentProfile = opponent.Copy(),
            PlayerHp = player.MaxHp,
            OpponentHp = opponent.MaxHp,
            Turn = 1
        };
        Save();
    }

    public void Load()
    {
        var loaded = stateRepository.LoadGame() ?? throw new InvalidOperationException("Збереженого бою немає.");
        loaded.PlayerProfile ??= catalog.Find(loaded.PlayerHeroId)?.Copy();
        loaded.OpponentProfile ??= catalog.Find(loaded.OpponentHeroId)?.Copy();
        if (loaded.PlayerProfile is null || loaded.OpponentProfile is null)
            throw new InvalidOperationException("Не вдалося відновити профілі персонажів збереженого бою.");
        loaded.PlayerProfile.Validate();
        loaded.OpponentProfile.Validate();
        if (!loaded.PlayerProfile.Id.Equals(loaded.PlayerHeroId, StringComparison.OrdinalIgnoreCase) ||
            !loaded.OpponentProfile.Id.Equals(loaded.OpponentHeroId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("ID профілів не відповідають ID учасників збереженого бою.");
        if (loaded.Turn < 1 || loaded.PlayerHp < 0 || loaded.OpponentHp < 0 ||
            loaded.PlayerHp > loaded.PlayerProfile.MaxHp || loaded.OpponentHp > loaded.OpponentProfile.MaxHp ||
            (!loaded.IsFinished && (loaded.PlayerHp == 0 || loaded.OpponentHp == 0)))
            throw new InvalidDataException("Збережений стан бою містить некоректні значення HP або номера раунду.");
        loaded.History ??= [];
        _state = loaded;
    }

    public void Save()
    {
        if (_state is null) throw new InvalidOperationException("Немає активного бою для збереження.");
        stateRepository.SaveGame(_state);
    }

    public static int CalculateDamage(int attack, int armor, double resist)
    {
        var safeAttack = Math.Max(0, attack);
        var safeArmor = Math.Max(0, armor);
        var safeResist = double.IsNaN(resist) ? 0 : Math.Clamp(resist, 0.0, 1.0);
        return (int)Math.Floor(Math.Max(0, safeAttack - safeArmor) * (1.0 - safeResist));
    }

    public string Act(string action)
    {
        EnsureLoaded();
        if (_state!.IsFinished) throw new InvalidOperationException("Бій завершено. Запустіть новий.");
        if (action.Equals("status", StringComparison.OrdinalIgnoreCase)) return Status();
        if (!new[] { "attack", "heal", "defend" }.Contains(action, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Доступні дії: attack, heal, defend, status.");

        var player = _state.PlayerProfile!;
        var opponent = _state.OpponentProfile!;
        var turn = new BattleTurn(_state.Turn, player.Speed, opponent.Speed);
        var messages = new List<string>();

        foreach (var scheduled in turn.Actions)
        {
            if (_state.IsFinished) break;
            if (scheduled.IsPlayer)
                messages.Add(PerformPlayerAction(action, player, opponent, scheduled.ActionNumber));
            else
                messages.Add(PerformOpponentAction(opponent, player, scheduled.ActionNumber));
        }

        if (!_state.IsFinished)
            _state.Turn++;
        Save();
        messages.Add(Status());
        if (_state.IsFinished) messages.Add("Результат: " + _state.Result);
        return string.Join(Environment.NewLine, messages);
    }

    private string PerformPlayerAction(string action, Hero player, Hero opponent, int actionNumber)
    {
        if (action.Equals("attack", StringComparison.OrdinalIgnoreCase))
        {
            int damage = CalculateDamage(player.Attack, opponent.Armor, opponent.Resist);
            _state!.OpponentHp = Math.Max(0, _state.OpponentHp - damage);
            var message = $"{player.Name} виконує атаку №{actionNumber}: {damage} шкоди.";
            AddRecord(player.Name, "attack", damage, 0, message);
            if (_state.OpponentHp == 0) Finish(player.Name);
            return message;
        }

        if (action.Equals("heal", StringComparison.OrdinalIgnoreCase))
        {
            int oldHp = _state!.PlayerHp;
            _state.PlayerHp = Math.Min(player.MaxHp, _state.PlayerHp + 20);
            int restored = _state.PlayerHp - oldHp;
            var message = $"{player.Name} виконує лікування №{actionNumber}: +{restored} HP.";
            AddRecord(player.Name, "heal", 0, restored, message);
            return message;
        }

        _state!.PlayerDefending = true;
        var defendMessage = $"{player.Name} готує захист (дія №{actionNumber}); наступна отримана атака буде зменшена на 50%.";
        AddRecord(player.Name, "defend", 0, 0, defendMessage);
        return defendMessage;
    }

    private string PerformOpponentAction(Hero opponent, Hero player, int actionNumber)
    {
        int damage = CalculateDamage(opponent.Attack, player.Armor, player.Resist);
        if (_state!.PlayerDefending)
        {
            damage = (int)Math.Floor(damage * 0.5);
            _state.PlayerDefending = false;
        }
        _state.PlayerHp = Math.Max(0, _state.PlayerHp - damage);
        var message = $"{opponent.Name} виконує автоматичну атаку №{actionNumber}: {damage} шкоди.";
        AddRecord(opponent.Name, "attack", damage, 0, message);
        if (_state.PlayerHp == 0) Finish(opponent.Name);
        return message;
    }

    private void AddRecord(string actor, string action, int damage, int healing, string message)
    {
        _state!.History.Add(new TurnRecord
        {
            Turn = _state.Turn,
            Sequence = _state.History.Count(x => x.Turn == _state.Turn) + 1,
            Actor = actor,
            Action = action,
            Damage = damage,
            Healing = healing,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }

    private void Finish(string winner)
    {
        if (_state!.IsFinished) return;
        _state.IsFinished = true;
        _state.Result = $"Переміг {winner}!";
        resultRepository.SaveResult(_state.Result);
    }

    private void EnsureLoaded()
    {
        if (_state is null) Load();
    }

    public string History()
    {
        EnsureLoaded();
        return _state!.History.Count == 0 ? "Історія ходів порожня." :
            string.Join(Environment.NewLine, _state.History.Select(x =>
                $"Раунд {x.Turn}, дія {x.Sequence}: {x.Message}"));
    }

    public string Status()
    {
        EnsureLoaded();
        var player = _state!.PlayerProfile!;
        var opponent = _state.OpponentProfile!;
        return $"Раунд: {_state.Turn}\n{player.Name}: {_state.PlayerHp}/{player.MaxHp} HP (швидкість {player.Speed})\n" +
               $"{opponent.Name}: {_state.OpponentHp}/{opponent.MaxHp} HP (швидкість {opponent.Speed})\n" +
               $"Стан: {(_state.IsFinished ? _state.Result : "бій триває")}";
    }
}
