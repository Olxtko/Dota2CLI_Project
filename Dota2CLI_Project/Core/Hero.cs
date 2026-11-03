namespace Dota2CLI.Core;

public sealed class Hero
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Attribute { get; set; } = "Unknown";
    public string AttackType { get; set; } = "Unknown";
    public int MaxHp { get; set; } = 100;
    public int Attack { get; set; } = 10;
    public string ArtworkUrl { get; set; } = "";
}
