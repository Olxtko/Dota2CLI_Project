namespace Dota2CLI.Core;

public sealed class GameState
{
    public string PlayerHeroId { get; set; } = "";
    public string OpponentHeroId { get; set; } = "";
    public int PlayerHp { get; set; }
    public int OpponentHp { get; set; }
    public int Turn { get; set; }
    public bool IsFinished { get; set; }
    public string Result { get; set; } = "Гра триває";
}
