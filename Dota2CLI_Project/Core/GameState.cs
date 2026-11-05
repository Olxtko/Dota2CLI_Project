namespace Dota2CLI.Core;

public sealed class GameState
{
    public string PlayerHeroId { get; set; } = "";
    public string OpponentHeroId { get; set; } = "";
    public Hero? PlayerProfile { get; set; }
    public Hero? OpponentProfile { get; set; }
    public int PlayerHp { get; set; }
    public int OpponentHp { get; set; }
    public int Turn { get; set; } = 1;
    public bool IsFinished { get; set; }
    public string Result { get; set; } = "Гра триває";
    public bool PlayerDefending { get; set; }
    public List<TurnRecord> History { get; set; } = [];
}

public sealed class TurnRecord
{
    public int Turn { get; set; }
    public int Sequence { get; set; }
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public int Damage { get; set; }
    public int Healing { get; set; }
    public string Message { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
